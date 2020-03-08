using Ergo.Abstractions.Inference;
using Ergo.Structures.Monads;
using System;
using System.Diagnostics;
using System.Linq;

namespace Ergo.Structures.Inference
{
    [DebuggerDisplay("{Canonical()}")]
    public readonly struct Solution : ICanonicalRepresentation
    {
        public readonly Goal Goal;
        public readonly TemporaryVariable[] Variables;

        public Solution(Goal g, params TemporaryVariable[] vars)
        {
            Goal = g;
            Variables = vars
                .Where(v => !v.Variable.Name.StartsWith("_"))
                .ToArray();
        }

        public string Canonical()
        {
            if(Variables.Length == 0) {
                return Goal.Term.Canonical();
            }
            return String.Join(", ", Variables
                .Select(v => $"{v.Variable.Name} = {v.Instantiation.Canonical()}"));
        }

        public readonly struct TemporaryVariable
        {
            public readonly string RuntimeName;
            public readonly Variable Variable;
            public readonly ITerm Instantiation;

            public TemporaryVariable(Variable v, int i, ITerm inst)
            {
                Variable = v;
                Instantiation = inst;
                RuntimeName = $"__G{i}";
            }

            private TemporaryVariable(Variable v, string n, ITerm inst)
            {
                Variable = v;
                Instantiation = inst;
                RuntimeName = n;
            }

            public Maybe<TemporaryVariable> UnifyWith(ITerm newValue)
            {
                var _variable = Variable; var _name = RuntimeName;
                return Instantiation.UnifyWith(newValue)
                    .Match(v => Maybe.Some(new TemporaryVariable(_variable, _name, v)), () => Maybe.None);
            }
        }
    }
}
