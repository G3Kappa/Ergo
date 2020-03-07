using Ergo.Abstractions.Inference;
using Ergo.Structures.Monads;
using System.Diagnostics;

namespace Ergo.Structures.Inference
{
    [DebuggerDisplay("{CanonicalRepresentation()}")]
    public readonly struct Variable : ITerm
    {
        public readonly string Name { get; }

        public Variable(string name)
        {
            Name = name;
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
        public string CanonicalRepresentation()
        {
            return Name;
        }
    }
}
