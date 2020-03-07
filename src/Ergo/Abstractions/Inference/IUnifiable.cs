using Ergo.Structures.Monads;

namespace Ergo.Abstractions.Inference
{
    public interface IUnifiable<T>
    {
        Maybe<T> UnifiesWith(T other);
    }
}
