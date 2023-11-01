﻿

namespace Ergo.Solver.BuiltIns;

public sealed class Push : SolverBuiltIn
{
    public Push()
        : base("", new("push_data"), Maybe<int>.Some(1), WellKnown.Modules.CSharp)
    {
    }

    public override IEnumerable<Evaluation> Apply(SolverContext context, SolverScope scope, ImmutableArray<ITerm> args)
    {
        if (!args[0].IsGround)
        {
            scope.Throw(SolverError.TermNotSufficientlyInstantiated, args[0].Explain(true));
            yield return False();
            yield break;
        }

        context.Solver.PushData(args[0]);
        yield return True();
    }
}
