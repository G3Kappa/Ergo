using Ergo.Abstractions.Inference;
using Ergo.Extensions.Inference;
using Ergo.Structures.Inference;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ergo.Structures.Monads
{
    public sealed class Query : IEnumerable<Goal>
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

        public IEnumerator<Goal> GetEnumerator()
        {
            return ((IEnumerable<Goal>)Goals).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<Goal>)Goals).GetEnumerator();
        }

        public override string ToString()
        {
            return String.Join(" -> ", Goals.Select(g => g.Term.Canonical()));
        }
    }
}
