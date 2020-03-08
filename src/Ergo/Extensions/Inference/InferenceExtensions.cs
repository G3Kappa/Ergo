using Ergo.Abstractions.Inference;
using Ergo.Structures.Inference;
using Ergo.Structures.Monads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ergo.Extensions.Inference
{
    public static class ITermExtensions
    {
        public static Variable[] Variables(this ICanonicalRepresentation obj)
        {
            return obj switch
            {
                CompoundTerm c => c.Arguments.SelectMany(a => a.Variables()).ToArray(),
                Variable v => new[] { v },
                Clause k => k.Head.Term.Variables().Union(k.Body.Goals.SelectMany(g => g.Term.Variables())).ToArray(),
                _ => Array.Empty<Variable>()
            };
        }

        public static Variable[] Variables(this Query query)
        {
            return query.Goals.SelectMany(g => g.Term.Variables()).ToArray();
        }
    }
}
