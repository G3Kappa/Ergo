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
}
