using Ergo.Abstractions.Inference;
using Ergo.Structures.Monads;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Ergo.Structures.Inference
{

    public readonly struct ComplexTerm : ITerm
    {
        public readonly Atom Functor;
        public readonly ITerm[] Arguments;
        public readonly int Arity => Arguments.Length;


        public ComplexTerm(Atom functor, params ITerm[] args)
        {
            Functor = functor;
            Arguments = args;
        }

        Maybe<ITerm> IUnifiable<ITerm>.UnifiesWith(ITerm other)
        {
            return other switch { 
                ComplexTerm c => UnifyComplex(this, c),
                _ => Maybe.None
            };

            Maybe<ITerm> UnifyComplex(ComplexTerm a, ComplexTerm b)
            {
                if (a.Arity != b.Arity)
                    return Maybe.None;
                if (!a.Functor.UnifiesWith(b.Functor).TryGetValue(out _))
                    return Maybe.None;
                for (int i = 0; i < a.Arity; i++) {
                    if(!a.Arguments[i].UnifiesWith(b.Arguments[i]).TryGetValue(out _))
                        return Maybe.None;
                }
                return Maybe.Some((ITerm)b);
            }
        }
    }
}
