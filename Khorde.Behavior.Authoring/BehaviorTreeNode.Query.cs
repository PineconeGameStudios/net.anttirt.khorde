using Khorde.Expr;
using Khorde.Expr.Authoring;
using Khorde.Query;
using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Khorde.Behavior.Authoring
{
	[Serializable]
	[NodeCategory("Execution")]
	internal class Query : ExecBase, IExecNode, ICustomExprNode
	{
		private IPort execInput;
		private IPort execSuccess;
		private IPort execFailure;
		private IPort result;

		private INodeOption queryOption;
		private int resultVariableIndex;
		private int resultCountVariableIndex;

		void IExecNode.Register(BTBakingContext context)
		{
			queryOption.TryGetValue<QueryGraphAsset>(out var queryGraphAsset);
			if(queryGraphAsset == null)
			{
				context.AddError(this, "query must be selected");
				return;
			}

			var valueType = queryGraphAsset.GetValue(QSData.SchemaVersion).itemType;
			var type = valueType.GetValueType();

			resultVariableIndex = context.RegisterGeneratedVariable(this, 0, "result", type);
			resultCountVariableIndex = context.RegisterGeneratedVariable(this, 1, "resultCount", typeof(int));
		}

		ExpressionRef ICustomExprNode.GetExpressionRef(GraphExpressionBakingContext context, IPort port)
		{
			if(port == result)
			{
				return context.GetGeneratedVariableNodeRef(this, 0);
			}

			context.AddError(this, $"port doesn't match");
			return default;
		}

		public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId)
		{
			queryOption.TryGetValue<QueryGraphAsset>(out var queryGraphAsset);
			if(queryGraphAsset == null)
			{
				context.AddError(this, "query must be selected");
				return;
			}

			var valueType = queryGraphAsset.GetValue(QSData.SchemaVersion).itemType;
			var type = valueType.GetValueType();

			exec.type = BTExec.BTExecType.Query;
			exec.data.query = new Behavior.Query
			{
				variableIndex = resultVariableIndex,
				resultCountVariableIndex = resultCountVariableIndex,
				queryIndex = context.GetQueryIndex(queryOption),
				success = context.GetTargetNodeId(execSuccess),
				failure = context.GetTargetNodeId(execFailure),
			};

			context.BakeGeneratedVariable(this, 0, resultVariableIndex);
			context.BakeGeneratedVariable(this, 1, resultCountVariableIndex);
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			queryOption = context.AddOption<QueryGraphAsset>("Query")
				.WithDisplayName("Query")
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			execInput = context.AddInputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			execSuccess = context.AddOutputPort<Exec>("ExecSuccess")
				.WithDisplayName("Success")
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			queryOption.TryGetValue<QueryGraphAsset>(out var queryGraphAsset);
			var type = queryGraphAsset?.GetValue(QSData.SchemaVersion).itemType.GetValueType();
			if(type != null)
			{
				result = context.AddOutputPort("Result")
					.WithDisplayName("Result")
					.WithDataType(type)
					.WithConnectorUI(PortConnectorUI.Circle)
					.WithPortCapacity(PortCapacity.Multi)
					.Build();
			}
			else
			{
				result = context.AddOutputPort("Result")
					.WithDisplayName("Result")
					.WithConnectorUI(PortConnectorUI.Circle)
					.WithPortCapacity(PortCapacity.Multi)
					.Build();
			}

			execFailure = context.AddOutputPort<Exec>("ExecFailure")
				.WithDisplayName("Failure")
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

		}
	}
}