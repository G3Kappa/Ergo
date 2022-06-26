﻿using Ergo.Interpreter;

namespace Ergo.Solver.BuiltIns;

public sealed class Nonvar : SolverBuiltIn
{
    public Nonvar()
        : base("", new("nonvar"), Maybe<int>.Some(1), WellKnown.Modules.Reflection)
    {
    }

    public override async IAsyncEnumerable<Evaluation> Apply(ErgoSolver solver, SolverScope scope, ITerm[] arguments)
    {
        yield return new(new Atom(arguments[0] is not Variable));
    }
}
