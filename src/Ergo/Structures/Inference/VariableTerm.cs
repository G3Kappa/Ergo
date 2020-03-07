using Ergo.Abstractions.Inference;
using Ergo.Structures.Monads;

namespace Ergo.Structures.Inference
{
    public readonly struct VariableTerm : ITerm
    {
        public readonly string Name { get; }

        public VariableTerm(string name)
        {
            Name = name;
        }

        Maybe<ITerm> IUnifiable<ITerm>.UnifiesWith(ITerm other)
        {
            return other switch {
                ConstantTerm _ => Maybe.Some(other),
                VariableTerm _ => Maybe.Some(other),
                ComplexTerm  _ => Maybe.Some(other),
                _ => Maybe.None
            };
        }
    }
}
