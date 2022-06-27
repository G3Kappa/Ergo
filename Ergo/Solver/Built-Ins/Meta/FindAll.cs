﻿
namespace Ergo.Solver.BuiltIns;

public sealed class FindAll : SolverBuiltIn
{
    public FindAll()
        : base("", new("findall"), 3, WellKnown.Modules.Meta)
    {
    }

    public override async IAsyncEnumerable<Evaluation> Apply(ErgoSolver solver, SolverScope scope, ITerm[] args)
    {
        scope = scope.WithDepth(scope.Depth + 1)
            .WithCaller(scope.Callee)
            .WithCallee(GetStub(args));
        if (!args[1].IsAbstract<NTuple>().TryGetValue(out var comma))
        {
            comma = new(ImmutableArray<ITerm>.Empty.Add(args[1]));
        }

        var solutions = (await solver.Solve(new(comma), scope).CollectAsync())
            .Select(s => s.Simplify())
            .ToArray();
        if (solutions.Length == 0)
        {
            if (args[2].IsGround && args[2].Equals(WellKnown.Literals.EmptyList))
            {
                yield return new Evaluation(WellKnown.Literals.True);
            }
            else if (!args[2].IsGround)
            {
                yield return True(new Substitution(args[2], WellKnown.Literals.EmptyList));
            }
            else
            {
                yield return False();
            }
        }
        else
        {
            var list = new List(ImmutableArray.CreateRange(solutions.Select(s => args[0].Substitute(s.Substitutions))));
            if (args[2].IsGround && args[2] == list.CanonicalForm)
            {
                yield return new Evaluation(WellKnown.Literals.True);
            }
            else if (!args[2].IsGround)
            {
                yield return True(new Substitution(args[2], list.CanonicalForm));
            }
            else
            {
                yield return False();
            }
        }
    }
}
