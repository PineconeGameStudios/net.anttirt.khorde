using System;
using Unity.GraphToolkit.Editor;

namespace Mpr.AI.BT.Nodes
{
	[Serializable]
	[UseWithGraph(typeof(BehaviorTreeGraph))]
	internal abstract class Base : Node
	{
		public const string EXEC_PORT_DEFAULT_NAME = "Execution";
	}

	[Serializable]
	internal class Root : Base
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort(EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();
		}
	}

	[Serializable]
	[UseWithGraph(typeof(BehaviorTreeGraph))]
	internal class Sequence : ContextNode
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort(Base.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();
		}
	}

	[Serializable]
	[UseWithGraph(typeof(BehaviorTreeGraph))]
	internal class Selector : ContextNode
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort(Base.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();
		}
	}

	[Serializable]
	[UseWithContext(typeof(Sequence))]
	internal class SetInt : BlockNode
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<int>("Value")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}

	[Serializable]
	[UseWithContext(typeof(Sequence))]
	internal class SubTree : BlockNode
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort(Base.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();
		}
	}

	[Serializable]
	[UseWithContext(typeof(Selector))]
	internal class SubTreeOption : BlockNode
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<bool>("Condition")
				.WithDisplayName("Condition")
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddOutputPort(Base.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();
		}
	}

	[Serializable]
	internal abstract class ComponentReaderNode<T> : Base where T : Unity.Entities.IComponentData
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			foreach(var field in typeof(T).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
			{
				context.AddOutputPort(field.Name)
					.WithDisplayName(field.Name)
					.WithDataType(field.FieldType)
					.Build();
			}
		}
	}

	[Serializable]
	internal abstract class ComponentWriterNode<T> : Base where T : Unity.Entities.IComponentData
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort(Base.EXEC_PORT_DEFAULT_NAME)
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();

			foreach(var field in typeof(T).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
			{
				context.AddInputPort(field.Name)
					.WithDisplayName(field.Name)
					.WithDataType(field.FieldType)
					.Build();
			}
		}
	}

	[Serializable]
	internal class ReadPlayerController : ComponentReaderNode<Mpr.Game.PlayerController> { }

	[Serializable]
	internal class WritePlayerController : ComponentWriterNode<Mpr.Game.PlayerController> { }
}