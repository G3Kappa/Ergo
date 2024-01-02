namespace Ergo.Lang.Ast;

public sealed class VariableTermNode(Variable var, TermCache tree) : TermNode(tree, 0)
{
    public readonly TermCache.VarAddr Variable = tree.DefineVariable(var);
    public override ITerm ToTerm() => Tree[Variable];
}

