using Ergo.Abstractions.Inference;
using Ergo.Structures.Inference;
using Ergo.Structures.Monads;

namespace Ergo.Structures.Knowledge
{
    public readonly struct Solution
    {
        public readonly struct Binding : ICanonicalRepresentation
        {
            public readonly string VariableName;
            public readonly ITerm BoundValue;

            public Binding(string var, ITerm t)
            {
                VariableName = var;
                BoundValue = t;
            }

            public string Canonical()
            {
                return $"{VariableName} = {BoundValue.Canonical()}";
            }
        }

        public readonly Binding[] Bindings;

        public Solution(Binding[] b)
        {
            Bindings = b;
        }
    }
}