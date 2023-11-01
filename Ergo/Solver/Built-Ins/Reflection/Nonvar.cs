﻿namespace Ergo.Solver.BuiltIns;

public sealed class Nonvar : SolverBuiltIn
{
    public Nonvar()
        : base("", new("nonvar"), Maybe<int>.Some(1), WellKnown.Modules.Reflection)
    {
    }

    public override IEnumerable<Evaluation> Apply(SolverContext context, SolverScope scope, ImmutableArray<ITerm> arguments)
    {
        yield return Bool(arguments[0] is not Variable);
    }
}
