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

        public static Solution.TemporaryVariable[] UnifyWith(this Solution.TemporaryVariable[] a, Solution.TemporaryVariable[] b)
        {
            var left = a.ToDictionary(a => a.RuntimeName, a => a);
            var right = new Dictionary<string, Solution.TemporaryVariable>();
            for (int i = 0; i < b.Length; i++) {
                if (left.TryGetValue(b[i].RuntimeName, out var t)) {
                    if(t.Instantiation.UnifyWith(b[i].Instantiation).TryGetValue(out var unified)) {
                        right[t.RuntimeName] = new Solution.TemporaryVariable(t.Variable, t.RuntimeName, b[i].Instantiation);
                    }
                }
            }
            return right.Values.ToArray();
        }

        public static Variable[] Variables(this Query query)
        {
            return query.Goals.SelectMany(g => g.Term.Variables()).ToArray();
        }
    }
}
