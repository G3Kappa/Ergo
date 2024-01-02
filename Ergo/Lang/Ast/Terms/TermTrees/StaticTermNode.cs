namespace Ergo.Lang.Ast;

public sealed class StaticTermNode(Atom value, TermStore tree, int size) : TermNode(tree, size)
{
    public readonly TermStore.ConstAddr Functor = tree.DefineConstant(value);
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

