using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Khorde.Expr
{
	public partial struct ReadLocalToWorld : IExpression
	{
	    public ExpressionComponentTypeInfo typeInfo;

	    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
	    void CheckInitialized(int outputIndex)
	    {
	        if (typeInfo.fields[outputIndex].length == 0)
	            throw new InvalidOperationException("field info not initialized");
	    }

		public const int OutputIndex_Transform = 0;
		public const int OutputIndex_Position = 1;
		public const int OutputIndex_Rotation = 2;
		public const int OutputIndex_Scale = 3;

		public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			CheckInitialized(OutputIndex_Transform);
			var field = typeInfo.fields[OutputIndex_Transform];

			switch(outputIndex)
			{
				case OutputIndex_Transform:
					untypedResult.CopyFrom(ctx.componentPtrs[typeInfo.componentIndex].AsNativeArray(field.offset, field.length));
					break;
				case OutputIndex_Position:
					untypedResult.ReinterpretStore<float3>(0, ctx.componentPtrs[typeInfo.componentIndex].AsComponent<LocalToWorld>().Position);
					break;
				case OutputIndex_Rotation:
					untypedResult.ReinterpretStore<quaternion>(0, ctx.componentPtrs[typeInfo.componentIndex].AsComponent<LocalToWorld>().Rotation);
					break;
				case OutputIndex_Scale:
					untypedResult.ReinterpretStore<float3>(0, ctx.componentPtrs[typeInfo.componentIndex].AsComponent<LocalToWorld>().Value.Scale());
					break;
				default:
					untypedResult.Clear();
					break;
			}
	    }
	}

	public partial struct LookupLocalToWorld : IExpression<Entity>
	{
	    public ExpressionRef Input0 { get; set; }
	    public ExpressionComponentTypeInfo typeInfo;

		public const int OutputIndex_HasComponent = 0;
		public const int OutputIndex_Transform = 1;
		public const int OutputIndex_Position = 2;
		public const int OutputIndex_Rotation = 3;
		public const int OutputIndex_Scale = 4;

	    public void Evaluate(in ExpressionEvalContext ctx, in Entity entity, int outputIndex, ref NativeArray<byte> untypedResult)
	    {
	        if (ctx.componentLookups[typeInfo.componentIndex].TryGetRefRO(entity, out var componentData))
	        {
				if(outputIndex == OutputIndex_HasComponent)
				{
					untypedResult.AsSingle<bool>() = true;
				}
				else
				{
					var field = typeInfo.fields[OutputIndex_Transform - 1];

					if(componentData.IsCreated)
					{
						switch(outputIndex)
						{
							case OutputIndex_Transform:
								untypedResult.CopyFrom(componentData.GetSubArray(field.offset, field.length));
								break;
							case OutputIndex_Position:
								untypedResult.ReinterpretStore<float3>(0, ctx.componentPtrs[typeInfo.componentIndex].AsComponent<LocalToWorld>().Position);
								break;
							case OutputIndex_Rotation:
								untypedResult.ReinterpretStore<quaternion>(0, ctx.componentPtrs[typeInfo.componentIndex].AsComponent<LocalToWorld>().Rotation);
								break;
							case OutputIndex_Scale:
								untypedResult.ReinterpretStore<float3>(0, ctx.componentPtrs[typeInfo.componentIndex].AsComponent<LocalToWorld>().Value.Scale());
								break;
							default:
								untypedResult.Clear();
								break;
						}
					}
					else
					{
						untypedResult.Clear();
					}
				}
	        }
	        else
	        {
	            untypedResult.Clear();
	        }
	    }
	}
}
