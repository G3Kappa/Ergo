using Ergo.Abstractions.Inference;
using Ergo.Structures.Monads;
using System.Diagnostics;
using System.Linq;

namespace Ergo.Structures.Inference
{

    [DebuggerDisplay("{CanonicalRepresentation()}")]
    public readonly struct AtomicTerm : ITerm
    {
        public readonly Atom Value;

        public AtomicTerm(Atom value)
        {
            Value = value;
        }

        Maybe<ITerm> IUnifiable<ITerm>.UnifyWith(ITerm other)
        {
            return other switch {
                AtomicTerm k => Value.UnifyWith(k.Value).Map(_ => other),
                _ => Maybe.None
            };
        }
        
        public string CanonicalRepresentation()
        {
            return Value.CanonicalRepresentation();
        }
    }
}
