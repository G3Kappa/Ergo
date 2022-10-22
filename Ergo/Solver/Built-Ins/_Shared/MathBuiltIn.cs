﻿
using PeterO.Numbers;

namespace Ergo.Solver.BuiltIns;

public abstract class MathBuiltIn : SolverBuiltIn
{
    protected MathBuiltIn(string documentation, Atom functor, Maybe<int> arity)
        : base(documentation, functor, arity, WellKnown.Modules.Math)
    {
    }
    public dynamic Evaluate(ErgoSolver solver, SolverScope scope, ITerm t)
    {
        return Evaluate(solver, t);
        dynamic Evaluate(ErgoSolver solver, ITerm t)
        {
            if (t is Atom a) { return a.Value is EDecimal d ? d : Throw(a); }
            if (t is not Complex c) { return Throw(t); }

            return c.Functor switch
            {
                var f when c.Arguments.Length == 1 && f.Equals(Signature.Functor)
                => Evaluate(solver, c.Arguments[0]),
                var f when c.Arguments.Length == 2 && WellKnown.Functors.Gt.Contains(f)
                => Evaluate(solver, c.Arguments[0]).CompareTo(Evaluate(solver, c.Arguments[1])) > 0,
                var f when c.Arguments.Length == 2 && WellKnown.Functors.Gte.Contains(f)
                => Evaluate(solver, c.Arguments[0]).CompareTo(Evaluate(solver, c.Arguments[1])) >= 0,
                var f when c.Arguments.Length == 2 && WellKnown.Functors.Lt.Contains(f)
                => Evaluate(solver, c.Arguments[0]).CompareTo(Evaluate(solver, c.Arguments[1])) < 0,
                var f when c.Arguments.Length == 2 && WellKnown.Functors.Lte.Contains(f)
                => Evaluate(solver, c.Arguments[0]).CompareTo(Evaluate(solver, c.Arguments[1])) <= 0,
                var f when c.Arguments.Length == 2 && WellKnown.Functors.Modulo.Contains(f)
                => Evaluate(solver, c.Arguments[0]) % Evaluate(solver, c.Arguments[1]),
                var f when c.Arguments.Length == 2 && WellKnown.Functors.Addition.Contains(f)
                => Evaluate(solver, c.Arguments[0]) + Evaluate(solver, c.Arguments[1]),
                var f when c.Arguments.Length == 2 && WellKnown.Functors.Subtraction.Contains(f)
                => Evaluate(solver, c.Arguments[0]) - Evaluate(solver, c.Arguments[1]),
                var f when c.Arguments.Length == 2 && WellKnown.Functors.Multiplication.Contains(f)
                => Evaluate(solver, c.Arguments[0]) * Evaluate(solver, c.Arguments[1]),
                var f when c.Arguments.Length == 2 && WellKnown.Functors.Division.Contains(f)
                => Evaluate(solver, c.Arguments[0]) / Evaluate(solver, c.Arguments[1]),
                var f when c.Arguments.Length == 2 && WellKnown.Functors.IntDivision.Contains(f)
                => (((EDecimal)Evaluate(solver, c.Arguments[0])).DivideToIntegerNaturalScale(Evaluate(solver, c.Arguments[1]))),
                var f when c.Arguments.Length == 2 && WellKnown.Functors.Power.Contains(f)
                => ((EDecimal)Evaluate(solver, c.Arguments[0])).Pow(Evaluate(solver, c.Arguments[1])),
                var f when c.Arguments.Length == 1 && WellKnown.Functors.SquareRoot.Contains(f)
                => ((EDecimal)Evaluate(solver, c.Arguments[0])).Sqrt(null),
                var f when c.Arguments.Length == 1 && WellKnown.Functors.Minus.Contains(f)
                => -Evaluate(solver, c.Arguments[0]),
                var f when c.Arguments.Length == 1 && WellKnown.Functors.Plus.Contains(f)
                => +Evaluate(solver, c.Arguments[0]),
                _ => Throw(c)
            };
        }

        double Throw(ITerm t) => throw new SolverException(SolverError.ExpectedTermOfTypeAt, scope, WellKnown.Types.Number, t.Explain());
    }
}
