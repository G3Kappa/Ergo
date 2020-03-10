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
        public bool Satisfied => Term.IsGround();

        internal Goal(ITerm term)
        {
            Term = term;
        }

        public static implicit operator Goal(Fact value) => new Goal(value.Term);
        public static explicit operator Fact(Goal value) => Fact.From(value.Term).ValueOrThrow("Unreachable");

        public static Maybe<Goal> From(ITerm value) => Fact.From(value).Map(f => (Goal)f);

        public Goal Clone(bool preserveReferences)
        {
            var map = Term.Variables()
                .ToLookup(v => v.Name, v => !preserveReferences
                    ? new Variable(v.Name, Maybe.None)
                    : v);
            return From(Term.ReplaceArguments((i, arg) => arg switch {
                Variable v => map[v.Name].First(),
                _ => arg
            })).ValueOrThrow("Unreachable");
        }
    }
}
