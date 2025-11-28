using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.AI.BT.Nodes
{
	[Serializable]
	[NodeCategory("Component")]
	public abstract class ComponentReaderNode<T> : Base, IExprNode, IComponentAccess where T : Unity.Entities.IComponentData
	{
		public Type ComponentType => typeof(T);
		public bool IsReadOnly => true;

		public override string Title => $"Read {typeof(T).Name}";

		public void Bake(ref BlobBuilder builder, ref BTExpr expr, BakingContext context)
		{
			var index = context.componentTypes.IndexOf(typeof(T));
			if(index == -1)
				throw new System.Exception($"component type {typeof(T).Name} not found in type list");

			expr.type = BTExpr.ExprType.ReadField;
			expr.data.readField = new BTExpr.ReadField
			{
				componentIndex = (byte)index,
			};

			var fields = GetFields();

			var bakedFields = builder.Allocate(ref expr.data.readField.fields, fields.Length);
			for(int i = 0; i < fields.Length; i++)
			{
				int offset = UnsafeUtility.GetFieldOffset(fields[i]);
				if(offset > ushort.MaxValue)
					throw new Exception("component too large; field offset over 65k");

				bakedFields[i] = fields[i];
			}
		}

		public static System.Reflection.FieldInfo[] GetFields() => typeof(T).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			foreach(var field in GetFields())
			{
				context.AddOutputPort(field.Name)
					.WithDisplayName(field.Name)
					.WithDataType(field.FieldType)
					.Build();
			}
		}
	}

	[Serializable]
	[NodeCategory("Component")]
	public abstract class ComponentWriterNode<T> : Base, IExecNode, IComponentAccess where T : Unity.Entities.IComponentData
	{
		public Type ComponentType => typeof(T);
		public bool IsReadOnly => false;

		public override string Title => $"Write {typeof(T).Name}";

		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context)
		{
			var componentIndex = context.componentTypes.IndexOf(typeof(T));
			if(componentIndex == -1)
				throw new System.Exception($"component type {typeof(T).Name} not found in type list");

			exec.type = BTExec.Type.WriteField;
			exec.data.writeField = new WriteField
			{
				componentIndex = (byte)componentIndex,
			};

			var fields = ComponentReaderNode<T>.GetFields();

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
						input = context.GetExprNodeRef(port),
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
			foreach(var field in ComponentReaderNode<T>.GetFields())
			{
				context.AddOption<bool>("w_" + field.Name)
					.WithDisplayName(field.Name)
					.Build();
			}
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(Base.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			var fields = ComponentReaderNode<T>.GetFields();

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

	[Serializable] internal class ReadLocalTransform : ComponentReaderNode<Unity.Transforms.LocalTransform> { }
	[Serializable] internal class WriteLocalTransform : ComponentWriterNode<Unity.Transforms.LocalTransform> { }
}