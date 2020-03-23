using Ergo.Abstractions.Inference;
using Ergo.Structures.Monads;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ergo.Structures.Inference
{
    //[DebuggerDisplay("{Canonical()}")]
    public readonly struct CompoundTerm : ITerm
    {
        public readonly Atom Functor;
        public readonly ITerm[] Arguments;
        public readonly int Arity => Arguments.Length;


        public CompoundTerm(Atom functor, params ITerm[] args)
        {
            Functor = functor;
            Arguments = args;
        }

        Maybe<ITerm> IUnifiable<ITerm>.UnifyWith(ITerm other)
        {
            return other switch { 
                CompoundTerm c => UnifyComplex(this, c),
                _ => Maybe.None
            };

            Maybe<ITerm> UnifyComplex(CompoundTerm a, CompoundTerm b)
            {
                if (a.Arity != b.Arity)
                    return Maybe.None;
                if (!a.Functor.UnifyWith(b.Functor).TryGetValue(out var functor))
                    return Maybe.None;
                var args = new ITerm[a.Arity];
                for (int i = 0; i < a.Arity; i++) {
                    if(a.Arguments[i].UnifyWith(b.Arguments[i]).TryGetValue(out var arg)) {
                        args[i] = arg;
                    }
                    else return Maybe.None;
                }
                return Maybe.Some<ITerm>(new CompoundTerm(functor, args));
            }
        }

        public string Canonical() => $"{Functor.Canonical()}({String.Join(", ", Arguments.Select(a => a.Canonical()))})";
        public bool IsGround() => Arguments.All(arg => arg.IsGround());
    }
}
