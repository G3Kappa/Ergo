using Ergo.Lang.Ast.Terms.Interfaces;
using System.Diagnostics;

namespace Ergo.Lang.Ast;

public sealed class TermTree
{
    internal readonly List<TermNode> nodes = new() { new InvalidTermNode() };
    internal readonly Dictionary<(int TreeIndex, string VariableIndex), (int TreeIndex, int StructIndex)> variableSubstructures = new();
    internal readonly Queue<int> recycledIds = new();

    public TermNode this[int i] => nodes[i];

    public int GetFreeId() => recycledIds.TryDequeue(out var id) ? id : nodes.Count;
    internal void Add(TermNode node) => nodes[node.TreeIndex] = node;
}

public sealed class TermTreeSubstitutionMap
{
    private readonly Dictionary<int, TermTree> trees = new();
    private readonly Dictionary<(int TreeHash, int TreeIndex, string VarIndex), (int OldNode, int NewNode)> map = new();

    public void Add(TermTree tree, int treeIndex, string varIndex, int oldValue, int newValue)
    {
        var treeHash = tree.GetHashCode();
        map[(treeHash, treeIndex, varIndex)] = (oldValue, newValue);
        trees[treeHash] = tree;
    }

    public void ApplyForwards()
    {
        foreach (var ((treeHash, treeIndex, varIndex), (oldNode, newNode)) in map)
        {
            var tree = trees[treeHash];
            Debug.Assert(tree[treeIndex][varIndex].TreeIndex == oldNode);
            tree[treeIndex][varIndex] = tree[newNode];
        }
    }

    public void ApplyBackwards()
    {
        foreach (var ((treeHash, treeIndex, varIndex), (oldNode, newNode)) in map)
        {
            var tree = trees[treeHash];
            Debug.Assert(tree[treeIndex][varIndex].TreeIndex == newNode);
            tree[treeIndex][varIndex] = tree[oldNode];
        }
    }
}

public abstract class TermNode : IDisposable
{
    public readonly int TreeIndex;
    public readonly TermTree Tree;
    protected readonly int[] structure;
    internal int refCount;

    internal TermNode(TermTree tree, int size)
    {
        structure = size == 0 ? ([]) : (new int[size]);
        TreeIndex = tree.GetFreeId();
        Tree.Add(this);
    }

    public void SetArg(int index, TermNode node)
    {
        if (node is VariableTermNode { Variable.Name: var varIndex })
            Tree.variableSubstructures[(TreeIndex, varIndex)] = (node.TreeIndex, index);
        structure[index] = node.TreeIndex;
        node.refCount++;
    }

    public TermNode this[string variable]
    {
        get => Tree.nodes[Tree.variableSubstructures[(TreeIndex, variable)].TreeIndex];
        set => structure[Tree.variableSubstructures[(TreeIndex, variable)].StructIndex] = value.TreeIndex;
    }

    public bool Unify(TermNode other, TermTreeSubstitutionMap map, int parentIndex = 0, int otherParentIndex = 0)
    {
        if (TreeIndex == other.TreeIndex)
            return true;
        var variableUnif = false;
        if (this is VariableTermNode { Variable.Name: var varIndex })
        {
            map.Add(Tree, parentIndex, varIndex, TreeIndex, other.TreeIndex);
            variableUnif |= true;
        }
        if (other is VariableTermNode { Variable.Name: var otherVarIndex })
        {
            map.Add(other.Tree, otherParentIndex, otherVarIndex, other.TreeIndex, TreeIndex);
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
            var (a, b) = (Tree.nodes[structure[i]], other.Tree.nodes[other.structure[i]]);
            if (!a.Unify(b, map, TreeIndex, other.TreeIndex))
                return false;
        }
        return true;
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        Tree.recycledIds.Enqueue(TreeIndex);
        for (int i = 0; i < structure.Length; i++)
        {
            var node = Tree.nodes[structure[i]];
            if (node is VariableTermNode { Variable.Name: var varIndex })
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
}

public sealed class StaticTermNode(Atom value, TermTree tree, int size) : TermNode(tree, size)
{
    public readonly Atom Functor = value;
}

public sealed class VariableTermNode(Variable var, TermTree tree) : TermNode(tree, 0)
{
    public readonly Variable Variable = var;
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

