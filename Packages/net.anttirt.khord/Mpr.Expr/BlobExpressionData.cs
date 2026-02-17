using Mpr.Blobs;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Mpr.Expr
{
	/// <summary>
	/// Root structure for storing an expression graph in a blob.
	/// </summary>
	public struct BlobExpressionData
	{
		public const int SchemaVersion = 1;

		/// <summary>
		/// Storage for constant-valued expression node references
		/// </summary>
		public BlobArray<byte> constants;

		/// <summary>
		/// Storage for expressions
		/// </summary>
		public BlobArray<ExpressionData> expressions;

		/// <summary>
		/// Used after loading an expression data blob to populate the function pointers in the <see cref="expressions"/> array
		/// </summary>
		public BlobArray<ulong> expressionTypeHashes;

		/// <summary>
		/// Debug type names matching <see cref="expressionTypeHashes"/>
		/// </summary>
		public BlobArray<BlobString> expressionDebugTypeNames;

		/// <summary>
		/// Component types (especially non-[ChunkSerializable] ones) might have a
		/// different layout on the target platform so we have to initialize layouts
		/// at runtime
		/// </summary>
		public BlobArray<BlobPtr<ExpressionComponentTypeInfo>> patchableTypeInfos;

		/// <summary>
		/// Used after loading an expression data blob to populate the field offsets
		/// in ExpressionComponentTypeInfo
		/// </summary>
		public BlobArray<ulong> typeInfoTypeHashes;

		/// <summary>
		/// Source graph node ids corresponding to nodes in the <see cref="expressions"/> array. Used for debugging.
		/// </summary>
		public BlobArray<UnityEngine.Hash128> sourceGraphNodeIds;

		/// <summary>
		/// Output definitions for graph types that can be evaluated directly.
		/// </summary>
		public BlobArray<ExpressionOutput> outputs;

		/// <summary>
		/// List of local component types used by this expression graph.
		/// Indices in this array correspond to <see cref="ExpressionComponentTypeInfo.componentIndex"/>
		/// for local components.
		/// </summary>
		public BlobArray<BlobComponentType> localComponents;

		/// <summary>
		/// List of local component types used by this expression graph.
		/// Indices in this array correspond to <see cref="ExpressionComponentTypeInfo.componentIndex"/>
		/// for lookup components.
		/// </summary>
		public BlobArray<BlobComponentType> lookupComponents;

		public struct BlackboardVariable
		{
			public BlobString name;
			public BlobString typeAssembly;
			public BlobString typeName;
			public bool isGlobal;
		}

		/// <summary>
		/// Blackboard variable definitions for baking and debugging
		/// </summary>
		public BlobArray<BlackboardVariable> blackboardVariables;

		/// <summary>
		/// Get constants buffer as a NativeArray
		/// </summary>
		/// <returns></returns>
		public NativeArray<byte> GetConstants()
		{
			unsafe
			{
				var slice = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(
					constants.GetUnsafePtr(),
					constants.Length,
					Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref slice, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
				return slice;
			}
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void CheckConstantRange(int start, int length)
		{
			unchecked
			{
				if(start < 0)
					throw new ArgumentOutOfRangeException(nameof(start));

				if(start + length < start)
					throw new ArgumentOutOfRangeException(nameof(length));
			}

			if(constants.Length < start + length)
				throw new IndexOutOfRangeException();
		}

		public unsafe ref readonly TConstant GetConstant<TConstant>(int byteOffset) where TConstant : unmanaged
		{
			CheckConstantRange(byteOffset, sizeof(TConstant));
			return ref *(TConstant*)(byteOffset + (byte*)constants.GetUnsafePtr());
		}

		private bool isRuntimeInitialized;
		private ObjectLoadingStatus loadingStatus;

		/// <summary>
		/// Whether <see cref="RuntimeInitialize"/> has been called on this instance.
		/// </summary>
		public bool IsRuntimeInitialized => isRuntimeInitialized;

		public static FieldInfo[] GetComponentFields<T>() where T : unmanaged, IComponentData
			=> GetComponentFields(typeof(T));

		static FieldInfo[] GetComponentFields(Type type)
			=> type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.OrderBy(UnsafeUtility.GetFieldOffset)
				.ToArray();

		struct ComponentReflectionCache
		{
			public NativeHashMap<ulong, UnsafeList<ExpressionComponentTypeInfo.Field>> Cache;
			public FunctionPointer<ComputeFieldsDelegate> ComputeFields;

			public delegate void ComputeFieldsDelegate(ulong typeHash,
				out UnsafeList<ExpressionComponentTypeInfo.Field> fields);

			[AOT.MonoPInvokeCallback(typeof(ComputeFieldsDelegate))]
			public static void GetFieldsImpl(ulong typeHash, out UnsafeList<ExpressionComponentTypeInfo.Field> fields)
			{
				var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(typeHash);
				if(typeIndex == default)
					throw new InvalidOperationException($"couldn't find type index for StableTypeHash {typeHash}");

				var info = TypeManager.GetTypeInfo(typeIndex);
				var reflFields = GetComponentFields(info.Type);
				fields = new UnsafeList<ExpressionComponentTypeInfo.Field>(reflFields.Length, Allocator.Domain);
				foreach(var field in reflFields)
					fields.Add(field);
			}
		}

		static readonly SharedStatic<ComponentReflectionCache> Cache
			= SharedStatic<ComponentReflectionCache>.GetOrCreate<ComponentReflectionCache>();

		private static ComponentReflectionCache.ComputeFieldsDelegate s_computeFields;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
#endif
		static void Initialize()
		{
			s_computeFields = ComponentReflectionCache.GetFieldsImpl;
			Cache.Data.Cache = new(0, Allocator.Domain);
			Cache.Data.ComputeFields = new(Marshal.GetFunctionPointerForDelegate(s_computeFields));
		}

		static UnsafeList<ExpressionComponentTypeInfo.Field> GetFields(ulong typeHash)
		{
			if(Hint.Unlikely(!Cache.Data.Cache.TryGetValue(typeHash, out var fields)))
			{
				Cache.Data.ComputeFields.Invoke(typeHash, out fields);
				Cache.Data.Cache[typeHash] = fields;
			}

			return fields;
		}

		/// <summary>
		/// Initialize expression function pointers, patch strong object refs, start loading weak object refs, etc.
		/// </summary>
		public void RuntimeInitialize()
		{
			if(isRuntimeInitialized)
				return;

			isRuntimeInitialized = true;

			if(expressions.Length != expressionTypeHashes.Length)
			{
				throw new InvalidOperationException("corrupted data: must have the same amount of expressions and expression type hashes");
			}

			for(int i = 0; i < expressions.Length; ++i)
			{
				var expressionTypeHash = expressionTypeHashes[i];
				if(expressionTypeHash == 0)
					continue;

				if(ExpressionTypeManager.TryGetEvaluateFunction(expressionTypeHash, out var function))
				{
					expressions[i].evaluateFuncPtr = (long)function.Value;
				}
				else
				{
					FixedString512Bytes msg = $"couldn't find generated type info for hash {expressionTypeHash} at index {i} (type ";
					FixedString512Bytes typeName = default;
					expressionDebugTypeNames[i].CopyTo(ref typeName);
					msg.Append(typeName);
					msg.Append(')');
					Debug.LogError(msg);
					throw new InvalidOperationException("couldn't find generated type info; add expression type to ExpressionTypeRegistry.gen.cs");
				}
			}

			for(int i = 0; i < patchableTypeInfos.Length; ++i)
			{
				ref var typeInfo = ref patchableTypeInfos[i];
				var componentTypeHash = typeInfoTypeHashes[i];
				var fields = GetFields(componentTypeHash);
				ref var patchedFields = ref typeInfo.Value.fields;
				for(int j = 0; j < fields.Length; ++j)
					patchedFields[j] = fields[j];
			}
		}

		public void CheckExpressionComponents(NativeArray<UnsafeComponentReference> componentPtrs, NativeArray<UntypedComponentLookup> lookups)
		{
			if(localComponents.Length > componentPtrs.Length)
			{
				var missing = new NativeHashSet<TypeIndex>(0, Allocator.Temp);
				foreach(ref var bct in localComponents.AsSpan())
					missing.Add(bct.ResolveComponentType().TypeIndex);

				foreach(var cptr in componentPtrs)
					missing.Remove(cptr.typeIndex);

				foreach(var m in missing)
					Debug.LogError($"missing local component {TypeManager.GetTypeInfo(m).DebugTypeName}");

				throw new Exception(
					$"not enough components; bt requires {localComponents.Length} but only {componentPtrs.Length} found");
			}

			if(localComponents.Length < componentPtrs.Length)
				throw new Exception(
					$"too many components; bt requires {localComponents.Length} but {componentPtrs.Length} found");

			for(int i = 0; i < localComponents.Length; ++i)
				if(localComponents[i].stableTypeHash != componentPtrs[i].stableTypeHash)
					throw new Exception($"wrong type at index {i}, expected " +
										$"{TypeManager.GetTypeInfo(TypeManager.GetTypeIndexFromStableTypeHash(localComponents[i].stableTypeHash)).DebugTypeName}, found " +
										$"{TypeManager.GetTypeInfo(componentPtrs[i].typeIndex).DebugTypeName}");

			if(lookups.Length != lookupComponents.Length)
			{
				throw new Exception($"wrong number of lookups; expected {lookupComponents.Length}, found {lookups.Length}");
			}

			for(int i = 0; i < lookupComponents.Length; ++i)
			{
				if(!lookups[i].IsCreated)
					throw new Exception($"component lookup at index {i} was not created");

				// TODO: this is an expensive check, remove it somehow
				if(lookupComponents[i].ResolveComponentType().TypeIndex != lookups[i].TypeIndex)
					throw new Exception($"wrong type at index {i}, expected " +
										$"{TypeManager.GetTypeInfo(TypeManager.GetTypeIndexFromStableTypeHash(lookupComponents[i].stableTypeHash)).DebugTypeName}, found " +
										$"{TypeManager.GetTypeInfo(lookups[i].TypeIndex).DebugTypeName}");
			}
		}
	}
}