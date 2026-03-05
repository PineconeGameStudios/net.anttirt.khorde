using Khorde.Entities;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.LowLevel.Unsafe;

namespace Khorde.Expr
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

        public bool isBuffer;
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
        const int kMaxHandles = 10;

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

        // these are initialized per chunk
        [NativeDisableContainerSafetyRestriction] UnsafeUntypedBufferAccessor bufferAccessor0;
        [NativeDisableContainerSafetyRestriction] UnsafeUntypedBufferAccessor bufferAccessor1;
        [NativeDisableContainerSafetyRestriction] UnsafeUntypedBufferAccessor bufferAccessor2;
        [NativeDisableContainerSafetyRestriction] UnsafeUntypedBufferAccessor bufferAccessor3;
        [NativeDisableContainerSafetyRestriction] UnsafeUntypedBufferAccessor bufferAccessor4;
        [NativeDisableContainerSafetyRestriction] UnsafeUntypedBufferAccessor bufferAccessor5;
        [NativeDisableContainerSafetyRestriction] UnsafeUntypedBufferAccessor bufferAccessor6;
        [NativeDisableContainerSafetyRestriction] UnsafeUntypedBufferAccessor bufferAccessor7;
        [NativeDisableContainerSafetyRestriction] UnsafeUntypedBufferAccessor bufferAccessor8;
        [NativeDisableContainerSafetyRestriction] UnsafeUntypedBufferAccessor bufferAccessor9;

        // these are initialized per entity where relevant
        [NativeDisableContainerSafetyRestriction] UntypedDynamicBuffer bufferTemp0;
        [NativeDisableContainerSafetyRestriction] UntypedDynamicBuffer bufferTemp1;
        [NativeDisableContainerSafetyRestriction] UntypedDynamicBuffer bufferTemp2;
        [NativeDisableContainerSafetyRestriction] UntypedDynamicBuffer bufferTemp3;
        [NativeDisableContainerSafetyRestriction] UntypedDynamicBuffer bufferTemp4;
        [NativeDisableContainerSafetyRestriction] UntypedDynamicBuffer bufferTemp5;
        [NativeDisableContainerSafetyRestriction] UntypedDynamicBuffer bufferTemp6;
        [NativeDisableContainerSafetyRestriction] UntypedDynamicBuffer bufferTemp7;
        [NativeDisableContainerSafetyRestriction] UntypedDynamicBuffer bufferTemp8;
        [NativeDisableContainerSafetyRestriction] UntypedDynamicBuffer bufferTemp9;

        public int Length => components.Length;

        public int GetTypeSize(int index) => components[index].typeSize;

        Span<UntypedDynamicBuffer> BufferTemps
        {
            get
            {
                unsafe
                {
                    fixed(UntypedDynamicBuffer* ptr = &bufferTemp0)
                        return new Span<UntypedDynamicBuffer>(ptr, kMaxHandles);
                }
            }
        }

        Span<UnsafeUntypedBufferAccessor> BufferAccessors
        {
            get
            {
                unsafe
                {
                    fixed(UnsafeUntypedBufferAccessor* ptr = &bufferAccessor0)
                        return new Span<UnsafeUntypedBufferAccessor>(ptr, kMaxHandles);
                }
            }
        }

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
            {
                if(components.ElementAt(i).isBuffer)
                {
                    unsafe
                    {
                        BufferTemps[i] = BufferAccessors[i].GetUntypedDynamicBuffer(entityIndex);
                        ref UntypedDynamicBuffer buf = ref BufferTemps[i];
                        fixed(void* pbuf = &buf)
                            components.ElementAt(i).data = (IntPtr)pbuf;
                    }
                }
                else
                {
                    components.ElementAt(i).data = basePointers[i] + entityIndex * components[i].typeSize;
                }
            }

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

                if(components[i].isBuffer)
                {
                    BufferAccessors[i] = chunk.GetUntypedBufferAccessor(ref handle);
                }
                else
                {
                    var data = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref handle, GetTypeSize(i));

                    unsafe
                    {
                        basePointers[i] = handle.IsReadOnly ? (IntPtr)data.GetUnsafeReadOnlyPtr() : (IntPtr)data.GetUnsafePtr();
                    }
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
                isBuffer = holder.isBuffer,
            });
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ExprJobComponentLookups
    {
        // These safety disables are here for a few reasons:

        // The safety system doesn't allow having both a ComponentTypeHandle
        // and a lookup for the same type in the same job, so one of them needs
        // to have safety disabled. It's not immediately clear how to restore
        // at least some safety checks in this case, but it might be possible.

        // We want to allow writing to looked-up components when we know
        // they're on entities not related to the main BT entities, but this
        // can't be statically verified. When a BT has lookup writes, it should
        // default to sequential execution and have a graph option to allow
        // unsafe parallel execution. We'll still need at least
        // [NativeDisableParallelForRestriction] here, regardless.

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
        public static bool TryAddQueriesAndComponents<TTypeHandles, TLookups>(
            ref SystemState state,
            ref BlobExpressionData exprData,
            ref TTypeHandles typeHandles,
            ref TLookups lookups,
            NativeList<ComponentType> instanceComponents)
            where TTypeHandles : INativeList<ExprSystemTypeHandleHolder>
            where TLookups : INativeList<ExprSystemComponentLookupHolder>
        {
            exprData.RuntimeInitialize(state.WorldUnmanaged);

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

                if (!instanceComponents.IsCreated)
                {
                    UnityEngine.Debug.LogError($"expression graph references local component {TypeManager.GetTypeInfo(type.TypeIndex).DebugTypeName} but local components are not allowed");
                    continue;
                }

                instanceComponents.Add(type);

                ref readonly var typeInfo = ref TypeManager.GetTypeInfo(type.TypeIndex);

                typeHandles.Length++;
                typeHandles[^1] = new ExprSystemTypeHandleHolder
                {
                    typeHandle = state.GetDynamicComponentTypeHandle(type),
                    typeIndex = type.TypeIndex,
                    stableTypeHash = componentTypes[i].stableTypeHash,
                    typeSize = typeInfo.TypeSize,
                    isBuffer = typeInfo.Category == TypeManager.TypeCategory.BufferData,
                };
            }

            if (componentTypes.Length > 0 && !instanceComponents.IsCreated)
                return false;

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

                lookups.Length++;
                lookups[^1] = new ExprSystemComponentLookupHolder
                {
                    componentLookup = state.GetUntypedComponentLookup(type.TypeIndex, isReadOnly: type.AccessModeType == ComponentType.AccessMode.ReadOnly),
                    stableTypeHash = lookupTypes[i].stableTypeHash,
                    typeSize = TypeManager.GetTypeInfo(type.TypeIndex).TypeSize,
                };
            }

            return true;
        }
                    
    }
}