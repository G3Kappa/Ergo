using Ergo.Abstractions.Inference;

namespace Ergo.Structures.Knowledge
{
    public readonly partial struct Solution
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
    }
}