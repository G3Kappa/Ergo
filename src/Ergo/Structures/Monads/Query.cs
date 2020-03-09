using Ergo.Extensions.Inference;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ergo.Structures.Monads
{
    public sealed class Query
    {
        public readonly List<Goal> Goals;

        internal Query(IEnumerable<Goal> goals)
        {
            Goals = new List<Goal>(goals);
        }

        public static implicit operator Query(Goal value) => new Query(new[] { value });
        public static implicit operator Query(Goal[] values) => new Query(values);
        public static implicit operator Query(List<Goal> values) => new Query(values);
    }
}
