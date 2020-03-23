using Ergo.Abstractions.Inference;
using Ergo.Structures.Inference;
using Ergo.Structures.Monads;
using System;
using System.Linq;

namespace Ergo.Structures.Knowledge
{
    public readonly partial struct Solution : ICanonicalRepresentation
    {
        public readonly Binding[] Bindings;

        public Solution(Binding[] b)
        {
            Bindings = b;
        }

        public string Canonical()
        {
            return String.Join(", ", 
                Bindings.Select(v => v.Canonical()));
        }
    }
}