using System.Diagnostics;

namespace Ergo.Lang.Ast;

public class NewKnowledgeBase(TermCache cache)
{
    public readonly record struct OpAddr(TermCache.ConstAddr Functor, TermCache.Arity Arity);


    public readonly TermCache Cache = cache;
    internal readonly Dictionary<OpAddr, ErgoVM.Op> ops = new();






}

public sealed class TermCache
{
    public readonly record struct Arity(int I) { public static explicit operator Arity(int a) => new(a); };
    public readonly record struct VarAddr(int I) { public static explicit operator VarAddr(int a) => new(a); };
    public readonly record struct ConstAddr(int I) { public static explicit operator ConstAddr(int a) => new(a); };
    public readonly record struct StructAddr(int I) { public static explicit operator StructAddr(int a) => new(a); };
    public readonly record struct NodeAddr(int I) { public static explicit operator NodeAddr(int a) => new(a); };

    internal readonly Dictionary<ConstAddr, Atom> constants = new();
    internal readonly Dictionary<VarAddr, Variable> variables = new();
    internal readonly List<TermNode> nodes = new() { new InvalidTermNode() };
    internal readonly Dictionary<(NodeAddr TreeIndex, VarAddr VariableIndex), (NodeAddr TreeIndex, StructAddr StructIndex)> variableSubstructures = new();
    internal readonly Queue<NodeAddr> recycledIds = new();

    public TermNode this[NodeAddr i] => nodes[i.I];
    public Atom this[ConstAddr i] => constants[i];
    public Variable this[VarAddr i] => variables[i];

    public ConstAddr DefineConstant(Atom a)
    {
        var i = new ConstAddr(a.GetHashCode());
# if DEBUG
        if (constants.TryGetValue(i, out var oldValue))
            Debug.Assert(oldValue.Equals(a));
#endif
        constants[i] = a;
        return i;
    }

    public VarAddr DefineVariable(Variable v)
    {
        var i = new VarAddr(v.GetHashCode());
# if DEBUG
        if (variables.TryGetValue(i, out var oldValue))
            Debug.Assert(oldValue.Equals(v));
#endif
        variables[i] = v;
        return i;
    }

    public NodeAddr GetFreeId() => recycledIds.TryDequeue(out var id) ? id : new(nodes.Count);
    internal void Add(TermNode node) => nodes.Insert(node.TreeIndex.I, node);
}

