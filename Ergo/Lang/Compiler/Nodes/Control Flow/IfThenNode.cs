﻿namespace Ergo.Lang.Compiler;

public class IfThenNode : ExecutionNode
{
    public IfThenNode(ExecutionNode condition, ExecutionNode trueBranch)
    {
        Condition = condition;
        TrueBranch = trueBranch;
    }

    public ExecutionNode Condition { get; }
    public ExecutionNode TrueBranch { get; }
    public override Action Compile(ErgoVM vm) => vm.IfThen(Condition.Compile(vm), TrueBranch.Compile(vm));
    public override IfThenNode Optimize() => new IfThenNode(Condition.Optimize(), TrueBranch.Optimize());
    public override ExecutionNode Instantiate(InstantiationContext ctx, Dictionary<string, Variable> vars = null)
    {
        return new IfThenNode(Condition.Instantiate(ctx, vars), TrueBranch.Instantiate(ctx, vars));
    }
    public override ExecutionNode Substitute(IEnumerable<Substitution> s)
    {
        return new IfThenNode(Condition.Substitute(s), TrueBranch.Substitute(s));
    }
    public override string Explain(bool canonical = false) => $"{Condition.Explain(canonical)}\r\n{("-> " + TrueBranch.Explain(canonical)).Indent(1)}";
}
