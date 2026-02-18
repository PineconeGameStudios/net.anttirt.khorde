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
	}
}
