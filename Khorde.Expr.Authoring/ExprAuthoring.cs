using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;
using UnityEditor;
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
		delegate ushort WriteConstantDelegate(object objectValue, out ushort length, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null, List<ConstRefl> refl = null);

		static Dictionary<System.Type, WriteConstantDelegate> writeConstantMethodCache = new();

		public const ushort MaxConstantSize = 0x7fff;

		public struct ConstRefl
		{
			public Type type;
			public int offset;
			public int size;
			public int alignment;
		}

		public static ExpressionRef WriteConstant2(object value, NativeList<byte> constStorage,
			Dictionary<object, (ushort offset, ushort length)> cache = null, List<ConstRefl> reflection = null)
		{
			ushort offset = WriteConstant(value, out var length, constStorage, cache, reflection);
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
		public static ushort WriteConstant(object value, out ushort length, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null, List<ConstRefl> reflection = null)
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

			return impl(value, out length, constStorage, cache, reflection);
		}

		static ushort WriteConstantTrampoline<T>(object objectValue, out ushort length, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null, List<ConstRefl> reflection = null) where T : unmanaged
		{
			T value = (T)objectValue;
			return WriteConstant(value, out length, constStorage, cache, reflection);
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
		public static ExpressionRef WriteConstant2<T>(T value, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null, List<ConstRefl> reflection = null) where T : unmanaged
		{
			var offset = WriteConstant<T>(value, out var length, constStorage, cache, reflection);
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
		public static ushort WriteConstant<T>(T value, out ushort length, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null, List<ConstRefl> reflection = null) where T : unmanaged
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

			reflection?.Add(new()
			{
				type = typeof(T),
				offset = offset,
				size = size,
				alignment = align,
			});

			return (ushort)offset;
		}

		public static void BakeConstStorage(ref BlobBuilder builder, ref BlobExpressionData exprData, NativeList<byte> constStorage, List<ConstRefl> reflection = null)
		{
			unsafe
			{
				UnsafeUtility.MemCpy(
					builder.Allocate(ref exprData.constants, constStorage.Length).GetUnsafePtr(),
					constStorage.GetUnsafePtr(),
					constStorage.Length
				);
			}

			if(reflection != null)
			{
				var result = builder.Allocate(ref exprData.constantReflection, reflection.Count);
				for(int i = 0; i < reflection.Count; ++i)
				{
					ref var dst = ref result[i];
					var src = reflection[i];

					dst.size = src.size;
					dst.alignment = src.alignment;
					dst.offset = src.offset;
					builder.AllocateString(ref dst.typeName, src.type.FullName);
					builder.AllocateString(ref dst.typeAssembly, src.type.Assembly.FullName);
				}
			}
			else
			{
				builder.Allocate(ref exprData.constantReflection, 0);
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
			public byte[] defaultValue;
		}

		/// <summary>
		/// Compute a combined blackboard variable layout for a set of expression graphs sharing the same blackboard.
		/// </summary>
		/// <param name="expressions"></param>
		/// <returns>A set of computed offsets into the shared blackboard. Each graph asset gets its own subset for the variables it uses.</returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static Dictionary<Hash128, List<LayoutVariable>>
			ComputeLayout(List<(Hash128 assetHash, Ptr<BlobExpressionData> assetData, string assetName)> expressions)
		{
			var variableSets = new List<List<(string name, Type type, bool isGlobal, byte[] defaultValue)>>();

			var assemblies = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().ToDictionary(asm => asm.FullName);

			for(int assetIndex = 0; assetIndex < expressions.Count; ++assetIndex)
			{
				ref var data = ref expressions[assetIndex].assetData.ValueRW;
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

					byte[] defaultValue;
					if(variable.defaultValue.Length > 0)
						defaultValue = variable.defaultValue.ToArray();
					else
						defaultValue = Array.Empty<byte>();

					var name = variable.name.ToString();
					if(!variable.isGlobal)
						name += varIndex;
					variableSet.Add((name, type, variable.isGlobal, defaultValue));
				}
			}

			var globals = new Dictionary<string, (Type type, byte[] defaultValue, int initialAssetIndex)>();

			// all variables with a unique storage location
			var allVars = new Dictionary<(int assetIndex, string name), (int size, int alignment, Type type, int varIndex, byte[] defaultValue)>();

			for(int assetIndex = 0; assetIndex < variableSets.Count; ++assetIndex)
			{
				var variableSet = variableSets[assetIndex];

				for(int varIndex = 0; varIndex < variableSet.Count; ++varIndex)
				{
					var variable = variableSet[varIndex];
					if(variable.isGlobal)
					{
						if(globals.TryGetValue(variable.name, out var global))
						{
							if(global.type != variable.type)
								throw new InvalidOperationException($"global variable '{variable.name}' has conflicting types '{global.type.FullName}' and '{variable.type.FullName}'");

							bool ok = true;

							if((global.defaultValue?.Length == 0) != (variable.defaultValue?.Length == 0))
								ok = IsAllZero(global.defaultValue?.Length > 0 ? global.defaultValue : variable.defaultValue);
							else
								ok = Enumerable.SequenceEqual(global.defaultValue, variable.defaultValue);

							if(!ok)
							{
								var obj0 = Activator.CreateInstance(global.type);
								var obj1 = Activator.CreateInstance(global.type);
								CopyBoxedValue(obj0, global.defaultValue);
								CopyBoxedValue(obj1, variable.defaultValue);

								var name0 = expressions[global.initialAssetIndex].assetName;
								var name1 = expressions[assetIndex].assetName;

								var hex0 = BitConverter.ToString(global.defaultValue);
								var hex1 = BitConverter.ToString(variable.defaultValue);

								throw new InvalidOperationException(
									$"global variable '{variable.name}' has conflicting default values " +
									$"'{obj0}' ({hex0}) (from '{name0}') and " +
									$"'{obj1}' ({hex1}) (from '{name1}')");
							}
						}
						else
						{
							globals.Add(variable.name, (variable.type, variable.defaultValue, assetIndex));
							allVars[(-1, variable.name)] = (UnsafeUtility.SizeOf(variable.type), AlignOf(variable.type), variable.type, varIndex, variable.defaultValue);
						}
					}
					else
					{
						allVars[(assetIndex, variable.name)] = (UnsafeUtility.SizeOf(variable.type), AlignOf(variable.type), variable.type, varIndex, variable.defaultValue);
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
					assetLayout.Add(new LayoutVariable { offset = slice.offset, length = slice.length, name = variable.name, isGlobal = variable.isGlobal, defaultValue = variable.defaultValue });
				}
			}

			return assetLayouts;
		}

		public static void DumpLayout(Dictionary<Hash128, List<LayoutVariable>> layout, params Blobs.BlobAssetBase[] assets)
			=> DumpLayout(layout, (IEnumerable<Blobs.BlobAssetBase>)assets);

		public static void DumpLayout(Dictionary<Hash128, List<LayoutVariable>> layout, IEnumerable<Blobs.BlobAssetBase> assets)
		{
			var assetLookup = assets.ToDictionary(asset => asset.DataHash);

			foreach(var (asset, layoutVariables) in layout)
			{
				Debug.Log($"{assetLookup[asset]} blackboard layout:\n" + string.Join('\n', layoutVariables.Select(lv => $"{lv.name}: {lv.offset}+{lv.length} (global:{lv.isGlobal})")));
			}
		}

		private static bool IsAllZero(byte[] bytes)
		{
			for(int i = 0; i < bytes.Length; ++i)
				if(bytes[i] != 0)
					return false;

			return true;
		}

		static void CopyBoxedValue(object dst, byte[] src)
		{
			if(dst == null)
				throw new ArgumentNullException(nameof(dst));

			if(dst.GetType().IsValueType != true)
				throw new InvalidOperationException();

			var handle = GCHandle.Alloc(dst, GCHandleType.Pinned);
			try
			{
				unsafe
				{
					src.AsSpan().CopyTo(new Span<byte>((void*)handle.AddrOfPinnedObject(), UnsafeUtility.SizeOf(dst.GetType())));
				}
			}
			finally
			{
				handle.Free();
			}
		}

		/// <summary>
		/// Bake a computed shared blackboard variable layout into a blob asset
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

				container.byteLength = math.max(container.byteLength, byteLength);

				++index;
			}

			return bb.CreateBlobAssetReference<ExpressionBlackboardLayouts.LayoutContainer>(allocator);
		}

		public static void InitializeBlackboard(NativeArray<ExpressionBlackboardStorage> blackboard,
			Dictionary<Hash128, List<LayoutVariable>> layouts)
		{
			int byteLength = 0;

			foreach(var (_, layout) in layouts)
				for(int i = 0; i < layout.Count; ++i)
					byteLength = math.max(byteLength, layout[i].length + layout[i].offset);

			int elemSize = UnsafeUtility.SizeOf<ExpressionBlackboardStorage>();

			var blackboardBytes = blackboard.Reinterpret<byte>(UnsafeUtility.SizeOf<ExpressionBlackboardStorage>()).AsSpan();

			foreach(var (_, layout) in layouts)
			{
				foreach(var variable in layout)
				{
					if(variable.defaultValue != null)
					{
						variable.defaultValue.AsSpan().CopyTo(blackboardBytes.Slice(variable.offset, variable.length));
					}
				}
			}
		}

		public static int AlignOf(Type type)
		{
			return (int)typeof(UnsafeUtility).GetMethod(nameof(UnsafeUtility.AlignOf), BindingFlags.Static | BindingFlags.Public)
				.MakeGenericMethod(type)
				.Invoke(null, Array.Empty<object>());
		}

		public static IEnumerable<GUID> GetSubgraphs(this Graph graph)
		{
			var assets = new HashSet<GUID>();
			var visited = new HashSet<Graph>();
			GetSubgraphs(graph, visited, assets);
			return assets;
		}

		static void GetSubgraphs(Graph graph, HashSet<Graph> visited, HashSet<GUID> assets)
		{
			foreach(var node in graph.GetNodes())
			{
				if(node is ISubgraphNode subgraphNode)
				{
					var subgraph = subgraphNode.GetSubgraph();
					if(!visited.Contains(subgraph))
					{
						visited.Add(subgraph);
						if(subgraphNode.TryGetSubgraphAssetGuid(out var assetGuid))
							assets.Add(assetGuid);

						GetSubgraphs(subgraph, visited, assets);
					}
				}
			}
		}

		static readonly Regex kAssetHash = new("(Hash: |guid: )([0-9a-f]{32})");

		public static string[] GatherDependenciesFromSourceFile(string path)
		{
			// subgraph references: "Hash: [0-9a-f]{32}" (nibble-reversed format, though that might change in 6.4?)
			// regular asset references: "fileID: [0-9]+, guid: [0-9a-f]{32}
			var text = File.ReadAllText(path);
			var matches = kAssetHash.Matches(text);

			var result = new List<string>();

			for(int i = 0; i < matches.Count; ++i)
			{
				var hex = matches[i].Groups[2].Value;
				if(GUID.TryParse(hex, out var guid))
				{
					var assetPath = AssetDatabase.GUIDToAssetPath(guid);

					if(string.IsNullOrWhiteSpace(assetPath))
					{
						// subgraph references are stored in nibble-reversed format
						var hash = UnityEngine.Hash128.Parse(hex);
						guid = UnsafeUtility.As<UnityEngine.Hash128, GUID>(ref hash);
						assetPath = AssetDatabase.GUIDToAssetPath(guid);
					}

					if(!string.IsNullOrWhiteSpace(assetPath))
					{
						if(!assetPath.EndsWith(".cs"))
							result.Add(assetPath);
					}
				}
			}

			return result.ToArray();
		}
	}
}