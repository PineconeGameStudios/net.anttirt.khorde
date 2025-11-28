using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.AI.BT.Nodes
{
	[Serializable]
	[NodeCategory("Execution")]
	internal class Root : Base, IExecNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context)
		{
			exec.SetData(new BT.Root { child = context.GetTargetNodeId(GetOutputPort(0)) });
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();
		}

		public override void OnEnable()
		{
			base.OnEnable();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class Sequence : Base, IExecNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context)
		{
			exec.type = BTExec.Type.Sequence;
			exec.data.sequence = new BT.Sequence { };
			var outputPorts = builder.Allocate(ref exec.data.sequence.children, outputPortCount);
			for(int i = 0; i < outputPorts.Length; ++i)
				outputPorts[i] = context.GetTargetNodeId(GetOutputPort(i));
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
						.WithPortCapacity(PortCapacity.Single)
						.Build();
				}
			}

			context.AddInputPort<Exec>(Base.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
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
				outputPorts[i].nodeId = context.GetTargetNodeId(option.GetOutputPort(0));
				outputPorts[i].condition = context.GetExprNodeRef(option.GetInputPort(0));
			}
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(Base.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	[UseWithContext(typeof(Selector))]
	internal class SubTreeOption : BlockNode
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<bool>("Condition")
				.WithDisplayName("Condition")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			context.AddOutputPort<Exec>(Base.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class Optional : Base, IExecNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context)
		{
			exec.type = BTExec.Type.Optional;
			exec.data.optional = new BT.Optional
			{
				child = context.GetTargetNodeId(GetOutputPort(0)),
				condition = context.GetExprNodeRef(GetInputPort(1)),
			};
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			context.AddInputPort<bool>("Condition")
				.WithDisplayName("Condition")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			context.AddOutputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
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
				.WithPortCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class Catch : Base, IExecNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context)
		{
			exec.type = BTExec.Type.Catch;
			exec.data.@catch = new BT.Catch
			{
				child = context.GetTargetNodeId(GetOutputPort(0)),
			};
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			context.AddOutputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class Wait : Base, IExecNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context)
		{
			exec.type = BTExec.Type.Wait;
			exec.data.wait = new BT.Wait
			{
				until = context.GetExprNodeRef(GetInputPort(1)),
			};
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			context.AddInputPort<bool>("Until")
				.WithDisplayName("Until")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Single)
				.Build();
		}
	}

}