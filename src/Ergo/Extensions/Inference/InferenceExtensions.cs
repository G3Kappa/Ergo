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

        public static ITerm[] Arguments(this ITerm t)
        {
            return t switch
            {
                CompoundTerm c => c.Arguments,
                _ => Array.Empty<ITerm>()
            };
        }

        public static ITerm ReplaceArguments(this ITerm t, Func<int, ITerm, ITerm> replace)
        {
            if(t is CompoundTerm c) {
                var args = new ITerm[c.Arguments.Length];
                for (int i = 0; i < c.Arguments.Length; i++) {
                    args[i] = replace(i, c.Arguments[i]).ReplaceArguments(replace);
                }
                return new CompoundTerm(c.Functor, args);
            }
            return t;
        }

        public static Variable[] Variables(this Query query)
        {
            return query.Goals.SelectMany(g => g.Term.Variables()).ToArray();
        }
    }
}
