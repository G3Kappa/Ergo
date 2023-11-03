﻿using Ergo.Solver;

namespace Ergo.Lang.Compiler;

/// <summary>
/// Represents a logical disjunction.
/// </summary>
public class BranchNode : ExecutionNode
{
    public readonly ExecutionNode Left;
    public readonly ExecutionNode Right;

    public BranchNode(ExecutionNode left, ExecutionNode right)
    {
        Left = left;
        Right = right;
    }

    public override ExecutionNode Optimize()
    {
        var left = Left.Optimize();
        var right = Right.Optimize();
        if (left is FalseNode)
            return right;
        if (right is FalseNode)
            return left;
        return new BranchNode(left, right);
    }

    public override IEnumerable<ExecutionScope> Execute(SolverContext ctx, SolverScope solverScope, ExecutionScope execScope)
    {
        var cut = false;
        foreach (var res in Left.Execute(ctx, solverScope, execScope.ChoicePoint()))
        {
            yield return res;
            cut |= res.IsCut;
        }
        if (cut) yield break;
        foreach (var res in Right.Execute(ctx, solverScope, execScope.ChoicePoint()))
        {
            yield return res;
        }
    }
    public override ExecutionNode Instantiate(InstantiationContext ctx, Dictionary<string, Variable> vars = null)
    {
        return new BranchNode(Left.Instantiate(ctx, vars), Right.Instantiate(ctx, vars));
    }

    public override ExecutionNode Substitute(IEnumerable<Substitution> s)
    {
        return new BranchNode(Left.Substitute(s), Right.Substitute(s));
    }
    public override string Explain(bool canonical = false) => $"( {Left.Explain(canonical)}\r\n; {Right.Explain(canonical)} )";
}