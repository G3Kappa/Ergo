using Ergo.Abstractions.Inference;
using Ergo.Extensions.Inference;
using Ergo.Structures.Inference;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ergo.Structures.Monads
{
    public sealed class Query
    {
        public readonly List<Goal> Goals;
        public bool Satisfied => Goals.All(g => g.Satisfied);

        internal Query(IEnumerable<Goal> goals)
        {
            Goals = new List<Goal>(goals);
        }

        public static implicit operator Query(Goal value) => new Query(new[] { value });
        public static implicit operator Query(Goal[] values) => new Query(values);
        public static implicit operator Query(List<Goal> values) => new Query(values);

        public Query Clone(bool preserveReferences)
        {
            var map = this.Variables()
                .ToLookup(v => v.Name, v => preserveReferences ? v.Value : new Variable(v.Name, Maybe.None));
            var newGoals = Goals
                .Select(g => Goal.From(g.Term.ReplaceArguments((i, arg) => {
                    if (arg is Variable v) {
                        return map[v.Name].Last();
                    }
                    return arg;
                })).ValueOrThrow("Solver fail!"))
                .ToList();
            return new Query(newGoals);
        }
    }
}
