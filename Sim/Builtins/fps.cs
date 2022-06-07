﻿using Ergo.Lang;
using Ergo.Lang.Ast;
using Ergo.Lang.Extensions;
using Ergo.Solver;
using Ergo.Solver.BuiltIns;
using Raylib_cs;
using static Ergo.Lang.Ast.WellKnown;

namespace Builtins
{
    public class fps : BuiltIn
    {
        public static int Value = 60;

        public fps()
            : base("", new(nameof(fps)), Maybe.Some(1), new("ui"))
        {
        }

        public override IEnumerable<Evaluation> Apply(ErgoSolver solver, SolverScope scope, ITerm[] args)
        {
            if (args[0].Matches<int>(out var newValue))
            {
                Value = newValue;
                Raylib.SetTargetFPS(newValue);
                yield return new Evaluation(Literals.True);
                yield break;
            }
            else if (!args[0].IsGround)
            {
                if (new Substitution(args[0], TermMarshall.ToTerm(Value)).TryUnify(out var subs))
                {
                    yield return new Evaluation(Literals.True, subs.ToArray());
                    yield break;
                }
            }
            yield return new Evaluation(Literals.False);
        }
    }

}