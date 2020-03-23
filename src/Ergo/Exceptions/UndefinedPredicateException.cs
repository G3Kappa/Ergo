namespace Ergo.Exceptions
{
    public class UndefinedPredicateException : ErgoException
    {
        public readonly string Canonical;

        public UndefinedPredicateException(string canonical) : base($"Unknown procedure: {canonical}")
        {
            Canonical = canonical;
        }
    }

}
