﻿namespace Ergo.Solver.BuiltIns;

public sealed class AssertA : DynamicPredicateBuiltIn
{
    public AssertA()
        : base("", new("asserta"), 1)
    {
    }

    public override async IAsyncEnumerable<Evaluation> Apply(SolverContext context, SolverScope scope, ITerm[] arguments)
    {
        if (Assert(context.Solver, scope, arguments[0], z: false))
        {
            yield return new(WellKnown.Literals.True);
        }
        else
        {
            yield return new(WellKnown.Literals.False);
        }
    }
}
