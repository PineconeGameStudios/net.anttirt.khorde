using Khorde.Expr.Authoring;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Khorde.Behavior
{
	class BehaviorTreeActionPostProcessor : AssetPostprocessor
	{
		public struct LayoutParam
		{
			public string name;
			public ushort offset;
			public ushort size;
			public Type type;
		}

		static Dictionary<Type, List<LayoutParam>> s_layouts = new();

		private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
		{
			if(didDomainReload)
			{
				// code may have changed, update all layouts
				foreach(var guid in AssetDatabase.FindAssetGUIDs($"t:{nameof(BehaviorTreeAction)}"))
				{
					var action = AssetDatabase.LoadAssetByGUID<BehaviorTreeAction>(guid);

					if(action != null)
					{
						UpdateAsset(action);
					}
				}
			}
			else
			{
				foreach(var path in importedAssets)
				{
					if(path.EndsWith(".asset"))
					{
						var action = AssetDatabase.LoadAssetAtPath<BehaviorTreeAction>(path);

						if(action != null)
						{
							UpdateAsset(action);
						}
					}
				}
			}
		}

		public static List<LayoutParam> GetLayout(BehaviorTreeAction action, SerializedObject so)
		{
			var actionType = action.GetType();
			if(!s_layouts.TryGetValue(actionType, out var layout))
				s_layouts[actionType] = layout = ComputeLayout(actionType, so);
			return layout;
		}

		public static void UpdateAsset(BehaviorTreeAction action)
		{
			var actionType = action.GetType();
			bool dirty = false;

			var so = new SerializedObject(action);
			var layout = GetLayout(action, so);

			for(int i = 0; i < layout.Count; ++i)
			{
				var p = layout[i];
				var prop = so.FindProperty(p.name);

				if(prop.FindPropertyRelative("offset").uintValue != p.offset)
				{
					prop.FindPropertyRelative("offset").uintValue = p.offset;
					dirty = true;
				}
			}

			if(dirty)
			{
				so.ApplyModifiedProperties();
				AssetDatabase.SaveAssetIfDirty(action);
			}
		}

		private static List<LayoutParam> ComputeLayout(Type actionType, SerializedObject so)
		{
			List<LayoutParam> layout = new();

			var actionParams = new Dictionary<string, Type>();
			foreach(var field in actionType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
			{
				if(field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(BehaviorTreeActionParam<>))
				{
					actionParams[field.Name] = field.FieldType.GetGenericArguments()[0];
				}
			}

			var parameters = new List<(Type type, string name, int alignment, int size)>();

			{
				var prop = so.GetIterator();
				if(prop.Next(true))
				{
					do
					{
						if(actionParams.TryGetValue(prop.name, out var paramType))
						{
							int size = UnsafeUtility.SizeOf(paramType);
							int alignment = ExprAuthoring.AlignOf(paramType);
							parameters.Add((paramType, prop.name, alignment, size));
						}
					}
					while(prop.Next(false));
				}
			}

			parameters.Sort((a, b) => b.alignment.CompareTo(a.alignment));

			int currentOffset = 0;
			foreach(var p in parameters)
			{
				var rem = currentOffset % p.alignment;
				if(rem != 0)
					currentOffset += p.alignment - rem;

				if(currentOffset + p.size > BehaviorTreeInvocation.ParamStorageSize)
				{
					UnityEngine.Debug.LogError($"Type {actionType} has too many invoke parameters. All parameters must fit in max {BehaviorTreeInvocation.ParamStorageSize} bytes of storage.");
					return layout;
				}

				layout.Add(new LayoutParam { name = p.name, offset = (ushort)currentOffset, size = (ushort)p.size, type = p.type });

				currentOffset += p.size;
			}

			return layout;
		}
	}

	[CustomPropertyDrawer(typeof(BehaviorTreeActionParam<>))]
	class BehaviorTreeActionParamPropertyDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return 0;
		}
	}

}