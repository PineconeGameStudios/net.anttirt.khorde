using Khorde.Expr;
using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Khorde.Behavior.Authoring
{
	[Serializable]
	[NodeCategory("Execution")]
	internal class Root : ExecBase, IExecNode
	{
		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex)
		{
			exec.SetData(new Behavior.Root { child = context.GetTargetNodeId(GetOutputPort(0)) });
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
	internal class Sequence : ExecBase, IExecNode
	{
		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex)
		{
			exec.type = BTExec.BTExecType.Sequence;
			exec.data.sequence = new Behavior.Sequence { };
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
					context.AddOutputPort<Exec>(ExecBase.EXEC_PORT_DEFAULT_NAME + "_" + i.ToString())
						.WithDisplayName(string.Empty)
						.WithConnectorUI(PortConnectorUI.Arrowhead)
						.WithPortCapacity(PortCapacity.Single)
						.Build();
				}
			}

			context.AddInputPort<Exec>(ExecBase.EXEC_PORT_DEFAULT_NAME)
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
		public int NodeCount => 1;

		public void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex)
		{
			exec.type = BTExec.BTExecType.Selector;
			exec.data.selector = new Behavior.Selector { };

			var outputPorts = builder.Allocate(ref exec.data.selector.children, blockCount);
			for(int i = 0; i < outputPorts.Length; ++i)
			{
				var option = (SubTreeOption)GetBlock(i);
				outputPorts[i].nodeId = context.GetTargetNodeId(option.GetOutputPort(0));
				outputPorts[i].condition = context.GetExpressionRef(option.GetInputPort(0));
			}
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(ExecBase.EXEC_PORT_DEFAULT_NAME)
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

			context.AddOutputPort<Exec>(ExecBase.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class Optional : ExecBase, IExecNode
	{
		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex)
		{
			exec.type = BTExec.BTExecType.Optional;
			exec.data.optional = new Behavior.Optional
			{
				child = context.GetTargetNodeId(GetOutputPort(0)),
				condition = context.GetExpressionRef(GetInputPort(1)),
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
	internal class Fail : ExecBase, IExecNode
	{
		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex)
		{
			exec.type = BTExec.BTExecType.Fail;
			exec.data.fail = new Behavior.Fail { };
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
	internal class Catch : ExecBase, IExecNode
	{
		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex)
		{
			exec.type = BTExec.BTExecType.Catch;
			exec.data.@catch = new Behavior.Catch
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
	internal class Wait : ExecBase, IExecNode
	{
		enum WaitMode
		{
			Condition,
			Duration,
		}

		INodeOption waitModeOption;
		IPort untilInputPort;
		IPort durationInputPort;

		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex)
		{
			exec.type = BTExec.BTExecType.Wait;

			waitModeOption.TryGetValue<WaitMode>(out var waitMode);

			if(waitMode == WaitMode.Condition)
			{
				exec.data.wait = new Behavior.Wait
				{
					until = context.GetExpressionRef(untilInputPort),
				};
			}
			else
			{
				exec.data.wait = new Behavior.Wait
				{
					duration = context.GetExpressionRef(durationInputPort),
				};
			}
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			waitModeOption = context.AddOption<WaitMode>("waitMode")
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			waitModeOption.TryGetValue<WaitMode>(out var waitMode);

			if(waitMode == WaitMode.Condition)
			{
				untilInputPort = context.AddInputPort<bool>("Until")
					.WithDisplayName("Until")
					.WithConnectorUI(PortConnectorUI.Circle)
					.WithPortCapacity(PortCapacity.Single)
					.Build();
			}
			else
			{
				durationInputPort = context.AddInputPort<float>("Duration")
					.WithDisplayName("Duration")
					.WithConnectorUI(PortConnectorUI.Circle)
					.WithPortCapacity(PortCapacity.Single)
					.Build();
			}
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class WriteVar : ExecBase, IExecNode
	{
		private INodeOption valueTypeOption;
		private IPort varPort;
		private IPort valuePort;

		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex)
		{
			int varIndex = context.GetVariableIndex(((IVariableNode)(varPort.firstConnectedPort.GetNode())).variable);
			exec.type = BTExec.BTExecType.WriteVar;
			exec.data.writeVar = new Behavior.WriteVar
			{
				variableIndex = varIndex,
				input = context.GetExpressionRef(valuePort),
			};
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			valueTypeOption = context.AddOption<ExpressionValueType>("ValueType")
				.WithDisplayName(string.Empty)
				.WithDefaultValue(ExpressionValueType.Int)
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			valueTypeOption.TryGetValue<ExpressionValueType>(out var valueType);
			var type = valueType.GetValueType();

			if(type == null)
				return;

			varPort = context.AddInputPort("Variable")
				.WithDisplayName("Variable")
				.WithDataType(type)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			valuePort = context.AddInputPort("Value")
				.WithDisplayName("Value")
				.WithDataType(type)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class Parallel : ExecBase, IExecNode
	{
		private IPort main;
		private IPort parallel;
		private INodeOption loop;

		public override int NodeCount => 2;

		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex)
		{
			switch(nodeIndex)
			{
			case 0:
				var mainNode = context.GetTargetNodeId(main);
				var threadRootNode = mainNode;
				// when a graph node generates multiple exec nodes, they have sequential ids
				threadRootNode.index += 1;
				exec.type = BTExec.BTExecType.Parallel;
				exec.data.parallel = new Behavior.Parallel
				{
					main = mainNode,
					parallel = threadRootNode,
				};
				break;

			case 1:
				this.loop.TryGetValue<bool>(out var loop);
				exec.type = BTExec.BTExecType.ThreadRoot;
				exec.data.threadRoot = new Behavior.ThreadRoot
				{
					child = context.GetTargetNodeId(parallel),
					loop = loop,
				};
				break;

			default:
				break;
			}
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			loop = context.AddOption<bool>("loop")
				.WithDisplayName("Loop")
				.WithTooltip("Loop the parallel branch until the main branch is complete")
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			main = context.AddOutputPort<Exec>("main")
				.WithDisplayName("Main")
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			parallel = context.AddOutputPort<Exec>("parallel")
				.WithDisplayName("Parallel")
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();
		}
	}
}