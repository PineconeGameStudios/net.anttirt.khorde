using System;
using Unity.GraphToolkit.Editor;

namespace Khorde.Expr.Authoring
{
	public static class GraphExt
	{
		public static bool TryGetValue(this IConstantNode node, out object value)
		{
			throw new NotImplementedException();
		}

		public static bool TryGetValue(this IPort port, out object value)
		{
			throw new NotImplementedException();
		}

		public static IPort GetInputPortForVariable(this ISubgraphNode node, IVariable variable)
		{
			throw new NotImplementedException();
		}

		public static IPort GetOutputPortForVariable(this ISubgraphNode node, IVariable variable)
		{
			throw new NotImplementedException();
		}

		public static IVariable GetVariableForOutputPort(this ISubgraphNode node, IPort port)
		{
			throw new NotImplementedException();
		}

		public static IVariable GetVariableForInputPort(this ISubgraphNode node, IPort port)
		{
			throw new NotImplementedException();
		}

		public static bool TryGetSubgraphAssetGuid(this ISubgraphNode node, out UnityEngine.GUID guid)
		{
			throw new NotImplementedException();
		}
	}
}