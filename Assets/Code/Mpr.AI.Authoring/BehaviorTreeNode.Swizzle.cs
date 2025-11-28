using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Mpr.AI.BT.Nodes
{
	internal abstract class SwizzleBase : Base
	{
		protected enum BaseType
		{
			Int,
			Float,
			Double,
		}

		protected static (BaseType, int) Decompose(Type type)
		{
			if(type == typeof(int))     return (BaseType.Int   , 1);
			if(type == typeof(int2))    return (BaseType.Int   , 2);
			if(type == typeof(int3))    return (BaseType.Int   , 3);
			if(type == typeof(int4))    return (BaseType.Int   , 4);
			if(type == typeof(float))   return (BaseType.Float , 1);
			if(type == typeof(float2))  return (BaseType.Float , 2);
			if(type == typeof(float3))  return (BaseType.Float , 3);
			if(type == typeof(float4))  return (BaseType.Float , 4);
			if(type == typeof(double))  return (BaseType.Double, 1);
			if(type == typeof(double2)) return (BaseType.Double, 2);
			if(type == typeof(double3)) return (BaseType.Double, 3);
			if(type == typeof(double4)) return (BaseType.Double, 4);
			throw new NotImplementedException();
		}

		protected static Type GetResultType(BaseType baseType, int count)
		{
			switch(baseType)
			{
				case BaseType.Int:
					switch(count)
					{
						case 1: return typeof(int);
						case 2: return typeof(int2);
						case 3: return typeof(int3);
						case 4: return typeof(int4);
					}
					break;
				case BaseType.Float:
					switch(count)
					{
						case 1: return typeof(float);
						case 2: return typeof(float2);
						case 3: return typeof(float3);
						case 4: return typeof(float4);
					}
					break;
				case BaseType.Double:
					switch(count)
					{
						case 1: return typeof(double);
						case 2: return typeof(double2);
						case 3: return typeof(double3);
						case 4: return typeof(double4);
					}
					break;
				default:
					break;
			}
			throw new NotImplementedException();
		}
	}

	[Serializable]
	internal abstract class SwizzleBase<T> : SwizzleBase, IExprNode where T : unmanaged
	{
		public void Bake(ref BlobBuilder builder, ref BTExpr expr, BakingContext context)
		{
			UnityEngine.Debug.LogWarning($"{GetType().Name}.Bake() not implemented");
		}

		public override string Title => $"Swizzle ({typeof(T).Name})";

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			var (_, count) = Decompose(typeof(T));

			string pattern = count switch
			{
				1 => "x",
				2 => "xy",
				3 => "xyz",
				4 => "xyzw",
				_ => throw new NotImplementedException()
			};

			context.AddOption<string>("pattern")
				.WithDisplayName(string.Empty)
				.WithDefaultValue(pattern)
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			GetNodeOption(0).TryGetValue<string>(out var pattern);

			context.AddInputPort("in")
				.WithDisplayName(string.Empty)
				.WithDataType<T>()
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			if(pattern.Length == 0)
				return;

			if(pattern.Length > 4)
				return;

			int minInputCount = 0;

			foreach(char ch in pattern)
			{
				switch(ch)
				{
					case 'x': case 'r': minInputCount = math.max(minInputCount, 1); break;
					case 'y': case 'g': minInputCount = math.max(minInputCount, 2); break;
					case 'z': case 'b': minInputCount = math.max(minInputCount, 3); break;
					case 'w': case 'a': minInputCount = math.max(minInputCount, 4); break;
					default: return;
				}
			}

			var (baseType, inputCount) = Decompose(typeof(T));

			if(inputCount < minInputCount)
				return;

			var resultType = GetResultType(baseType, pattern.Length);

			context.AddOutputPort("out")
				.WithDisplayName(string.Empty)
				.WithDataType(resultType)
				.WithPortCapacity(PortCapacity.Multi)
				.Build();
		}
	}

	[Serializable] [NodeCategory("Math/Swizzle")] internal class SwizzleInt : SwizzleBase<int> { }
	[Serializable] [NodeCategory("Math/Swizzle")] internal class SwizzleInt2 : SwizzleBase<int> { }
	[Serializable] [NodeCategory("Math/Swizzle")] internal class SwizzleInt3 : SwizzleBase<int> { }
	[Serializable] [NodeCategory("Math/Swizzle")] internal class SwizzleInt4 : SwizzleBase<int> { }
	[Serializable] [NodeCategory("Math/Swizzle")] internal class SwizzleFloat : SwizzleBase<float> { }
	[Serializable] [NodeCategory("Math/Swizzle")] internal class SwizzleFloat2 : SwizzleBase<float2> { }
	[Serializable] [NodeCategory("Math/Swizzle")] internal class SwizzleFloat3 : SwizzleBase<float3> { }
	[Serializable] [NodeCategory("Math/Swizzle")] internal class SwizzleFloat4 : SwizzleBase<float4> { }
	[Serializable] [NodeCategory("Math/Swizzle")] internal class SwizzleDouble : SwizzleBase<double> { }
	[Serializable] [NodeCategory("Math/Swizzle")] internal class SwizzleDouble2 : SwizzleBase<double2> { }
	[Serializable] [NodeCategory("Math/Swizzle")] internal class SwizzleDouble3 : SwizzleBase<double3> { }
	[Serializable] [NodeCategory("Math/Swizzle")] internal class SwizzleDouble4 : SwizzleBase<double4> { }

}
