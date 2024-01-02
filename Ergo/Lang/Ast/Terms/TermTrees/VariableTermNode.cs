namespace Ergo.Lang.Ast;

public sealed class VariableTermNode(Variable var, TermStore tree) : TermNode(tree, 0)
{
    public readonly TermStore.VarAddr Variable = tree.DefineVariable(var);
    public override ITerm ToTerm() => Tree[Variable];
}

