using Ergo.Abstractions.Inference;
using Ergo.Structures.Monads;
using System.Linq;

namespace Ergo.Structures.Inference
{

    public readonly struct ConstantTerm : ITerm
    {
        public readonly Atom Value;

        public ConstantTerm(Atom value)
        {
            Value = value;
        }

        Maybe<ITerm> IUnifiable<ITerm>.UnifiesWith(ITerm other)
        {
            return other switch {
                ConstantTerm k => Value.UnifiesWith(k.Value).Map(_ => other),
                _ => Maybe.None
            };
        }
    }
}
