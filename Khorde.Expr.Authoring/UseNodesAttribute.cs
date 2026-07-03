using System;

namespace Khorde.Expr.Authoring
{
	[System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = true)]
	public sealed class UseNodesAttribute : System.Attribute
	{
		readonly Type nodeBaseType;

		public UseNodesAttribute(Type nodeBaseType)
		{
			this.nodeBaseType = nodeBaseType;
		}

		public Type NodeBaseType
		{
			get { return nodeBaseType; }
		}
	}
}