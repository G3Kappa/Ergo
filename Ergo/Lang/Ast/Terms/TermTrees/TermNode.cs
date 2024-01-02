using System.Diagnostics;

namespace Ergo.Lang.Ast;

public abstract class TermNode : IDisposable
{
    public readonly TermStore.NodeAddr TreeIndex;
    public readonly TermStore Tree;
    public readonly int Arity;
    protected readonly TermStore.NodeAddr[] structure;
    internal int refCount;

    internal TermNode(TermStore tree, int size)
    {
        structure = size == 0 ? ([]) : (new TermStore.NodeAddr[Arity = size]);
        if (tree != null)
        {
            TreeIndex = tree.GetFreeId();
            (Tree = tree).Add(this);
        }
    }

    public void SetArg(TermStore.StructAddr index, TermNode node)
    {
        if (node is VariableTermNode { Variable: var varIndex })
            Tree.variableSubstructures[(TreeIndex, varIndex)] = (node.TreeIndex, index);
        structure[index.I] = node.TreeIndex;
        node.refCount++;
    }

    public TermNode this[TermStore.VarAddr addr]
    {
        get => Tree.nodes[Tree.variableSubstructures[(TreeIndex, addr)].TreeIndex.I];
        set
        {
            var sub = Tree.variableSubstructures[(TreeIndex, addr)];
            structure[sub.StructIndex.I] = value.TreeIndex;
            Tree.variableSubstructures[(TreeIndex, addr)] = (value.TreeIndex, sub.StructIndex);
        }
    }

    public TermNode this[TermStore.StructAddr addr]
    {
        get => Tree[structure[addr.I]];
    }

    public bool Unify(TermNode other, TermTreeSubstitutionMap map, TermStore.NodeAddr parentIndex = default, TermStore.NodeAddr otherParentIndex = default)
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

    public int GetVariantHashCode()
    {
        var hashCode = Arity;
        if (this is StaticTermNode { Functor: var fun })
            hashCode = HashCode.Combine(hashCode, fun.I);
        for (int i = 0; i < structure.Length; ++i)
        {
            var node = Tree.nodes[structure[i].I];
            if (node is VariableTermNode { Variable: var varIndex })
                hashCode = HashCode.Combine(hashCode, Tree.variableSubstructures[(TreeIndex, varIndex)].StructIndex.I);
            else
                hashCode = HashCode.Combine(hashCode, node.GetVariantHashCode());
        }
        return hashCode;
    }

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

