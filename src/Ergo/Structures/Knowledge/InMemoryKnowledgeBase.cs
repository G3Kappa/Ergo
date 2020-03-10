using Ergo.Abstractions.Inference;
using Ergo.Abstractions.Knowledge;
using Ergo.Extensions.Inference;
using Ergo.Structures.Inference;
using Ergo.Structures.Monads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Ergo.Structures.Knowledge
{
    public class InMemoryKnowledgeBase : IKnowledgeBase
    {
        private readonly IList<Clause> _kb;
        public IReadOnlyList<Clause> Data
            => (IReadOnlyList<Clause>)_kb;

        public InMemoryKnowledgeBase()
        {
            _kb = new List<Clause>();
        }

        public void AssertFirst(Clause assertion)
        {
            _kb.Insert(0, assertion);
        }

        public void AssertLast(Clause assertion)
        {
            _kb.Add(assertion);
        }

        internal IEnumerable<Clause> UnifyClauses(Goal g)
        {
            return MaybeUnify()
                .Where(m => m.TryGetValue(out _))
                .Select(m => m.ValueOrThrow("Unreachable"));

            IEnumerable<Maybe<Clause>> MaybeUnify()
            {
                foreach (var clause in Data) {
                    yield return g.Clone(false).Term
                        .UnifyWith(clause.Head.Term)
                        .Map(c => ReplaceVariables(clause, Fact.From(c).ValueOrThrow("Unreachable")));
                }
            }
        }

        public static Clause ReplaceVariables(Clause left, Fact newHead)
        {
            var headArgs = left.Head.Term.Arguments();
            var clauseArgs = newHead.Term.Arguments();
            var map = new Dictionary<string, ITerm>();
            for (int i = 0; i < headArgs.Length; i++) {
                if (headArgs[i] is Variable @var) {
                    map[@var.Name] = clauseArgs[i];
                }
            }
            var newGoals = left.Body.Goals
                .Select(g => g.Term.ReplaceArguments((i, a) => a switch {
                    Variable v when !v.Instantiated && map.TryGetValue(v.Name, out var val) => val,
                    Variable v when v.Instantiated => v.Value,
                    _ => a
                }))
                .Select(t => Goal.From(t).ValueOrThrow("Solver fail!"))
                .ToArray();
            return new Clause(newHead, newGoals);
        }

        public Maybe<List<Clause>> SolveGoal(Goal goal)
        {
            var solutions = new List<Clause>();
            var matches = UnifyClauses(goal)
                .ToList();
            foreach (var clause in matches) {
                bool failed = false;
                foreach (var sub in clause.Body.Goals) {
                    if(SolveGoal(sub).TryGetValue(out _)) {

                    }
                    else {
                        failed = true;
                        break;
                    }
                }
                if(!failed) {
                    solutions.Add(clause);
                }
            }
            /* Backtrack */
            if(solutions.Any()) {
                return Maybe.Some(solutions);
            }
            return Maybe.None;
        }

    }
}