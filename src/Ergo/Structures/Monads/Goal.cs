using Ergo.Abstractions.Inference;
using Ergo.Extensions.Inference;
using Ergo.Structures.Inference;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ergo.Structures.Monads
{
    [DebuggerDisplay("{Term.Canonical()}")]
    public sealed class Goal
    {
        public readonly ITerm Term;

        internal Goal(ITerm term)
        {
            Term = term;
        }

        public static implicit operator Goal(Fact value) => new Goal(value.Term);
        public static explicit operator Fact(Goal value) => Fact.From(value.Term).ValueOrThrow("Unreachable");

        public static Maybe<Goal> From(ITerm value) => Fact.From(value).Map(f => (Goal)f);

        public Solution.TemporaryVariable[] TemporaryVariables()
        {
            return _Temps(Term);
        }

        private Solution.TemporaryVariable[] _Temps(ICanonicalRepresentation t, int i = 1)
        {
            return t switch
            {
                AtomicTerm a => new[] { new Solution.TemporaryVariable(new Variable("_"), i, a) },
                Variable v => new[] { new Solution.TemporaryVariable(v, i, v) },
                CompoundTerm c => c.Arguments
                    .SelectMany((a, j) => _Temps(a, i * 100 + j))
                    .Prepend(new Solution.TemporaryVariable(new Variable("_"), i, c))
                    .ToArray(),
                Clause k => _Temps(k.Head.Term, i)
                    .Union(k.Body.Goals
                        .SelectMany((g, j) => _Temps(g.Term, i * 100 + j)))
                    .ToArray(),
                _ => Array.Empty<Solution.TemporaryVariable>()
            };
        }

        public Maybe<Goal> Constrain(ITerm k, params Solution.TemporaryVariable[] temps)
        {
            if (Term.IsGround())
                return Maybe.Some(this);
            return Term.UnifyWith(k).Map(t => {
                if(t is AtomicTerm atom) {
                    if (temps.Length != 1)
                        return Maybe.None;
                    temps[0] = new Solution.TemporaryVariable(temps[0].Variable, 0, atom);
                }
                else if(t is CompoundTerm comp) {
                    var args = _Temps(comp);
                    if (comp.Variables().Length > temps.Length)
                        return Maybe.None;
                    for (int i = 0; i < args.Length; i++) {
                        // find tmp var with same runtime name
                        for (int j = 0; j < temps.Length; j++) {
                            if (!temps[j].RuntimeName.Equals(args[i].RuntimeName))
                                continue;
                            temps[j] = temps[j].UnifyWith(args[i].Instantiation).ValueOrThrow("Constraint fail!");
                            break;
                        }
                    }
                }
                return From(t);
            }).ValueOrThrow("Solver fail!")
            .ValueOrThrow("Unification fail!");
        }
    }
}
