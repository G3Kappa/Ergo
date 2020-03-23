using Ergo.Structures.Inference;

namespace Ergo.Exceptions
{
    public class VariableUninstantiatedException : ErgoException
    {
        public readonly Variable Variable;

        public VariableUninstantiatedException(Variable v) : base(v.Name)
        {
        }
    }

    public class UndefinedPredicateException : ErgoException
    {
        public readonly string Canonical;

        public UndefinedPredicateException(string canonical) : base($"Unknown procedure: {canonical}")
        {
            Canonical = canonical;
        }
    }

}
