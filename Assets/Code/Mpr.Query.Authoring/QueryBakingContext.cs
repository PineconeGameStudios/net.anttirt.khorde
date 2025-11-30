using Mpr.Expr.Authoring;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.Query.Authoring
{
	public class QueryBakingContext : ExprBakingContext
	{
		IQuery query;

		public QueryBakingContext(Graph rootGraph) : base(rootGraph)
		{
		}

		protected override bool RegisterNodes()
		{
			var queries = rootGraph.GetNodes().OfType<IQuery>().ToList();
			if(queries.Count == 0)
			{
				errors.Add("no Query node found");
				return false;
			}

			if(queries.Count > 1)
			{
				errors.Add($"graph must have exactly one Query node, {queries.Count} found");
				return false;
			}

			query = queries[0];
			return base.RegisterNodes();
		}

		protected override void Bake()
		{
			ref var qsData = ref builder.ConstructRoot<QSData>();
			BakeExprData(ref qsData.exprData);

			List<IPass> passNodes = new();
			foreach(var port in query.GetPassPorts())
			{
				var pass = FindPass(port);
				if(pass != null)
					passNodes.Add(pass);
			}

			var passes = builder.Allocate(ref qsData.passes, passNodes.Count);

			for(int i = 0; i < passNodes.Count; ++i)
				BakePass(passNodes[i], ref passes[i]);

			// TODO: verify item type somehow
			// qsData.itemTypeHash = TypeManager.GetTypeInfo(TypeManager.GetTypeIndex(query.ItemType)).StableTypeHash;

			qsData.resultCount = GetExprNodeRef(query.GetResultCountPort());

			// TODO: bake custom function pointer nodes

			BakeConstData(ref qsData.exprData);
		}

		void BakePass(IPass node, ref QSPass pass)
		{
			int generatorCount = 0,
				filterCount = 0,
				scorerCount = 0;

			foreach(var blockNode in ((ContextNode)node).blockNodes)
			{
				if(blockNode is IGenerator)
					++generatorCount;
				else if(blockNode is IFilter)
					++filterCount;
				else if(blockNode is IScorer)
					++scorerCount;
				else
					throw new NotImplementedException();
			}

			var generators = builder.Allocate(ref pass.generators, generatorCount);
			var filters = builder.Allocate(ref pass.filters, filterCount);
			var scorers = builder.Allocate(ref pass.scorers, scorerCount);

			generatorCount = filterCount = scorerCount = 0;

			foreach(var blockNode in ((ContextNode)node).blockNodes)
			{
				if(blockNode is IGenerator generator)
					generator.Bake(ref generators[generatorCount++], this);
				else if(blockNode is IFilter filter)
					filter.Bake(ref filters[filterCount++], this);
				else if(blockNode is IScorer scorer)
					scorer.Bake(ref scorers[scorerCount++], this);
				else
					throw new NotImplementedException();
			}
		}

		IPass FindPass(IPort dstPort)
		{
			var portsTemp = new List<IPort>();

			using var _ = SaveSubgraph();

			while(true)
			{
				portsTemp.Clear();

				dstPort.GetConnectedPorts(portsTemp);
				if(portsTemp.Count == 0)
				{
					return null;
				}
				else
				{
					if(portsTemp.Count > 1)
						errors.Add($"unhandled multiple connections on {dstPort.GetNode()}");

					var srcPort = portsTemp[0];
					var srcNode = srcPort.GetNode();
					if(srcNode is ISubgraphNode subgraphNode)
					{
						subgraphStack.Push(subgraphNode);
						var variable = subgraphNode.GetVariableForOutputPort(srcPort);
						var varNodes = subgraphNode.GetSubgraph().GetNodes().Where(n => n is IVariableNode varNode && varNode.variable == variable).ToList();
						if(varNodes.Count == 0)
						{
							warnings.Add($"wire is cut, pass not added");
							return null;
						}
						else
						{
							if(varNodes.Count > 1)
								errors.Add($"unhandled multiple variable nodes on {variable}");

							var varNode = varNodes[0];
							portsTemp.Clear();
							dstPort = varNode.GetInputPort(0);
						}
					}
					else if(srcNode is IVariableNode varNode)
					{
						if(varNode.variable.variableKind == VariableKind.Input)
						{
							dstPort = subgraphStack.Current.GetInputPortForVariable(varNode.variable);
							subgraphStack.Pop();
						}
						else
						{
							errors.Add($"unhandled var kind {varNode.variable.variableKind} for {varNode}");
							return null;
						}
					}
					else if(srcNode is IPass passNode)
					{
						return passNode;
					}
					else
					{
						errors.Add($"unhandled node type {srcNode.GetType().FullName}");
						return null;
					}
				}
			}
		}
	}
}