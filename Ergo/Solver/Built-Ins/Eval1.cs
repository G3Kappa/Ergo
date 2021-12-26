﻿using Ergo.Lang;
using Ergo.Lang.Ast;

namespace Ergo.Solver.BuiltIns
{
    public sealed class Eval1 : MathBuiltIn
    {
        public Eval1()
            : base("", new("@eval"), Maybe<int>.Some(1))
        {
        }

        public override Evaluation Apply(ErgoSolver solver, SolverScope scope, ITerm[] arguments)
        {
            return new(new Lang.Ast.Atom(Eval(arguments[0])));
        }
    }
}
