using Ergo.Lang.Ast.Terms.Interfaces;
using System.Diagnostics;

namespace Ergo.Lang.Ast;

public sealed class TermTree
{
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

public sealed class TermTreeSubstitutionMap(TermTree tree)
{
    private readonly Dictionary<(TermTree.NodeAddr TreeIndex, TermTree.VarAddr VarIndex), (TermTree.NodeAddr OldNode, TermTree.NodeAddr NewNode)> map = new();

    public void Add(TermTree.NodeAddr treeIndex, TermTree.VarAddr varIndex, TermTree.NodeAddr oldValue, TermTree.NodeAddr newValue)
        => map[(treeIndex, varIndex)] = (oldValue, newValue);

    public void ApplyForwards()
    {
        foreach (var ((treeIndex, varIndex), (oldNode, newNode)) in map)
        {
            Debug.Assert(tree[treeIndex][varIndex].TreeIndex == oldNode);
            tree[treeIndex][varIndex] = tree[newNode];
        }
    }

    public void ApplyBackwards()
    {
        foreach (var ((treeIndex, varIndex), (oldNode, newNode)) in map)
        {
            Debug.Assert(tree[treeIndex][varIndex].TreeIndex == newNode);
            tree[treeIndex][varIndex] = tree[oldNode];
        }
    }
}

public abstract class TermNode : IDisposable
{
    public readonly TermTree.NodeAddr TreeIndex;
    public readonly TermTree Tree;
    public readonly int Arity;
    protected readonly TermTree.NodeAddr[] structure;
    internal int refCount;

    internal TermNode(TermTree tree, int size)
    {
        structure = size == 0 ? ([]) : (new TermTree.NodeAddr[Arity = size]);
        if (tree != null)
        {
            TreeIndex = tree.GetFreeId();
            (Tree = tree).Add(this);
        }
    }

    public void SetArg(TermTree.StructAddr index, TermNode node)
    {
        if (node is VariableTermNode { Variable: var varIndex })
            Tree.variableSubstructures[(TreeIndex, varIndex)] = (node.TreeIndex, index);
        structure[index.I] = node.TreeIndex;
        node.refCount++;
    }

    public TermNode this[TermTree.VarAddr addr]
    {
        get => Tree.nodes[Tree.variableSubstructures[(TreeIndex, addr)].TreeIndex.I];
        set
        {
            var sub = Tree.variableSubstructures[(TreeIndex, addr)];
            structure[sub.StructIndex.I] = value.TreeIndex;
            Tree.variableSubstructures[(TreeIndex, addr)] = (value.TreeIndex, sub.StructIndex);
        }
    }

    public TermNode this[TermTree.StructAddr addr]
    {
        get => Tree[structure[addr.I]];
    }

    public bool Unify(TermNode other, TermTreeSubstitutionMap map, TermTree.NodeAddr parentIndex = default, TermTree.NodeAddr otherParentIndex = default)
    {
        if (Tree != other.Tree)
            throw new InvalidOperationException();
        if (TreeIndex == other.TreeIndex)
            return true;
        var variableUnif = false;
        if (this is VariableTermNode { Variable: var varIndex })
        {
            map.Add(parentIndex, varIndex, TreeIndex, other.TreeIndex);
            variableUnif |= true;
        }
        if (other is VariableTermNode { Variable: var otherVarIndex })
        {
            map.Add(otherParentIndex, otherVarIndex, other.TreeIndex, TreeIndex);
            variableUnif |= true;
        }
        if (variableUnif)
            return true;
        if (structure.Length != other.structure.Length)
            return false;
        Debug.Assert(this is StaticTermNode);
        Debug.Assert(other is StaticTermNode);
        if (!((StaticTermNode)this).Functor.Equals(((StaticTermNode)other).Functor))
            return false;
        for (int i = 0; i < structure.Length; ++i)
        {
            var (a, b) = (Tree.nodes[structure[i].I], other.Tree.nodes[other.structure[i].I]);
            if (!a.Unify(b, map, TreeIndex, other.TreeIndex))
                return false;
        }
        return true;
    }

    public abstract ITerm ToTerm();

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        Tree.recycledIds.Enqueue(TreeIndex);
        for (int i = 0; i < structure.Length; i++)
        {
            var node = Tree.nodes[structure[i].I];
            if (node is VariableTermNode { Variable: var varIndex })
                Tree.variableSubstructures.Remove((TreeIndex, varIndex));
            if (--node.refCount == 0)
            {
                node.Dispose();
            }
        }
    }
}

internal sealed class InvalidTermNode : TermNode
{
    public InvalidTermNode() : base(null, 0) { }
    public override void Dispose() => throw new InvalidOperationException();
    public override ITerm ToTerm() => throw new InvalidOperationException();
}

public sealed class StaticTermNode(Atom value, TermTree tree, int size) : TermNode(tree, size)
{
    public readonly TermTree.ConstAddr Functor = tree.DefineConstant(value);
    public override ITerm ToTerm()
    {
        if (Arity == 0)
            return Tree[Functor];
        var args = new ITerm[Arity];
        for (int i = 0; i < structure.Length; i++)
            args[i] = tree[structure[i]].ToTerm();
        return new Complex(Tree[Functor], args);
    }
}

public sealed class VariableTermNode(Variable var, TermTree tree) : TermNode(tree, 0)
{
    public readonly TermTree.VarAddr Variable = tree.DefineVariable(var);
    public override ITerm ToTerm() => Tree[Variable];
}


public interface ITerm : IComparable<ITerm>, IEquatable<ITerm>, IExplainable
{
    Maybe<ParserScope> Scope { get; }
    bool IsGround { get; }
    bool IsQualified { get; }
    bool IsParenthesized { get; }
    IEnumerable<Variable> Variables { get; }
    TermNode ToNode(TermTree tree);

    Maybe<Atom> GetFunctor() => this switch
    {
        Atom a => a,
        Complex c => c.Functor,
        _ => Maybe<Atom>.None
    };

    ImmutableArray<ITerm> GetArguments() => this switch
    {
        Complex c => c.Arguments,
        _ => []
    };

    ITerm WithFunctor(Atom newFunctor) => this switch
    {
        Atom => newFunctor,
        Variable v => v,
        Complex c => c.WithFunctor(newFunctor),
        var x => x
    };

    ITerm WithScope(Maybe<ParserScope> newScope) => this switch
    {
        Atom a => a.WithScope(newScope),
        Variable v => v.WithScope(newScope),
        Complex c => c.WithScope(newScope),
        var x => x
    };
    ITerm AsParenthesized(bool parens) => this switch
    {
        Complex c => c.AsParenthesized(parens),
        AbstractTerm t => t.AsParenthesized(parens),
        var x => x
    };
    ITerm AsQuoted(bool quote) => this switch
    {
        Atom a => a.AsQuoted(quote),
        var x => x
    };

    ITerm Qualified(Atom m)
    {
        return GetQualification(out var head)
            .Select(some => Inner(head))
            .GetOr(Inner(this));
        ITerm Inner(ITerm t)
        {
            return new Complex(WellKnown.Functors.Module.First(), m, t)
                .AsOperator(WellKnown.Operators.NamedArgument);
        }
    }
    Maybe<Atom> GetQualification(out ITerm head)
    {
        head = this;
        if (!IsQualified || this is not Complex cplx || cplx.Arguments.Length != 2 || cplx.Arguments[0] is not Atom module)
            return default;
        head = cplx.Arguments[1];
        return Maybe.Some(module);
    }
    ITerm Substitute(Substitution s);
    ITerm Instantiate(InstantiationContext ctx, Dictionary<string, Variable> vars = null);
    ITerm Concat(params ITerm[] next)
    {
        if (this is Complex cplx)
            return cplx.WithArguments(cplx.Arguments.AddRange(next));
        if (this is Atom a)
            return new Complex(a, next);
        return this;
    }

    ITerm Substitute(IEnumerable<Substitution> subs)
    {
        if (IsGround)
            return this;
        var steps = subs.ToDictionary(s => s.Lhs, s => s.Rhs);
        var variables = Variables.Where(var => steps.ContainsKey(var));
        var @base = this;
        while (variables.Any())
        {
            foreach (var var in variables)
            {
                @base = @base.Substitute(new Substitution(var, steps[var]));
            }

            var newVariables = @base.Variables.Where(var => steps.ContainsKey(var));
            if (variables.SequenceEqual(newVariables))
                break;
            variables = newVariables;
        }
        return @base;
    }

    ITerm StripTemporaryVariables() => Substitute(Variables
        .Where(v => v.Ignored && v.Name.StartsWith("__"))
        .Select(v => new Substitution(v, WellKnown.Literals.Discard)));

    /// <summary>
    /// Two terms A and B are variants iff there exists a renaming of the variables in A that makes A equivalent (==) to B and vice versa.
    /// </summary>
    bool IsVariantOf(ITerm b)
    {
        return this.NumberVars().Equals(b.NumberVars());
    }
}

