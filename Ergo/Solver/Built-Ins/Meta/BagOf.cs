﻿using Ergo.Interpreter;
using Ergo.Lang;
using Ergo.Lang.Ast;
using Ergo.Lang.Extensions;
using System.Collections.Generic;
using System.Linq;

namespace Ergo.Solver.BuiltIns
{

    public sealed class BagOf : SolutionAggregationBuiltIn
    {
        public BagOf()
            : base("", new("bagof"), Maybe.Some(3), Modules.Meta)
        {
        }

        public override IEnumerable<Evaluation> Apply(ErgoSolver solver, SolverScope scope, ITerm[] args)
        {
            var any = false;
            foreach (var (ArgVars, ListTemplate) in AggregateSolutions(solver, scope, args, out var listVars))
            {
                if(!new Substitution(listVars.Root, ArgVars).TryUnify(out var listSubs)
                || !new Substitution(args[2], ListTemplate.Root).TryUnify(out var instSubs))
                {
                    yield return new(WellKnown.Literals.False);
                    yield break;
                }
                yield return new(WellKnown.Literals.True, listSubs.Concat(instSubs).ToArray());
                any = true;
            }
            if(!any)
            {
                yield return new(WellKnown.Literals.False);
            }
        }
    }
}
