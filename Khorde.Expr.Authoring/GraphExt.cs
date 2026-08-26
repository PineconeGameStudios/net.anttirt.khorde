using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.GraphToolkit.Editor;
using Unity.Scripting.LifecycleManagement;
using BF = System.Reflection.BindingFlags;

namespace Khorde.Expr.Authoring
{
	public static partial class GraphExt
	{
		const BF InstanceFlags = BF.Public | BF.NonPublic | BF.FlattenHierarchy | BF.Instance;
		const BF StaticFlags = BF.Public | BF.NonPublic | BF.FlattenHierarchy | BF.Static;
		static object Prop(object obj, string name) => obj.GetType().GetProperty(name, InstanceFlags).GetValue(obj);
		static object Field(object obj, string name) => obj.GetType().GetField(name, InstanceFlags).GetValue(obj);
		static object StaticField(Type type, string name) => type.GetField(name, StaticFlags).GetValue(null);

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

		delegate IReadOnlyList<Type> GetNodeTypesDelegate(Type graphType);

		[OnCodeLoaded]
		static void ProcessSubGraphAttributes()
		{
			var publicGraphFactoryT = typeof(IVariable).Assembly.GetType("Unity.GraphToolkit.Editor.Implementation.PublicGraphFactory");
			RuntimeHelpers.RunClassConstructor(publicGraphFactoryT.TypeHandle);

			var GetNodeTypes = (GetNodeTypesDelegate)publicGraphFactoryT.GetMethod("GetNodeTypes", BF.Static | BF.Public | BF.NonPublic).CreateDelegate(typeof(GetNodeTypesDelegate));

			var s_GraphInfos = (IDictionary)StaticField(publicGraphFactoryT, "s_GraphInfos");
			foreach(Type graphType in s_GraphInfos.Keys)
			{
				var graphInfo = s_GraphInfos[graphType];
				var subgraphTypes = (List<Type>)Field(graphInfo, "subgraphTypes");
				foreach(var useSubgraphAttribute in graphType.GetCustomAttributes<UseSubgraphAttribute>())
				{
					if(!subgraphTypes.Contains(useSubgraphAttribute.SubGraphType))
						subgraphTypes.Add(useSubgraphAttribute.SubGraphType);
				}

				// ensure graphInfo.nodeTypes is populated
				GetNodeTypes(graphType);

				// remove duplicates
				var nodeTypes = (List<Type>)Field(graphInfo, "nodeTypes");
				var typeSet = new HashSet<Type>(nodeTypes);
				for(int i = 0; i < nodeTypes.Count;)
				{
					if(!typeSet.Remove(nodeTypes[i]))
					{
						nodeTypes.RemoveAt(i);
					}
					else
					{
						i++;
					}
				}
			}
		}
	}

	[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
	public sealed class UseSubgraphAttribute : Attribute
	{
		readonly Type subGraphType;

		public UseSubgraphAttribute(Type subGraphType)
		{
			this.subGraphType = subGraphType;
		}

		public Type SubGraphType
		{
			get { return subGraphType; }
		}
	}
}