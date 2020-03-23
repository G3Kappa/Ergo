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

        public static readonly Goal False = From(Fact.False.Term).ValueOrThrow("Unreachable");
        public static readonly Goal True = From(Fact.True.Term).ValueOrThrow("Unreachable");

        internal Goal(ITerm term)
        {
            Term = term;
        }

        public static implicit operator Goal(Fact value) => new Goal(value.Term);
        public static explicit operator Fact(Goal value) => Fact.From(value.Term).ValueOrThrow("Unreachable");

        public static Maybe<Goal> From(ITerm value) => Fact.From(value).Map(f => (Goal)f);
    }
}
