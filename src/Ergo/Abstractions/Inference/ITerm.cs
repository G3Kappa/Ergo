namespace Ergo.Abstractions.Inference
{

    public interface ITerm : IUnifiable<ITerm>, ICanonicalRepresentation
    {
        bool IsGround();
    }
}
