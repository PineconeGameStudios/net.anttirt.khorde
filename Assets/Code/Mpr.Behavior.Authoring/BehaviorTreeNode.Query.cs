using Mpr.Expr;
using Mpr.Query;
using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.Behavior.Authoring;

[Serializable]
[NodeCategory("Execution")]
internal class Query : ExecBase, IExecNode
{
	private IPort resultVarPort;
	private IPort resultCountVarPort;
	private INodeOption queryOption;

	public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context)
	{
		exec.type = BTExec.BTExecType.Query;
		exec.data.query = new Behavior.Query
		{
			variableIndex = context.GetVariableIndex(resultVarPort),
			resultCountVariableIndex = context.GetVariableIndex(resultCountVarPort),
			queryIndex = context.GetQueryIndex(queryOption),
		};
	}

	protected override void OnDefineOptions(IOptionDefinitionContext context)
	{
		queryOption = context.AddOption<QueryGraphAsset>("Query")
			.WithDisplayName("Query")
			.Build();
	}

	protected override void OnDefinePorts(IPortDefinitionContext context)
	{
		context.AddInputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
			.WithDisplayName(string.Empty)
			.WithConnectorUI(PortConnectorUI.Arrowhead)
			.WithPortCapacity(PortCapacity.Single)
			.Build();

		queryOption.TryGetValue<QueryGraphAsset>(out var queryGraphAsset);
		if(queryGraphAsset == null)
			return;

		var valueType = queryGraphAsset.GetValue(QSData.SchemaVersion).itemType;

		var type = valueType.GetValueType();

		if(type == null)
			return;

		resultVarPort = context.AddInputPort("ResultVariable")
			.WithDisplayName(queryGraphAsset.name + "_Result")
			.WithDataType(type)
			.WithConnectorUI(PortConnectorUI.Arrowhead)
			.WithPortCapacity(PortCapacity.Single)
			.Build();

		resultCountVarPort = context.AddInputPort<int>("ResultCountVariable")
			.WithDisplayName(queryGraphAsset.name + "_Result_Count")
			.WithConnectorUI(PortConnectorUI.Arrowhead)
			.WithPortCapacity(PortCapacity.Single)
			.Build();
	}
}