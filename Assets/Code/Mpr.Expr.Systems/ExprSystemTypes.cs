using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Mpr.Expr
{
    public struct ExprSystemTypeHandleHolder : IBufferElementData
    {
        /// <summary>
        /// Type handle for passing component data references to BT evaluation during IJobChunk iteration
        /// </summary>
        public DynamicComponentTypeHandle typeHandle;

        /// <summary>
        /// Stable type hash of the component type
        /// </summary>
        public ulong stableTypeHash;

        public TypeIndex typeIndex;

        /// <summary>
        /// Size in bytes of the component type
        /// </summary>
        public int typeSize;
    }

    public struct ExprSystemComponentLookupHolder : IBufferElementData
    {
        /// <summary>
        /// Component lookup for accessing component data on other entities during BT evaluation
        /// </summary>
        public UntypedComponentLookup componentLookup;

        /// <summary>
        /// Stable type hash of the component type
        /// </summary>
        public ulong stableTypeHash;

        /// <summary>
        /// Size in bytes of the component type
        /// </summary>
        public int typeSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ExprJobComponentTypeHandles
    {
        DynamicComponentTypeHandle type0;
        DynamicComponentTypeHandle type1;
        DynamicComponentTypeHandle type2;
        DynamicComponentTypeHandle type3;
        DynamicComponentTypeHandle type4;
        DynamicComponentTypeHandle type5;
        DynamicComponentTypeHandle type6;
        DynamicComponentTypeHandle type7;
        DynamicComponentTypeHandle type8;
        DynamicComponentTypeHandle type9;
        FixedList512Bytes<UnsafeComponentReference> components;
        FixedList128Bytes<IntPtr> basePointers;
        const int kMaxHandles = 10;

        public int Length => components.Length;

        public int GetTypeSize(int index) => components[index].typeSize;

        Span<DynamicComponentTypeHandle> Handles
        {
            get
            {
                unsafe
                {
                    fixed(DynamicComponentTypeHandle* ptr = &type0)
                        return new Span<DynamicComponentTypeHandle>(ptr, kMaxHandles);
                }
            }
        }

        public NativeArray<UnsafeComponentReference> GetComponents(int entityIndex)
        {
            if (components.Length == 0)
                return default;
			
            for(int i = 0; i < components.Length; ++i)
                components.ElementAt(i).data = basePointers[i] + entityIndex * components[i].typeSize;

            unsafe
            {
                ref var c0 = ref components.ElementAt(0);
                fixed (void* pc0 = &c0)
                {
                    var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<UnsafeComponentReference>(pc0,
                        components.Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
                    return result;
                }
            }
        }

        public void Initialize(in ArchetypeChunk chunk)
        {
            basePointers.Length = Length;

            for(int i = 0; i < Length; ++i)
            {
                ref var handle = ref Handles[i];
                var data = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref handle, GetTypeSize(i));

                unsafe
                {
                    basePointers[i] = handle.IsReadOnly ? (IntPtr)data.GetUnsafeReadOnlyPtr() : (IntPtr)data.GetUnsafePtr();
                }
            }
        }

        public void AddType(ExprSystemTypeHandleHolder holder)
        {
            if(components.Length >= kMaxHandles)
                throw new Exception("max supported component type count is 10");

            Handles[components.Length] = holder.typeHandle;
            components.Add(new UnsafeComponentReference
            {
                data = default,
                typeSize = holder.typeSize,
                stableTypeHash = holder.stableTypeHash,
                typeIndex = holder.typeIndex,
            });
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ExprJobComponentLookups
    {
        [NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup0;
        [NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup1;
        [NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup2;
        [NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup3;
        [NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup4;
        [NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup5;
        [NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup6;
        [NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup7;
        [NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup8;
        [NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup9;

        FixedList64Bytes<int> componentTypeSizes;
        FixedList128Bytes<ulong> componentTypeHashes;
		
        public NativeArray<UntypedComponentLookup> Lookups
        {
            get
            {
                unsafe
                {
                    fixed (UntypedComponentLookup* handlesPtr = &lookup0)
                    {
                        var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<UntypedComponentLookup>(handlesPtr, componentTypeSizes.Length, Allocator.None);
                        #if ENABLE_UNITY_COLLECTIONS_CHECKS
                        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, AtomicSafetyHandle.GetTempMemoryHandle());
                        #endif
                        return result;
                    }
                }
            }
        }

        Span<UntypedComponentLookup> Values
        {
            get
            {
                unsafe
                {
                    fixed(UntypedComponentLookup* handlesPtr = &lookup0)
                        return new Span<UntypedComponentLookup>(handlesPtr, 10);
                }
            }
        }

        public void AddLookup(ExprSystemComponentLookupHolder holder)
        {
            if(componentTypeSizes.Length >= 10)
                throw new Exception("max supported component type count is 10");

            Values[componentTypeSizes.Length] = holder.componentLookup;
            componentTypeSizes.Add(holder.typeSize);
            componentTypeHashes.Add(holder.stableTypeHash);
        }
    }

    public static class ExpressionSystemUtility
    {
        /// <summary>
        /// Set up component dependencies, lookups for an expression graph system based on expression data
        /// </summary>
        /// <param name="state">The expression system that schedules IJobChunk jobs over instances using this expression graph</param>
        /// <param name="exprData">The baked expression graph</param>
        /// <param name="typeHandles"></param>
        /// <param name="lookups"></param>
        /// <param name="instanceComponents">List of components required for IJobChunk iteration over instances using this expression graph</param>
        /// <returns></returns>
        public static bool TryAddQueriesAndComponents(
            ref SystemState state,
            ref BlobExpressionData exprData,
            DynamicBuffer<ExprSystemTypeHandleHolder> typeHandles,
            DynamicBuffer<ExprSystemComponentLookupHolder> lookups,
            NativeList<ComponentType> instanceComponents)
        {
            exprData.RuntimeInitialize();

            ref var componentTypes = ref exprData.localComponents;

            for (int i = 0; i < componentTypes.Length; ++i)
            {
                var type = componentTypes[i].ResolveComponentType();
                if (type.TypeIndex == TypeIndex.Null)
                {
                    UnityEngine.Debug.LogError(
                        $"type with stableTypeHash={componentTypes[i].stableTypeHash} required by BehaviorTree not found");
                    return false;
                }

                instanceComponents.Add(type);
                state.AddDependency(type);

                typeHandles.Add(new ExprSystemTypeHandleHolder
                {
                    typeHandle = state.EntityManager.GetDynamicComponentTypeHandle(type),
                    typeIndex = type.TypeIndex,
                    stableTypeHash = componentTypes[i].stableTypeHash,
                    typeSize = TypeManager.GetTypeInfo(type.TypeIndex).TypeSize,
                });
            }

            ref var lookupTypes = ref exprData.lookupComponents;

            for (int i = 0; i < lookupTypes.Length; ++i)
            {
                var type = lookupTypes[i].ResolveComponentType();
                if (type.TypeIndex == TypeIndex.Null)
                {
                    UnityEngine.Debug.LogError(
                        $"type with stableTypeHash={componentTypes[i].stableTypeHash} required by BehaviorTree not found");
                    return false;
                }

                state.AddDependency(type);

                lookups.Add(new ExprSystemComponentLookupHolder
                {
                    componentLookup = state.GetUntypedComponentLookup(type.TypeIndex, true),
                    stableTypeHash = lookupTypes[i].stableTypeHash,
                    typeSize = TypeManager.GetTypeInfo(type.TypeIndex).TypeSize,
                });
            }

            return true;
        }
					
    }
}