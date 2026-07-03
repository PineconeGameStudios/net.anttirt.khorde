using Khorde.Expr;
using Khorde.Expr.Authoring;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace Khorde.Behavior.Authoring
{
	[Serializable]
	[NodeCategory("Execution")]
	internal class Root : ExecBase, IExecNode
	{
		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			exec.SetData(new Behavior.Root { child = context.GetTargetNodeId(GetOutputPort(0)) });
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
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
		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			exec.type = BTExec.BTExecType.Sequence;
			exec.data.sequence = new Behavior.Sequence { };
			var outputPorts = builder.Allocate(ref exec.data.sequence.children, OutputPortCount);
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
					context.AddOutputPort<ExecutionFlow>(ExecBase.EXEC_PORT_DEFAULT_NAME + "_" + i.ToString())
						.WithDisplayName(string.Empty)
						.WithConnectorUI(PortConnectorUI.Arrowhead)
						.WithCapacity(PortCapacity.Single)
						.Build();
				}
			}

			context.AddInputPort<ExecutionFlow>(ExecBase.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class Selector : ContextNode, IExecNode
	{
		public int NodeCount => 1;

		public void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			exec.type = BTExec.BTExecType.Selector;
			exec.data.selector = new Behavior.Selector { };

			var outputPorts = builder.Allocate(ref exec.data.selector.children, BlockCount);
			for(int i = 0; i < outputPorts.Length; ++i)
			{
				var option = (SubTreeOption)GetBlock(i);
				outputPorts[i].nodeId = context.GetTargetNodeId(option.GetOutputPort(0));
				outputPorts[i].condition = context.GetExpressionRef(option.GetInputPort(0));
			}
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<ExecutionFlow>(ExecBase.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
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
				.WithCapacity(PortCapacity.Single)
				.WithDefaultValue(true)
				.Build();

			context.AddOutputPort<ExecutionFlow>(ExecBase.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class Optional : ExecBase, IExecNode
	{
		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
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
			context.AddInputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			context.AddInputPort<bool>("Condition")
				.WithDisplayName("Condition")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Single)
				.Build();

			context.AddOutputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class Fail : ExecBase, IExecNode
	{
		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			exec.type = BTExec.BTExecType.Fail;
			exec.data.fail = new Behavior.Fail { };
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class Catch : ExecBase, IExecNode
	{
		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			exec.type = BTExec.BTExecType.Catch;
			exec.data.@catch = new Behavior.Catch
			{
				child = context.GetTargetNodeId(GetOutputPort(0)),
			};
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			context.AddOutputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class Wait : ExecBase, IExecNode
	{
		INodeOption timeoutOption;
		INodeOption conditionModeOption;
		IPort conditionInputPort;
		IPort durationInputPort;

		enum ConditionMode : int { Until, While, }

		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			exec.type = BTExec.BTExecType.Wait;

			timeoutOption.TryGetValue<bool>(out var timeout);
			conditionModeOption.TryGetValue<ConditionMode>(out var conditionMode);

			exec.data.wait = new Behavior.Wait
			{
				mode = (Behavior.Wait.ConditionMode)conditionMode,
				condition = context.GetExpressionRef(conditionInputPort),
			};

			if(timeout)
			{
				exec.data.wait.duration = context.GetExpressionRef(durationInputPort);
			}
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			timeoutOption = context.AddOption<bool>("timeout")
				.WithDisplayName("Timeout")
				.Build();

			conditionModeOption = context.AddOption<ConditionMode>("conditionMode")
				.WithDisplayName("Condition")
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			conditionModeOption.TryGetValue<ConditionMode>(out var conditionMode);

			conditionInputPort = context.AddInputPort<bool>("Until")
				.WithDisplayName(conditionMode.ToString())
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Single)
				.Build();

			timeoutOption.TryGetValue<bool>(out var timeout);

			if(timeout)
			{
				durationInputPort = context.AddInputPort<float>("Duration")
					.WithDisplayName("Duration")
					.WithConnectorUI(PortConnectorUI.Circle)
					.WithCapacity(PortCapacity.Single)
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

		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			if(varPort.FirstConnectedPort?.GetNode() is not IVariableNode varNode)
			{
				context.AddError(this, "variable port must be connected directly to a variable");
				return;
			}

			var varIndex = context.GetVariableIndex(varNode.Variable);
			exec.type = BTExec.BTExecType.WriteVar;
			exec.data.writeVar = new Behavior.WriteVar
			{
				variable = varIndex,
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
			context.AddInputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			valueTypeOption.TryGetValue<ExpressionValueType>(out var valueType);
			var type = valueType.GetValueType();

			if(type == null)
				return;

			varPort = context.AddInputPort("Variable")
				.WithDisplayName("Variable")
				.WithDataType(type)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			valuePort = context.AddInputPort("Value")
				.WithDisplayName("Value")
				.WithDataType(type)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Single)
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

		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			switch(nodeIndex)
			{
				case 0:
					var mainNode = context.GetTargetNodeId(main);
					var threadRootNode = nodeId;
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
			context.AddInputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			main = context.AddOutputPort<ExecutionFlow>("main")
				.WithDisplayName("Main")
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			parallel = context.AddOutputPort<ExecutionFlow>("parallel")
				.WithDisplayName("Parallel")
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class Repeat : ExecBase, IExecNode, ICustomExprNode
	{
		private INodeOption infiniteOption;
		private IPort child;
		private IPort parameter;
		private IPort counter;
		private VariableId counterVariableIndex;

		void IExecNode.Register(BTBakingContext context, BTExecNodeId nodeId)
		{
			counterVariableIndex = context.RegisterGeneratedVariable(this, 0, $"_Repeat_{nodeId.index}_counter", true, typeof(int));
		}

		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			infiniteOption.TryGetValue<RepeatMode>(out var mode);

			exec.type = BTExec.BTExecType.Repeat;
			exec.data.repeat = new Behavior.Repeat
			{
				child = context.GetTargetNodeId(child),
				param = parameter == null ? default : context.GetExpressionRef(parameter),
				mode = mode,
				counter = counterVariableIndex,
			};

			context.BakeGeneratedVariable(this, 0, counterVariableIndex);
		}

		public ExpressionRef GetExpressionRef(GraphExpressionBakingContext context, IPort port)
		{
			if(port == counter)
			{
				return context.GetGeneratedVariableNodeRef(this, 0);
			}

			context.AddError(this, $"port doesn't match");
			return default;
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			infiniteOption = context.AddOption<RepeatMode>("Mode")
				.WithDisplayName("Mode")
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			infiniteOption.TryGetValue<RepeatMode>(out var mode);

			context.AddInputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			child = context.AddOutputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			switch(mode)
			{
				case RepeatMode.Count:
					parameter = context.AddInputPort<int>("CountParameter")
						.WithConnectorUI(PortConnectorUI.Circle)
						.WithCapacity(PortCapacity.Single)
						.WithDisplayName("Count")
						.Build();

					break;

				case RepeatMode.Infinite:
					parameter = null;

					break;

				case RepeatMode.Condition:
					parameter = context.AddInputPort<bool>("ConditionParameter")
						.WithConnectorUI(PortConnectorUI.Circle)
						.WithCapacity(PortCapacity.Single)
						.WithDisplayName("Condition")
						.Build();

					break;

				default:
					break;
			}

			counter = context.AddOutputPort<int>("LoopCounter")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Multi)
				.WithDisplayName("Counter")
				.Build();
		}
	}

	[Serializable]
	[NodeCategory("Execution")]
	internal class Invoke : ExecBase, IExecNode
	{
		INodeOption action;
		INodeOption blocking;
		int actionIndex;
		List<IPort> paramPorts = new();

		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			this.blocking.TryGetValue<bool>(out var blocking);

			exec.type = BTExec.BTExecType.Invoke;
			exec.data.invoke = new Behavior.Invoke { actionIndex = actionIndex, blocking = blocking };

			if(action.TryGetValue<BehaviorTreeAction>(out var asset) && asset != null)
			{
				using(var so = new SerializedObject(asset))
				{
					var layout = BehaviorTreeActionPostProcessor.GetLayout(asset, so);
					var parameters = builder.Allocate(ref exec.data.invoke.parameters, paramPorts.Count);
					for(int i = 0; i < paramPorts.Count; ++i)
					{
						parameters[i].offset = layout[i].offset;
						parameters[i].size = layout[i].size;
						parameters[i].expr = context.GetExpressionRef(paramPorts[i]);
					}
				}
			}
		}

		void IExecNode.Register(BTBakingContext context, BTExecNodeId nodeId)
		{
			action.TryGetValue<BehaviorTreeAction>(out var asset);

			int index = context.Actions.IndexOf(asset);
			if(index == -1)
			{
				index = context.Actions.Count;
				context.Actions.Add(asset);
			}

			actionIndex = index;
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			action = context.AddOption<BehaviorTreeAction>("Action")
				.WithDisplayName("Action")
				.WithTooltip("Action asset to execute")
				.Build();

			blocking = context.AddOption<bool>("Blocking")
				.WithDisplayName("Blocking")
				.WithTooltip("Whether to wait until the action has executed before resuming BT execution")
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			paramPorts.Clear();

			if(action.TryGetValue<BehaviorTreeAction>(out var actionAsset) && actionAsset != null)
			{
				using(var so = new SerializedObject(actionAsset))
				{
					var layout = BehaviorTreeActionPostProcessor.GetLayout(actionAsset, so);
					foreach(var p in layout)
					{
						paramPorts.Add(context.AddInputPort(p.name)
							.WithDisplayName(ObjectNames.NicifyVariableName(p.name))
							.WithDataType(p.type)
							.WithCapacity(PortCapacity.Single)
							.Build());
					}
				}
			}
		}
	}
}
