using Khorde.Blobs;
using Khorde.Expr;
using Khorde.Expr.Authoring;
using System;
using System.Linq;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Khorde.Behavior.Authoring
{
	/// <summary>
	/// Root of the utility system. Queries actions for utility and selects the one with most utility.
	/// </summary>
	[Serializable]
	[Node("Execution/Utility", iconPath: null, title: "Utility Selector")]
	internal class UtilitySelector : ContextNode, IExecNode, IUtilityNode
	{
		private INodeOption tieScoreTolerance;
		private INodeOption tieBreakMode;

		public int NodeCount => 1;

		public void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			exec.type = BTExec.BTExecType.UtilitySelector;
			float tieTolerance = 0;
			tieScoreTolerance?.TryGetValue(out tieTolerance);
			exec.data.utilitySelector.tieTolerance = (half)tieTolerance;
			tieBreakMode?.TryGetValue(out exec.data.utilitySelector.tieBreakMode);

			var blocks = builder.Allocate(ref exec.data.utilitySelector.actions, BlockCount);

			for(int i = 0; i < BlockCount; ++i)
			{
				var src = (UtilityAction)GetBlock(i);
				ref var dst = ref blocks[i];

				dst.nodeId = context.GetTargetNodeId(src.Action);
			}

			// TODO: optimization: only select queries that are downstream from this node
			// TODO: optimization: only select queries that can contribute to utility (i.e. not in a ZeroUtility subtree)
			var allQueries = context.execNodeMap.Where(kv => kv.Key.node is Query).ToArray();
			var queries = builder.Allocate(ref exec.data.utilitySelector.queries, allQueries.Length);
			for(int i = 0; i < allQueries.Length; ++i)
				queries[i] = allQueries[i].Value;
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			tieScoreTolerance = context.AddOption<float>("TieScoreTolerance")
				.WithDisplayName("Tie Score Tolerance")
				.WithDefaultValue(0f)
				.WithTooltip(
					"Combined utility scores within this distance from the best score are consider to be tied with the best score.\n\n" +
					"For example if the tolerance is 0.2, and three actions score at 0.75, 0.65 and 0.4, then the first two actions are " +
					"considered a tie, and one of the two will be selected based on the Tie Break Mode."
				)
				.Build();

			tieBreakMode = context.AddOption<Behavior.UtilitySelector.TieBreakMode>("TieBreakMode")
				.WithDisplayName("Tie Break Mode")
				.WithTooltip(
					"The mode for selecting an action when multiple actions are tied for utility.\n\n" +
					"Top: The topmost action is selected.\n" +
					"Random: A random action is selected."
				)
				.WithDefaultValue(Behavior.UtilitySelector.TieBreakMode.Top)
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<ExecutionFlow>(ExecBase.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			context.AddUtilityDebugDisplayPort(out utilityPort);
		}

		IPort utilityPort;
		IPort IUtilityNode.GetUtilityDebugPort() => utilityPort;
	}

	/// <summary>
	/// An action performed from a Utility Selector context. The action with the highest utility score is selected, within tolerance.
	/// </summary>
	[Serializable]
	[Node("Execution/Utility", iconPath: null, title: "Action")]
	[UseWithContext(typeof(UtilitySelector))]
	internal class UtilityAction : BlockNode, IUtilityNode
	{
		public IPort Action { get; private set; }

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			// is there value in getting this value from an input?
			// parameterization?
			// context.AddOption<float>("Cooldown")
			// 	.WithDisplayName("Cooldown Time")
			// 	.WithDefaultValue(0.0f)
			// 	.Build();

			// context.AddOption<AnimationCurve>("Utility_Cooldown")
			// 	.WithDisplayName("Cooldown Utility")
			// 	.WithDefaultValue(DiracUtility)
			// 	.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			this.Action = context.AddOutputPort<ExecutionFlow>("Action")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			context.AddUtilityDebugDisplayPort(out utilityPort);
		}
		IPort utilityPort;
		IPort IUtilityNode.GetUtilityDebugPort() => utilityPort;
	}

	/// <summary>
	/// Multiplies utility according to a cooldown. The default curve is a hard cooldown.
	/// </summary>
	[Serializable]
	[Node("Execution/Utility", iconPath: "Packages/net.anttirt.khorde/Icons/Utility.png", title: "Utility (Cooldown)", stylesheet: "Packages/net.anttirt.khorde/Styles/Utility.uss")]
	internal class UtilityCooldown : ExecBase, IExecNode, ICustomExprNode, IUtilityNode
	{
		private INodeOption cooldown;
		private INodeOption softCooldown;
		private INodeOption softCooldownCurve;
		private VariableId execTimeVariableIndex;

		void IExecNode.Register(BTBakingContext context, BTExecNodeId nodeId)
		{
			execTimeVariableIndex = context.RegisterGeneratedVariable(this, 0, $"_Cooldown_{nodeId.index}_execTime", true, typeof(float));
		}

		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			exec.type = BTExec.BTExecType.UtilityCooldown;
			ref var data = ref exec.data.utilityCooldown;

			cooldown.TryGetValue(out data.invDuration);
			if(data.invDuration > 0)
				data.invDuration = 1 / data.invDuration;
			this.softCooldown.TryGetValue(out data.softCooldown);
			if(data.softCooldown)
			{
				this.softCooldownCurve.TryGetValue<AnimationCurve>(out var curve);
				curve.ConstructBlob(ref builder, ref data.curve);
			}

			context.BakeGeneratedVariable(this, 0, execTimeVariableIndex);
			data.lastExecutionTime = execTimeVariableIndex;
		}

		ExpressionRef ICustomExprNode.GetExpressionRef(GraphExpressionBakingContext context, IPort port)
		{
			context.AddError(this, $"port doesn't match");
			return default;
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			// is there value in getting this value from an input? parameterization?
			cooldown = context.AddOption<float>("Cooldown")
				.WithDisplayName("Duration")
				.WithDefaultValue(15.0f)
				.Build();

			softCooldown = context.AddOption<bool>("SoftCooldown")
				.WithDisplayName("Soft Cooldown")
				.Build();

			if(softCooldown.TryGetValue<bool>(out var s) && s)
			{
				softCooldownCurve = context.AddOption<AnimationCurve>("Utility_Cooldown")
					.WithDisplayName(string.Empty)
					.WithTooltip(
						"Multiplier to apply to downstream Utility based on the the last time this subtree was executed.\n" +
						"The X axis ranges from 0.0 to 1.0, with 1.0 corresponding to the full cooldown period."
					)
					.WithDefaultValue(UtilityCurves.StepOne)
					.Build();
			}
			else
			{
				softCooldownCurve = null;
			}
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

			context.AddUtilityDebugDisplayPort(out utilityPort);
		}
		IPort utilityPort;
		IPort IUtilityNode.GetUtilityDebugPort() => utilityPort;
	}

	/// <summary>
	/// Sets utility to a value. The input value can be modified by a curve, and the current utility can be fed in with the UtilityCurrent node.
	/// </summary>
	[Serializable]
	[Node("Execution/Utility", iconPath: "Packages/net.anttirt.khorde/Icons/Utility.png", title: "Utility (Curve)", stylesheet: "Packages/net.anttirt.khorde/Styles/Utility.uss")]
	internal class UtilityCurve : ExecBase, IUtilityNode
	{
		private INodeOption curve;
		private INodeOption customInput;
		private IPort value;
		private IPort child;

		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			exec.type = BTExec.BTExecType.UtilityCurve;
			ref var data = ref exec.data.utilityCurve;
			this.curve.TryGetValue<AnimationCurve>(out var curve);
			curve.ConstructBlob(ref builder, ref data.curve);
			customInput.TryGetValue(out data.customInput);
			if(data.customInput)
				data.input = context.GetExpressionRef(value);
			data.child = context.GetTargetNodeId(child);
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			curve = context.AddOption<AnimationCurve>("Curve")
				.WithDisplayName(string.Empty)
				.WithDefaultValue(UtilityCurves.LinearClamped)
				.Build();

			customInput = context.AddOption<bool>("CustomInput")
				.WithDisplayName("Custom Input")
				.WithDefaultValue(false)
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			this.customInput.TryGetValue(out bool customInput);

			if(customInput)
			{
				value = context.AddInputPort<float>("Value")
					.WithDisplayName(string.Empty)
					.WithDefaultValue(1)
					.Build();
			}
			else
			{
				value = null;
			}

			child = context.AddOutputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			context.AddUtilityDebugDisplayPort(out utilityPort);
		}
		IPort utilityPort;
		IPort IUtilityNode.GetUtilityDebugPort() => utilityPort;
	}

	/// <summary>
	/// Sets a simple utility value.
	/// </summary>
	[Serializable]
	[Node("Execution/Utility", iconPath: "Packages/net.anttirt.khorde/Icons/Utility.png", title: "Utility", stylesheet: "Packages/net.anttirt.khorde/Styles/Utility.uss")]
	internal class Utility : ExecBase, IUtilityNode
	{
		private IPort value;
		private IPort child;

		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			exec.type = BTExec.BTExecType.Utility;
			ref var data = ref exec.data.utility;
			data.input = context.GetExpressionRef(value);
			data.child = context.GetTargetNodeId(child);
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			value = context.AddInputPort<float>("Value")
				.WithDisplayName(string.Empty)
				.WithDefaultValue(1.0f)
				.Build();

			child = context.AddOutputPort<ExecutionFlow>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Single)
				.Build();

			context.AddUtilityDebugDisplayPort(out utilityPort);
		}
		IPort utilityPort;
		IPort IUtilityNode.GetUtilityDebugPort() => utilityPort;
	}

	/// <summary>
	/// Gets the current utility value during utility evaluation.
	/// </summary>
	[Serializable]
	[Node("Execution/Utility", iconPath: "Packages/net.anttirt.khorde/Icons/Utility.png", title: "Current Utility", stylesheet: "Packages/net.anttirt.khorde/Styles/Utility.uss")]
	internal class UtilityCurrent : ExprBase
	{
		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			context.CreateExpression(storage, new CurrentUtility());
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<float>("CurrentUtility")
				.WithDisplayName("Utility")
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithCapacity(PortCapacity.Multi)
				.Build();
		}
	}

	internal static class UtilityCurves
	{
		private static AnimationCurve full;
		private static AnimationCurve linearClamped;
		private static AnimationCurve stepOne;

		public static AnimationCurve Full => full ??= new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1));
		public static AnimationCurve LinearClamped
		{
			get
			{
				if(linearClamped == null)
				{
					linearClamped = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)) { postWrapMode = WrapMode.ClampForever };
					AnimationUtility.SetKeyLeftTangentMode(linearClamped, 0, AnimationUtility.TangentMode.Constant);
					AnimationUtility.SetKeyRightTangentMode(linearClamped, 0, AnimationUtility.TangentMode.Linear);
					AnimationUtility.SetKeyLeftTangentMode(linearClamped, 1, AnimationUtility.TangentMode.Linear);
					AnimationUtility.SetKeyRightTangentMode(linearClamped, 1, AnimationUtility.TangentMode.Constant);

				}
				return linearClamped;
			}
		}

		public static AnimationCurve StepOne
		{
			get
			{
				if(stepOne == null)
				{
					stepOne = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)) { postWrapMode = WrapMode.ClampForever };
					AnimationUtility.SetKeyLeftTangentMode(stepOne, 0, AnimationUtility.TangentMode.Constant);
					AnimationUtility.SetKeyRightTangentMode(stepOne, 0, AnimationUtility.TangentMode.Constant);
					AnimationUtility.SetKeyLeftTangentMode(stepOne, 1, AnimationUtility.TangentMode.Constant);
					AnimationUtility.SetKeyRightTangentMode(stepOne, 1, AnimationUtility.TangentMode.Constant);
				}

				return stepOne;
			}
		}

		public static void AddUtilityDebugDisplayPort(this Node.IPortDefinitionContext context, out IPort port)
		{
			port = context.AddInputPort<UtilityValue>("DebugUtilityValue")
				.WithDisplayName("Live Value")
				.WithTooltip("When playing / paused, shows the current utility value if the selected entity is running this behavior tree")
				.Build();
		}
	}
}
