﻿using Ergo.Interpreter;
using Ergo.Lang;
using Ergo.Lang.Ast;
using System.Collections.Generic;

namespace Ergo.Solver.BuiltIns
{
    public sealed class Nonvar : BuiltIn
    {
        public Nonvar()
            : base("", new("nonvar"), Maybe<int>.Some(1), Modules.Reflection)
        {
        }

        public override IEnumerable<Evaluation> Apply(ErgoSolver solver, SolverScope scope, ITerm[] arguments)
        {
            yield return new(new Atom(arguments[0] is not Variable));
        }
    }
}