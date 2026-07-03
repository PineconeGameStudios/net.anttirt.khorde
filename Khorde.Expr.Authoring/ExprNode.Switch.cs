using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Khorde.Expr.Authoring
{
	[Serializable]
	class SwitchInt : ExprBase
	{
		INodeOption valueType;
		INodeOption optionCount;
		List<INodeOption> cases = new();
		IPort @switch;
		List<IPort> options = new();
		IPort @default;

		public SwitchInt() { Title = "Switch"; }

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var expr = ref context.CreateExpression<Expr.Switch>(storage);
			expr.@switch = context.GetExpressionRef(@switch);

			this.valueType.TryGetValue<ExpressionValueType>(out var valueExprType);
			var valueType = valueExprType.GetValueType();
			if(valueType == null)
			{
				context.AddError(this, "Select value type");
				return;
			}

			optionCount.TryGetValue<int>(out var count);
			count = math.clamp(count, 0, 32);
			var options = context.Builder.Allocate(ref expr.cases, count);

			for(int i = 0; i < count; i++)
			{
				cases[i].TryGetValue<int>(out var @case);
				options[i].@case = @case;
				options[i].value = context.GetExpressionRef(this.options[i]);
			}

			expr.@default = context.GetExpressionRef(@default);
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			valueType = context.AddOption<ExpressionValueType>("valueType")
				.WithDisplayName(string.Empty)
				.Build();

			optionCount = context.AddOption<int>("optionCount")
				.WithDisplayName("Cases")
				.WithDefaultValue(1)
				.Build();

			optionCount.TryGetValue<int>(out var count);
			count = math.clamp(count, 0, 32);

			cases.Clear();
			for(int i = 0; i < count; i++)
			{
				cases.Add(context.AddOption<int>($"case{i:D2}")
					.WithDisplayName(string.Empty)
					.WithDefaultValue(i)
					.Build());
			}
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			@switch = context.AddInputPort<int>("switch")
				.WithDisplayName("Switch")
				.Build();

			this.valueType.TryGetValue<ExpressionValueType>(out var valueExprType);
			var valueType = valueExprType.GetValueType();
			if(valueType == null)
				return;

			optionCount.TryGetValue<int>(out var count);
			count = math.clamp(count, 0, 32);

			options.Clear();
			for(int i = 0; i < count; i++)
			{
				cases[i].TryGetValue<int>(out var @case);
				options.Add(context.AddInputPort($"option{i:D2}").WithDataType(valueType).WithDisplayName(@case.ToString()).Build());
			}

			@default = context.AddInputPort("default").WithDataType(valueType).WithDisplayName("Default").Build();

			context.AddOutputPort("result").WithDisplayName(string.Empty).WithDataType(valueType).Build();
		}
	}

	[Serializable]
	class Select : ExprBase
	{
		INodeOption valueType;
		IPort @switch;
		IPort @true;
		IPort @false;

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var expr = ref context.CreateExpression<Expr.Select>(storage);
			expr.@switch = context.GetExpressionRef(@switch);

			this.valueType.TryGetValue<ExpressionValueType>(out var valueExprType);
			var valueType = valueExprType.GetValueType();
			if(valueType == null)
			{
				context.AddError(this, "Select value type");
				return;
			}

			expr.@true = context.GetExpressionRef(@true);
			expr.@false = context.GetExpressionRef(@false);
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			valueType = context.AddOption<ExpressionValueType>("valueType")
				.WithDisplayName(string.Empty)
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			@switch = context.AddInputPort<bool>("switch")
				.WithDisplayName("If")
				.Build();

			this.valueType.TryGetValue<ExpressionValueType>(out var valueExprType);
			var valueType = valueExprType.GetValueType();
			if(valueType == null)
				return;

			@true = context.AddInputPort("true").WithDataType(valueType).WithDisplayName("True").Build();
			@false = context.AddInputPort("false").WithDataType(valueType).WithDisplayName("False").Build();

			context.AddOutputPort("result").WithDisplayName(string.Empty).WithDataType(valueType).Build();
		}
	}

	[Serializable]
	public abstract class SwitchEnum<TEnum> : ExprBase where TEnum : struct, Enum
	{
		INodeOption valueType;
		IPort @switch;
		List<IPort> options = new();

		public SwitchEnum() { Title = "Switch " + typeof(TEnum).Name; }

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var expr = ref context.CreateExpression<Expr.Switch>(storage);
			expr.@switch = context.GetExpressionRef(@switch);

			this.valueType.TryGetValue<ExpressionValueType>(out var valueExprType);
			var valueType = valueExprType.GetValueType();
			if(valueType == null)
			{
				context.AddError(this, "Select value type");
				return;
			}

			var cases = (TEnum[])Enum.GetValues(typeof(TEnum));
			var options = context.Builder.Allocate(ref expr.cases, cases.Length);

			for(int i = 0; i < cases.Length; i++)
			{
				options[i].@case = (int)(object)cases[i];
				options[i].value = context.GetExpressionRef(this.options[i]);
			}

			expr.@default = context.Const(Activator.CreateInstance(valueType));
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			valueType = context.AddOption<ExpressionValueType>("valueType")
				.WithDisplayName(string.Empty)
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			@switch = context.AddInputPort<TEnum>("switch")
				.WithDisplayName("Switch")
				.Build();

			this.valueType.TryGetValue<ExpressionValueType>(out var valueExprType);
			var valueType = valueExprType.GetValueType();
			if(valueType == null)
				return;

			var cases = (TEnum[])Enum.GetValues(typeof(TEnum));

			options.Clear();
			for(int i = 0; i < cases.Length; i++)
			{
				options.Add(context.AddInputPort($"option{i:D2}").WithDataType(valueType).WithDisplayName(cases[i].ToString()).Build());
			}

			context.AddOutputPort("result").WithDisplayName(string.Empty).WithDataType(valueType).Build();
		}
	}
}