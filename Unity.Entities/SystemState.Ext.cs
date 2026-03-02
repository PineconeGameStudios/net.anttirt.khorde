namespace Unity.Entities
{
    public static class SystemStateExt
    {
        public static void AddDependency<T>(ref this SystemState state, bool isReadOnly = false)
        {
            state.AddReaderWriter(isReadOnly ? ComponentType.ReadOnly<T>() : ComponentType.ReadWrite<T>());
        }

        public static void AddDependency(ref this SystemState state, ComponentType componentType)
        {
            state.AddReaderWriter(componentType);
        }

        //[GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleBufferElement) })]
        public static UntypedBufferLookup GetUntypedBufferLookup(ref this SystemState state, ComponentType componentType)
        {
            //CheckOnUpdate_Lookup();
            state.AddReaderWriter(componentType);
            return state.EntityManager.GetUntypedBufferLookup(componentType);
        }

        public unsafe static UntypedBufferLookup GetUntypedBufferLookup(this EntityManager entityManager, ComponentType componentType)
        {
            var access = entityManager.GetCheckedEntityDataAccess();
            var typeIndex = componentType.TypeIndex;
            var isReadOnly = componentType.AccessModeType == ComponentType.AccessMode.ReadOnly;
            ref readonly var typeInfo = ref TypeManager.GetTypeInfo(typeIndex);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &access->DependencyManager->Safety;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new UntypedBufferLookup(typeIndex, access, isReadOnly,
                safetyHandles->GetSafetyHandleForComponentLookup(typeIndex, isReadOnly),
                safetyHandles->GetBufferHandleForBufferLookup(typeIndex),
                typeInfo.BufferCapacity,
                typeInfo.ElementSize,
                typeInfo.AlignmentInBytes
                );
#else
            return new UntypedBufferLookup(typeIndex, access, isReadOnly,
                typeInfo.BufferCapacity,
                typeInfo.ElementSize,
                typeInfo.AlignmentInBytes
            );
#endif
        }
    }
}
