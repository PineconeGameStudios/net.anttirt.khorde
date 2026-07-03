using System.Collections;
using Unity.GraphToolkit.Editor;

using BF = System.Reflection.BindingFlags;

namespace Khorde.Expr.Authoring
{
	public static class GraphExt
	{
		const BF Flags = BF.Public | BF.NonPublic | BF.FlattenHierarchy | BF.Instance;
		static object Prop(object obj, string name) => obj.GetType().GetProperty(name, Flags).GetValue(obj);

		public static bool TryGetValue(this IConstantNode node, out object value)
		{
			return TryGetValue_Constant(Prop(node, "Value"), out value);
		}

		public static bool TryGetValue(this IPort port, out object value)
		{
			if(port.IsConnected)
			{
				value = null;
				return false;
			}

			return TryGetValue_Constant(Prop(port, "EmbeddedValue"), out value);
		}

		private static bool TryGetValue_Constant(object embeddedValue_Constant, out object value)
		{
			object objectValue_Object = Prop(embeddedValue_Constant, "ObjectValue");

			if(objectValue_Object.GetType().Name == "EnumValueReference")
			{
				value = Prop(objectValue_Object, "Value");
				return true;
			}

			value = objectValue_Object;
			return true;
		}

		public static IPort GetInputPortForVariable(this ISubgraphNode node, IVariable variable)
		{
			var e = ((IDictionary)Prop(node, "InputPortToVariableDeclarationDictionary")).GetEnumerator();
			while(e.MoveNext())
			{
				var kv = e.Current;
				if(Prop(kv, "Value") == variable)
					return (IPort)Prop(kv, "Key");
			}

			return null;
		}

		public static IPort GetOutputPortForVariable(this ISubgraphNode node, IVariable variable)
		{
			var e = ((IDictionary)Prop(node, "OutputPortToVariableDeclarationDictionary")).GetEnumerator();
			while(e.MoveNext())
			{
				var kv = e.Current;
				if(Prop(kv, "Value") == variable)
					return (IPort)Prop(kv, "Key");
			}

			return null;
		}

		public static IVariable GetVariableForOutputPort(this ISubgraphNode node, IPort port)
		{
			var dict = (IDictionary)Prop(node, "OutputPortToVariableDeclarationDictionary");
			if(dict.Contains(port))
				return (IVariable)dict[port];

			return null;
		}

		public static IVariable GetVariableForInputPort(this ISubgraphNode node, IPort port)
		{
			var dict = (IDictionary)Prop(node, "InputPortToVariableDeclarationDictionary");
			if(dict.Contains(port))
				return (IVariable)dict[port];

			return null;
		}

		public static bool TryGetSubgraphAssetGuid(this ISubgraphNode node, out UnityEngine.GUID guid)
		{
			guid = (UnityEngine.GUID)Prop(Prop(node, "SubgraphReference"), "AssetGuid");
			return guid != default;
		}
	}
}