namespace Ergo.Abstractions.Inference
{
    public interface ITerm : IUnifiable<ITerm>
    {
        string CanonicalRepresentation();
    }
}
