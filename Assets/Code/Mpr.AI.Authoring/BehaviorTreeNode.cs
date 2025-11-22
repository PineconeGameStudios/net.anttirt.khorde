using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using UnityEngine.Pool;

namespace Mpr.AI.BT.Nodes
{
	[Serializable]
	[UseWithGraph(typeof(BehaviorTreeGraph))]
	public abstract class Base : Node
	{
		public const string EXEC_PORT_DEFAULT_NAME = "Execution";
	}

	/// <summary>
	/// Marker type for ports of type Execution
	/// </summary>
	[Serializable]
	public class Exec { }

	public class BakingContext
	{
		public Dictionary<INode, BTExecNodeId> execNodeMap; 
		public Dictionary<INode, BTExprNodeRef> exprNodeMap; 
		public NativeList<byte> constStorage; 
		public List<Type> componentTypes; 
		public List<string> errors;

		public BakingContext(Dictionary<INode, BTExecNodeId> execNodeMap, Dictionary<INode, BTExprNodeRef> exprNodeMap, NativeList<byte> constStorage, List<Type> componentTypes, List<string> errors)
		{
			this.execNodeMap = execNodeMap ?? throw new ArgumentNullException(nameof(execNodeMap));
			this.exprNodeMap = exprNodeMap ?? throw new ArgumentNullException(nameof(exprNodeMap));
			this.constStorage = constStorage;
			this.componentTypes = componentTypes ?? throw new ArgumentNullException(nameof(componentTypes));
			this.errors = errors ?? throw new ArgumentNullException(nameof(errors));
		}
	}

	public interface IExecNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context);
	}

	public interface IExprNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExpr expr, BakingContext context);
	}

	[Serializable]
	internal class Root : Base, IExecNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context)
		{
			exec.SetData(new BT.Root { child = GetOutputPort(0).GetTargetNodeId(context) });
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();
		}
	}

	[Serializable]
	[UseWithGraph(typeof(BehaviorTreeGraph))]
	internal class Sequence : Base, IExecNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context)
		{
			exec.type = BTExec.Type.Sequence;
			exec.data.sequence = new BT.Sequence { };
			var outputPorts = builder.Allocate(ref exec.data.sequence.children, outputPortCount);
			for(int i = 0; i < outputPorts.Length; ++i)
				outputPorts[i] = GetOutputPort(i).GetTargetNodeId(context);
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			context.AddOption<int>("ChildCount")
				.WithDisplayName("Child Count")
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			if(GetNodeOptionByName("ChildCount").TryGetValue(out int childCount))
			{
				for(int i = 0; i < childCount; i++)
				{
					context.AddOutputPort<Exec>(Base.EXEC_PORT_DEFAULT_NAME + "_" + i.ToString())
						.WithDisplayName(string.Empty)
						.WithConnectorUI(PortConnectorUI.Arrowhead)
						.Build();
				}
			}

			context.AddInputPort<Exec>(Base.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();
		}
	}

	[Serializable]
	[UseWithGraph(typeof(BehaviorTreeGraph))]
	internal class Selector : ContextNode, IExecNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context)
		{
			exec.type = BTExec.Type.Selector;
			exec.data.selector = new BT.Selector { };

			var outputPorts = builder.Allocate(ref exec.data.selector.children, blockCount);
			for(int i = 0; i < outputPorts.Length; ++i)
			{
				var option = (SubTreeOption)GetBlock(i);
				outputPorts[i].nodeId = option.GetOutputPort(0).GetTargetNodeId(context);
				outputPorts[i].condition = option.GetInputPort(0).GetExprNodeRef(context);
			}
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(Base.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();
		}
	}

	[Serializable]
	[UseWithContext(typeof(Selector))]
	internal class SubTreeOption : BlockNode
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<bool>("Condition")
				.WithDisplayName("Condition")
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddOutputPort<Exec>(Base.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();
		}
	}

	[Serializable]
	internal class Optional : Base, IExecNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context)
		{
			exec.type = BTExec.Type.Optional;
			exec.data.optional = new BT.Optional
			{
				child = GetOutputPort(0).GetTargetNodeId(context),
				condition = GetInputPort(1).GetExprNodeRef(context),
			};
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();

			context.AddInputPort<bool>("Condition")
				.WithDisplayName("Condition")
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddOutputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();
		}
	}

	[Serializable]
	internal class Fail : Base, IExecNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context)
		{
			exec.type = BTExec.Type.Fail;
			exec.data.fail = new BT.Fail { };
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();
		}
	}

	[Serializable]
	internal class Catch : Base, IExecNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context)
		{
			exec.type = BTExec.Type.Catch;
			exec.data.@catch = new BT.Catch
			{
				child = GetOutputPort(0).GetTargetNodeId(context),
			};
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();

			context.AddOutputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();
		}
	}

	[Serializable]
	internal class Wait : Base, IExecNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context)
		{
			exec.type = BTExec.Type.Wait;
			exec.data.wait = new BT.Wait
			{
				until = GetInputPort(1).GetExprNodeRef(context),
			};
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();

			context.AddInputPort<bool>("Until")
				.WithDisplayName("Until")
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}

	[Serializable]
	public abstract class ComponentReaderNode<T> : Base, IExprNode where T : Unity.Entities.IComponentData
	{
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

			var offsets = builder.Allocate(ref expr.data.readField.offsets, fields.Length);
			for(int i = 0; i < fields.Length; i++)
			{
				int offset = UnsafeUtility.GetFieldOffset(fields[i]);
				if(offset > ushort.MaxValue)
					throw new Exception("component too large; field offset over 65k");
				offsets[i] = (ushort)offset;
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
	public abstract class ComponentWriterNode<T> : Base, IExecNode where T : Unity.Entities.IComponentData
	{
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

					var port = GetInputPort(enabledFieldCount);

					blobFields[enabledFieldCount] = new WriteField.Field
					{
						input = port.GetExprNodeRef(context),
						offset = (ushort)offset,
						size = (ushort)UnsafeUtility.SizeOf(field.FieldType),
					};

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
						.Build();
				}

				index++;
			}
		}
	}

	[Serializable] internal class ReadLocalTransform : ComponentReaderNode<Unity.Transforms.LocalTransform> { }
	[Serializable] internal class WriteLocalTransform : ComponentWriterNode<Unity.Transforms.LocalTransform> { }
}