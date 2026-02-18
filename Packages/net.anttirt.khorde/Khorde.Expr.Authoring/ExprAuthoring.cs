using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Khorde.Expr.Authoring
{
	/// <summary>
	/// Reference to storage for a single expression. Pass this to <see cref="ExpressionBakingContext.Allocate"/>
	/// </summary>
	public unsafe ref struct ExpressionStorageRef
	{
		public ExpressionStorage* storage;
		public ulong* typeHash;
		public BlobString* debugTypeName;

		public ExpressionStorageRef(ref ExpressionStorage storage, ref ulong typeHash, ref BlobString debugTypeName)
		{
			fixed(ExpressionStorage* ptr = &storage)
				this.storage = ptr;
			fixed(ulong* ptr = &typeHash)
				this.typeHash = ptr;
			fixed(BlobString* ptr = &debugTypeName)
				this.debugTypeName = ptr;
		}
	}

	public static class ExprAuthoring
	{
		delegate ushort WriteConstantDelegate(object objectValue, out ushort length, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache);

		static Dictionary<System.Type, WriteConstantDelegate> writeConstantMethodCache = new();

		public const ushort MaxConstantSize = 0x7fff;

		public static ExpressionRef WriteConstant2(object value, NativeList<byte> constStorage,
			Dictionary<object, (ushort offset, ushort length)> cache = null)
		{
			ushort offset = WriteConstant(value, out var length, constStorage, cache);
			return ExpressionRef.Const(offset, length);
		}

		/// <summary>
		/// Write a boxed constant value to constant storage, returning the offset and length. The content of the boxed value must have an unmanaged type.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="length"></param>
		/// <param name="constStorage"></param>
		/// <param name="cache">Value cache for constant value deduplication</param>
		/// <returns></returns>
		/// <exception cref="System.InvalidOperationException"></exception>
		public static ushort WriteConstant(object value, out ushort length, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null)
		{
			var type = value.GetType();

			if(!writeConstantMethodCache.TryGetValue(type, out var impl))
			{
				if(!UnsafeUtility.IsUnmanaged(type))
					throw new System.InvalidOperationException($"Attempt to write constant of managed type '{type}', only unmanaged types are allowed");

				impl = (WriteConstantDelegate)typeof(ExprAuthoring)
					.GetMethod(nameof(WriteConstantTrampoline), BindingFlags.Static | BindingFlags.NonPublic)
					.MakeGenericMethod(type)
					.CreateDelegate(typeof(WriteConstantDelegate));
				writeConstantMethodCache[type] = impl;
			}

			return impl(value, out length, constStorage, cache);
		}

		static ushort WriteConstantTrampoline<T>(object objectValue, out ushort length, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null) where T : unmanaged
		{
			T value = (T)objectValue;
			return WriteConstant(value, out length, constStorage, cache);
		}

		/// <summary>
		/// Write a value to constant storage, returning an <see cref="ExpressionRef"/> pointing to the constant.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="constStorage"></param>
		/// <param name="cache">Value cache for constant value deduplication</param>
		/// <returns></returns>
		/// <exception cref="System.Exception"></exception>
		public static ExpressionRef WriteConstant2<T>(T value, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null) where T : unmanaged
		{
			var offset = WriteConstant<T>(value, out var length, constStorage, cache);
			return ExpressionRef.Const(offset, length);
		}

		/// <summary>
		/// Write a value to constant storage, returning the offset and length.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="length"></param>
		/// <param name="constStorage"></param>
		/// <param name="cache">Value cache for constant value deduplication</param>
		/// <returns></returns>
		/// <exception cref="System.Exception"></exception>
		public static ushort WriteConstant<T>(T value, out ushort length, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null) where T : unmanaged
		{
			if(cache != null)
			{
				if(cache.TryGetValue(value, out var result))
				{
					length = result.length;
					return result.offset;
				}
			}

			int align = UnsafeUtility.AlignOf<T>();
			int size = UnsafeUtility.SizeOf<T>();
			if(size > MaxConstantSize)
				throw new System.Exception("max constant size 32767 bytes");

			length = (ushort)size;

			int rem = constStorage.Length % align;
			int offset = constStorage.Length;
			if(rem != 0)
				offset += align - rem;

			if(offset + size > ushort.MaxValue)
				throw new System.Exception("too many constants, max 65535 bytes storage");

			constStorage.ResizeUninitialized(offset + size);

			unsafe
			{
				byte* src = (byte*)&value;
				byte* dst = constStorage.GetUnsafePtr() + offset;
				UnsafeUtility.MemCpy(dst, src, size);
			}

			if(cache != null)
			{
				cache[value] = ((ushort)offset, length);
			}

			return (ushort)offset;
		}

		public static void BakeConstStorage(ref BlobBuilder builder, ref BlobExpressionData exprData, NativeList<byte> constStorage)
		{
			unsafe
			{
				UnsafeUtility.MemCpy(
					builder.Allocate(ref exprData.constants, constStorage.Length).GetUnsafePtr(),
					constStorage.GetUnsafePtr(),
					constStorage.Length
				);
			}
		}

		/// <summary>
		/// Allocate storage for an expression and record its type.
		/// </summary>
		/// <param name="builder">The blob builder being used for the current expression graph</param>
		/// <param name="storage">Reference to the storage slot for the expression being currently baked</param>
		/// <param name="hashCache">Cache of computed type hashes</param>
		/// <typeparam name="TExpression"></typeparam>
		/// <returns></returns>
		public static unsafe ref TExpression Allocate<TExpression>(ref BlobBuilder builder, ExpressionStorageRef storage, Dictionary<Type, ulong> hashCache) where TExpression : unmanaged, IExpressionBase
		{
			*storage.typeHash = ExpressionTypeManager.GetTypeHash<TExpression>(hashCache);
			builder.AllocateString(ref *storage.debugTypeName, typeof(TExpression).FullName);
			if(UnsafeUtility.SizeOf<TExpression>() <= UnsafeUtility.SizeOf<ExpressionStorage>())
			{
				return ref *(TExpression*)storage.storage;
			}
			else
			{
				ref var blobPtr = ref storage.storage->GetDataReference<TExpression>();
				return ref builder.Allocate(ref blobPtr);
			}
		}

		public struct LayoutVariable
		{
			public int offset;
			public int length;
			public string name;
			public bool isGlobal;
		}

		/// <summary>
		/// Compute a combined blackboard variable layout for a set of expression graphs sharing the same blackboard.
		/// </summary>
		/// <param name="expressions"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static Dictionary<Hash128, List<LayoutVariable>>
			ComputeLayout(List<(Hash128, Ptr<BlobExpressionData>)> expressions)
		{
			var variableSets = new List<List<(string name, Type type, bool isGlobal)>>();

			var assemblies = AppDomain.CurrentDomain.GetAssemblies().ToDictionary(asm => asm.FullName);

			for(int assetIndex = 0; assetIndex < expressions.Count; ++assetIndex)
			{
				ref var data = ref expressions[assetIndex].Item2.ValueRW;
				variableSets.Add(new());
				var variableSet = variableSets[^1];

				for(int varIndex = 0; varIndex < data.blackboardVariables.Length; ++varIndex)
				{
					ref var variable = ref data.blackboardVariables[varIndex];

					if(!assemblies.TryGetValue(variable.typeAssembly.ToString(), out var assembly))
						throw new InvalidOperationException($"expression references type '{variable.typeName.ToString()}' in unknown assembly '{variable.typeAssembly.ToString()}'");

					var type = assembly.GetType(variable.typeName.ToString());
					if(type == null)
						throw new InvalidOperationException($"expression references unknown type '{variable.typeName.ToString()}' in assembly '{variable.typeAssembly.ToString()}'");

					var name = variable.name.ToString();

					variableSet.Add((name, type, variable.isGlobal));
				}
			}

			var globals = new Dictionary<string, Type>();

			// all variables with a unique storage location
			var allVars = new Dictionary<(int assetIndex, string name), (int size, int alignment, Type type, int varIndex)>();

			for(int assetIndex = 0; assetIndex < variableSets.Count; ++assetIndex)
			{
				var variableSet = variableSets[assetIndex];

				for(int varIndex = 0; varIndex < variableSet.Count; ++varIndex)
				{
					var variable = variableSet[varIndex];
					if(variable.isGlobal)
					{
						if(globals.TryGetValue(variable.name, out var type))
						{
							if(type != variable.type)
								throw new InvalidOperationException($"global variable '{variable.name}' has conflicting types '{type.FullName}' and '{variable.type.FullName}'");
						}
						else
						{
							globals.Add(variable.name, variable.type);
							allVars[(-1, variable.name)] = (UnsafeUtility.SizeOf(variable.type), AlignOf(variable.type), variable.type, varIndex);
						}
					}
					else
					{
						allVars[(assetIndex, variable.name)] = (UnsafeUtility.SizeOf(variable.type), AlignOf(variable.type), variable.type, varIndex);
					}
				}
			}

			var packing = allVars
				.OrderByDescending(kv => kv.Value.alignment)
				.ThenBy(kv => kv.Key.assetIndex)
				.ThenBy(kv => kv.Value.varIndex)
				.Select(kv => (kv.Key.assetIndex, kv.Key.name, kv.Value.size, kv.Value.alignment))
				.ToList();

			var layout = new Dictionary<(int assetIndex, string name), (int offset, int length)>();

			int currentOffset = 0;
			foreach(var p in packing)
			{
				var rem = currentOffset % p.alignment;
				if(rem != 0)
					currentOffset += p.alignment - rem;

				layout[(p.assetIndex, p.name)] = (currentOffset, p.size);

				//Debug.Log($"asset {p.assetIndex} var {p.name}: offset {currentOffset} len {p.size} align {p.alignment}");

				currentOffset += p.size;
			}

			var assetLayouts = new Dictionary<Hash128, List<LayoutVariable>>();

			for(int assetIndex = 0; assetIndex < variableSets.Count; ++assetIndex)
			{
				var variableSet = variableSets[assetIndex];
				var asset = expressions[assetIndex].Item1;
				var assetLayout = assetLayouts[asset] = new();

				for(int varIndex = 0; varIndex < variableSet.Count; ++varIndex)
				{
					var variable = variableSet[varIndex];

					var slice = default((int offset, int length));
					if(variable.isGlobal)
						slice = layout[(-1, variable.name)];
					else
						slice = layout[(assetIndex, variable.name)];
					assetLayout.Add(new LayoutVariable { offset = slice.offset, length = slice.length, name = variable.name, isGlobal = variable.isGlobal });
				}
			}

			return assetLayouts;
		}

		/// <summary>
		/// Bake a computed layout into a blob asset
		/// </summary>
		/// <param name="layouts"></param>
		/// <param name="allocator"></param>
		/// <returns></returns>
		public static BlobAssetReference<ExpressionBlackboardLayouts.LayoutContainer> BakeLayout(
			Dictionary<Hash128, List<LayoutVariable>> layouts, Allocator allocator)
		{
			var bb = new BlobBuilder(Allocator.Temp);

			ref var container = ref bb.ConstructRoot<ExpressionBlackboardLayouts.LayoutContainer>();
			var blobLayouts = bb.Allocate(ref container.layouts, layouts.Count);

			int index = 0;
			foreach(var (asset, layout) in layouts)
			{
				blobLayouts[index].asset = asset;
				var blobLayout = bb.Allocate(ref blobLayouts[index].variables, layout.Count);
				int byteLength = 0;
				for(int i = 0; i < layout.Count; ++i)
				{
					blobLayout[i].length = layout[i].length;
					blobLayout[i].offset = layout[i].offset;
					byteLength = math.max(byteLength, layout[i].length + layout[i].offset);
				}
				blobLayouts[index].minByteLength = byteLength;
				++index;
			}

			return bb.CreateBlobAssetReference<ExpressionBlackboardLayouts.LayoutContainer>(allocator);
		}

		static int AlignOf(Type type)
		{
			return (int)typeof(UnsafeUtility).GetMethod(nameof(UnsafeUtility.AlignOf), BindingFlags.Static | BindingFlags.Public)
				.MakeGenericMethod(type)
				.Invoke(null, Array.Empty<object>());
		}
	}
}