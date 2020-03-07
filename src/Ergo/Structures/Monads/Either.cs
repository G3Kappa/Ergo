using System;

namespace Ergo.Structures.Monads
{

    public sealed class Either<_A, _B>
    {
        public readonly Maybe<_A> A;
        public readonly Maybe<_B> B;

        public Either(_A a)
        {
            A = a;
            B = default;
        }

        public Either(_B b)
        {
            A = default;
            B = b;
        }

        public TResult Match<TResult>(Func<_A, TResult> a, Func<_B, TResult> b)
        {
            var _B = B; 
            return A.Match(a, () 
                => _B.Match(b, () 
                    => default /* Unreachable */));
        }

        public void Match(Action<_A> a, Action<_B> b)
        {
            var _B = B; A.Match(a, () 
                => _B.Match(b, () 
                    => { } /* Unreachable */));
        }

        public static implicit operator Either<_A, _B>(_A value)
        {
            return new Either<_A, _B>(value);
        }

        public static implicit operator Either<_A, _B>(_B value)
        {
            return new Either<_A, _B>(value);
        }
    }

    public static class Either
    {
        public static Either<_A, _B> A<_A, _B>(_A value)
        {
            return value;
        }
        public static Either<_A, _B> B<_A, _B>(_B value)
        {
            return value;
        }
    }
}
