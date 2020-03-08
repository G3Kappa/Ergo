using Ergo.Abstractions.Inference;
using Ergo.Structures.Monads;
using System;
using System.Diagnostics;

namespace Ergo.Structures.Inference
{
    [DebuggerDisplay("{Canonical()}")]
    public readonly struct Variable : ITerm
    {
        public readonly string Name { get; }

        public Variable(string name)
        {
            // _    = anonymous discard
            // _Var = ignored singleton
            if (name == "_") {
                Name = $"__S{Guid.NewGuid().GetHashCode()}";
            }
            else {
                Name = name;
            }
        }

        Maybe<ITerm> IUnifiable<ITerm>.UnifyWith(ITerm other)
        {
            return other switch {
                AtomicTerm _ => Maybe.Some(other),
                Variable _ => Maybe.Some(other),
                CompoundTerm  _ => Maybe.Some(other),
                _ => Maybe.None
            };
        }
        public string Canonical() => Name;
        public bool IsGround() => false;
    }
}
