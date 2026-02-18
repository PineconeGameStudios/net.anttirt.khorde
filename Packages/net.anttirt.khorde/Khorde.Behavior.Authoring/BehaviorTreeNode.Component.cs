using Khorde.Expr.Authoring;
using System;
using Khorde.Expr;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Khorde.Behavior.Authoring
{
	[Serializable]
	[NodeCategory("Component")]
	public abstract class ComponentWriterNode<T> : ExecBase, IComponentAccess where T : unmanaged, Unity.Entities.IComponentData
	{
		public ComponentType ComponentType => new ComponentType(typeof(T), ComponentType.AccessMode.ReadWrite);
		public bool IsReadOnly => false;

		public override string Title => $"Write {typeof(T).Name}";

		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context)
		{
			var componentIndex = context.LocalComponents.FindIndex(kv => kv.GetManagedType() == typeof(T));
			if(componentIndex == -1)
				throw new System.Exception($"component type {typeof(T).Name} not found in type list");

			exec.type = BTExec.BTExecType.WriteField;
			exec.data.writeField = new WriteField
			{
				componentIndex = (byte)componentIndex,
			};

			var fields = BlobExpressionData.GetComponentFields<T>();

			int index = 0;
			int enabledFieldCount = 0;
			foreach(var field in fields)
			{
				GetNodeOption(index).TryGetValue<bool>(out var enabled);

				if(enabled)
					++enabledFieldCount;

				++index;
			}

			var blobFields = builder.Allocate(ref exec.data.writeField.fields, enabledFieldCount);

			index = 0;
			enabledFieldCount = 0;

			foreach(var field in fields)
			{
				GetNodeOption(index).TryGetValue<bool>(out var enabled);

				if(enabled)
				{
					int offset = UnsafeUtility.GetFieldOffset(fields[index]);
					if(offset > ushort.MaxValue)
						throw new Exception("component too large; field offset over 65k");

					var port = GetInputPort(enabledFieldCount + 1);

					var bakedField = new WriteField.Field
					{
						input = context.GetExpressionRef(port),
						offset = (ushort)offset,
						size = (ushort)UnsafeUtility.SizeOf(field.FieldType),
					};

					blobFields[enabledFieldCount] = bakedField;

					++enabledFieldCount;
				}

				++index;
			}
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			foreach(var field in BlobExpressionData.GetComponentFields<T>())
			{
				context.AddOption<bool>("w_" + field.Name)
					.WithDisplayName(field.Name)
					.Build();
			}
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(ExecBase.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			var fields = BlobExpressionData.GetComponentFields<T>();

			int index = 0;
			foreach(var field in fields)
			{
				GetNodeOption(index).TryGetValue<bool>(out var enabled);

				if(enabled)
				{
					context.AddInputPort(field.Name)
						.WithDisplayName(field.Name)
						.WithDataType(field.FieldType)
						.WithPortCapacity(PortCapacity.Single)
						.Build();
				}

				index++;
			}
		}
	}

	[Serializable] internal class WriteLocalTransform : ComponentWriterNode<Unity.Transforms.LocalTransform> { }
}