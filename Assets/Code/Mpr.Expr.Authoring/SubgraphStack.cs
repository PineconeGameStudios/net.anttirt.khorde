using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;

namespace Mpr.Expr.Authoring;

/// <summary>
/// <see cref="INode"/> instances can be "instantiated" via subgraphs. Each
/// instance gets a unique key by a hash of the subgraph stack leading to
/// the node and the node object itself.
/// </summary>
/// <param name="subgraphStackKey"></param>
/// <param name="node"></param>
public readonly record struct NodeKey<TNode>(UnityEngine.Hash128 subgraphStackKey, TNode node) where TNode : INode;

/// <summary>
/// Every execution path through unique subgraph nodes produces a copy of
/// the subgraph because they can have different input expressions. We key
/// them via the subgraph stack. Expression resolution works in reverse.
/// </summary>
public class SubgraphStack : IEquatable<SubgraphStack>
{
    private List<ISubgraphNode> path;
    private List<UnityEngine.Hash128> pathHashes;

    public SubgraphStack()
    {
        path = new();
        pathHashes = new();
    }

    public SubgraphStack(SubgraphStack src)
    {
        path = new(src.path);
        pathHashes = new(src.pathHashes);
    }

    public SubgraphStack Clone() => new SubgraphStack(this);

    public IEnumerable<UnityEngine.Hash128> Hashes => pathHashes;

    public int Depth => pathHashes.Count;
    public void Push(ISubgraphNode node) { path.Add(node); pathHashes.Add(node.Guid); }
    public void Pop() { path.RemoveAt(path.Count - 1); pathHashes.RemoveAt(pathHashes.Count - 1); }
    public UnityEngine.Hash128 GetKey() => UnityEngine.Hash128.Compute(pathHashes);
    public override bool Equals(object obj) => Equals(obj as SubgraphStack);
    public override int GetHashCode() => GetKey().GetHashCode();
    public ISubgraphNode Current => path[path.Count - 1];

    public bool Equals(SubgraphStack other)
    {
        if(other is null)
            return false;

        if(GetHashCode() != other.GetHashCode())
            return false;

        return pathHashes.SequenceEqual(other.pathHashes);
    }

    public void Clear()
    {
        path.Clear();
        pathHashes.Clear();
    }

    public static bool operator ==(SubgraphStack left, SubgraphStack right)
    {
        if(left is null)
            return right is null;

        return left.Equals(right);
    }

    public static bool operator !=(SubgraphStack left, SubgraphStack right) => !(left == right);
}