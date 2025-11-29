using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.Expr
{
	[Serializable]
	[NodeCategory("Component")]
	public abstract class ComponentReaderNode<T> : ExprNode, IComponentAccess where T : Unity.Entities.IComponentData
	{
		public Type ComponentType => typeof(T);
		public bool IsReadOnly => true;

		public override string Title => $"Read {typeof(T).Name}";

		public override void Bake(ref BlobBuilder builder, ref BTExpr expr, ExprBakingContext context)
		{
			var index = context.componentTypes.IndexOf(typeof(T));
			if(index == -1)
				throw new System.Exception($"component type {typeof(T).Name} not found in type list");

			expr.type = BTExpr.BTExprType.ReadField;
			expr.data.readField = new BTExpr.ReadField
			{
				componentIndex = (byte)index,
			};

			var fields = GetFields();

			var bakedFields = builder.Allocate(ref expr.data.readField.fields, fields.Length);
			for(int i = 0; i < fields.Length; i++)
			{
				int offset = UnsafeUtility.GetFieldOffset(fields[i]);
				if(offset > ushort.MaxValue)
					throw new Exception("component too large; field offset over 65k");

				bakedFields[i] = fields[i];
			}
		}

		public static System.Reflection.FieldInfo[] GetFields() => typeof(T).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			foreach(var field in GetFields())
			{
				context.AddOutputPort(field.Name)
					.WithDisplayName(field.Name)
					.WithDataType(field.FieldType)
					.Build();
			}
		}
	}

	[Serializable] internal class ReadLocalTransform : ComponentReaderNode<Unity.Transforms.LocalTransform> { }
}