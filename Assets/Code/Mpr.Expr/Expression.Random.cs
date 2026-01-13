using Unity.Burst;
using Unity.Collections;

namespace Mpr.Expr;

public partial struct RandomFloat : IExpression
{
	public float min, max;

	[BurstCompile]
	public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
	{
		ref var rng = ref RandomHelper.JobRandom;
		var result = untypedResult.Reinterpret<float>(1);
		for(int i = 0; i < result.Length; i++)
			result[i] = rng.NextFloat(min, max);
	}
}

public partial struct RandomFloat2Direction : IExpression
{
	[BurstCompile]
	public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
	{
		untypedResult.ReinterpretStore(0, RandomHelper.JobRandom.NextFloat2Direction());
	}
}

public partial struct RandomFloat3Direction : IExpression
{
	[BurstCompile]
	public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
	{
		untypedResult.ReinterpretStore(0, RandomHelper.JobRandom.NextFloat3Direction());
	}
}

public partial struct RandomInt : IExpression
{
	public int min, max;

	[BurstCompile]
	public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
	{
		ref var rng = ref RandomHelper.JobRandom;
		var result = untypedResult.Reinterpret<int>(1);
		for(int i = 0; i < result.Length; i++)
			result[i] = rng.NextInt(min, max);
	}
}
