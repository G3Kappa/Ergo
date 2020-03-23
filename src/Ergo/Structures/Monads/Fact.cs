using Ergo.Abstractions.Inference;
using Ergo.Structures.Inference;

namespace Ergo.Structures.Monads
{
    public class Fact
    {
        public readonly ITerm Term;

        internal Fact(ITerm term)
        {
            Term = term;
        }

        public static implicit operator Fact(AtomicTerm value) => new Fact(value);
        public static implicit operator Fact(CompoundTerm value) => new Fact(value);

        public static readonly Fact True = From(new AtomicTerm("true")).ValueOrThrow("Unreachable");
        public static readonly Fact False = From(new AtomicTerm("false")).ValueOrThrow("Unreachable");

        public static bool IsFalse(ITerm t)
            => t.UnifyWith(False.Term).TryGetValue(out _);
        public static bool IsTrue(ITerm t)
            => t.UnifyWith(True.Term).TryGetValue(out _);

        public static Maybe<Fact> From(ITerm term)
        {
            if (term is AtomicTerm a)
                return Maybe.Some<Fact>(a);
            if (term is CompoundTerm c)
                return Maybe.Some<Fact>(c);
            return Maybe.None;
        }
    }
}
