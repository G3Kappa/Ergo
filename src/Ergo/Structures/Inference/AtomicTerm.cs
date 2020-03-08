using Ergo.Abstractions.Inference;
using Ergo.Structures.Monads;
using System.Diagnostics;
using System.Linq;

namespace Ergo.Structures.Inference
{

    [DebuggerDisplay("{Canonical()}")]
    public readonly struct AtomicTerm : ITerm
    {
        public readonly Atom Atom;

        public AtomicTerm(Atom value)
        {
            Atom = value;
        }

        Maybe<ITerm> IUnifiable<ITerm>.UnifyWith(ITerm other)
        {
            return other switch {
                AtomicTerm k => Atom.UnifyWith(k.Atom).Map(_ => other),
                _ => Maybe.None
            };
        }
        
        public string Canonical() =>  Atom.Canonical();
        public bool IsGround() => true;
        public int Arity() => 0;
    }
}
