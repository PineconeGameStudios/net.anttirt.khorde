using System;
using Unity.Entities;

namespace Khorde.Behavior.Authoring
{
	[Serializable] internal class WriteLocalTransform : ComponentWriterNode<Unity.Transforms.LocalTransform> { }

	[Serializable] internal class WriteLocalToWorld : ComponentWriterNode<Unity.Transforms.LocalToWorld> { }
}