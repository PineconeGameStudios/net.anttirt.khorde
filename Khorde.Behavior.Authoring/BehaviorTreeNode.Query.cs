using Khorde.Expr;
using Khorde.Expr.Authoring;
using Khorde.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
		private List<int> queryVariableIndices = new();
		private List<IPort> queryVariablePorts = new();

		static Dictionary<string, Assembly> s_assemblies = AppDomain.CurrentDomain.GetAssemblies().ToDictionary(asm => asm.FullName);

		void IExecNode.Register(BTBakingContext context)
		{
			queryVariableIndices.Clear();

			queryOption.TryGetValue<QueryGraphAsset>(out var queryGraphAsset);
			if(queryGraphAsset == null)
			{
				context.AddError(this, "query must be selected");
				return;
			}

			ref var qsData = ref queryGraphAsset.GetValue(QSData.SchemaVersion);
			var valueType = qsData.itemType;
			var type = valueType.GetValueType();

			int varIndex = 0;

			resultVariableIndex = context.RegisterGeneratedVariable(this, varIndex++, $"_{Guid}_{resultVariableIndex}_result", true, type);
			resultCountVariableIndex = context.RegisterGeneratedVariable(this, varIndex++, $"_{Guid}_{resultCountVariableIndex}_count", true, typeof(int));

			ref var variables = ref qsData.exprData.blackboardVariables;
			for(int i = 0; i < variables.Length; i++)
			{
				var varType = GetVariableType(ref variables[i]);
				queryVariableIndices.Add(context.RegisterGeneratedVariable(this, varIndex++, variables[i].name.ToString(), true, varType));
			}
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

			ref var qsData = ref queryGraphAsset.GetValue(QSData.SchemaVersion);
			var valueType = qsData.itemType;
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

			int varIndex = 0;
			context.BakeGeneratedVariable(this, varIndex++, resultVariableIndex);
			context.BakeGeneratedVariable(this, varIndex++, resultCountVariableIndex);

			ref var variables = ref qsData.exprData.blackboardVariables;
			var inputVariables = builder.Allocate(ref exec.data.query.inputs, variables.Length);
			for(int i = 0; i < variables.Length; i++)
			{
				context.BakeGeneratedVariable(this, varIndex++, queryVariableIndices[i]);

				inputVariables[i] = new Behavior.WriteVar
				{
					variableIndex = queryVariableIndices[i],
					input = context.GetExpressionRef(queryVariablePorts[i]),
				};
			}
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			queryOption = context.AddOption<QueryGraphAsset>("Query")
				.WithDisplayName("Query")
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			queryVariablePorts.Clear();

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

			if(queryGraphAsset != null)
			{
				ref var qsData = ref queryGraphAsset.GetValue(QSData.SchemaVersion);
				var type = qsData.itemType.GetValueType();

				result = context.AddOutputPort("Result")
					.WithDisplayName("Result")
					.WithDataType(type)
					.WithConnectorUI(PortConnectorUI.Circle)
					.WithPortCapacity(PortCapacity.Multi)
					.Build();

				ref var variables = ref qsData.exprData.blackboardVariables;
				for(int i = 0; i < variables.Length; ++i)
				{
					ref var variable = ref variables[i];
					var rawName = variable.name.ToString();
					var name = rawName.Replace("_QUERYINPUT_", "");

					Type varType = GetVariableType(ref variable);

					queryVariablePorts.Add(context.AddInputPort(rawName)
						.WithDisplayName(name)
						.WithDataType(varType)
						.WithConnectorUI(PortConnectorUI.Circle)
						.WithPortCapacity(PortCapacity.Single)
						.Build());
				}
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

		private static Type GetVariableType(ref BlobExpressionData.BlackboardVariable variable)
		{
			if(!s_assemblies.TryGetValue(variable.typeAssembly.ToString(), out var assembly))
				throw new InvalidOperationException($"expression references type '{variable.typeName.ToString()}' in unknown assembly '{variable.typeAssembly.ToString()}'");

			var varType = assembly.GetType(variable.typeName.ToString());
			if(varType == null)
				throw new InvalidOperationException($"expression references unknown type '{variable.typeName.ToString()}' in assembly '{variable.typeAssembly.ToString()}'");
			return varType;
		}
	}
}