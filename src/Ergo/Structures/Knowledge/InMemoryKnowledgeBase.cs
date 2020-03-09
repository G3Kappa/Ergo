using Ergo.Abstractions.Knowledge;
using Ergo.Extensions.Inference;
using Ergo.Structures.Inference;
using Ergo.Structures.Monads;
using System.Collections.Generic;
using System.Linq;

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
            AssertFirst(new Clause(Fact.True));
        }

        public void AssertFirst(Clause assertion)
        {
            _kb.Insert(0, assertion);
        }

        public void AssertLast(Clause assertion)
        {
            _kb.Add(assertion);
        }

        public IEnumerable<Clause> UnifyClauseHeads(Goal match)
        {
            return MaybeUnify(match)
                .Where(m => m.TryGetValue(out _))
                .Select(m => m.ValueOrThrow("Unreachable"));

            IEnumerable<Maybe<Clause>> MaybeUnify(Goal match)
            {
                foreach (var clause in Data) {
                    yield return match.Term
                        .UnifyWith(clause.Head.Term)
                        .Map(c => new Clause(Fact.From(c).ValueOrThrow("Unreachable"), clause.Body));
                }
            }
        }

        public Answer Solve(Query query)
        {
            var graph = new LinkedList<Solution>();
            var choicePoints = new Stack<LinkedListNode<Solution>>();
            return new Answer(query, graph, choicePoints, Solve(query, graph, choicePoints));
        }

        protected virtual IEnumerable<Solution> Solve(Query currentGoals, LinkedList<Solution> graph, Stack<LinkedListNode<Solution>> choicePoints)
        {
            // For each individual goal that makes up this query
            foreach (var goal in currentGoals.Goals) {
                // Find all clauses that match and unify them (facts are clauses with "true" as their body)
                var clauses = UnifyClauseHeads(goal)
                    .ToArray();
                // If this goal matches no known rule or fact,
                if (clauses.Length == 0) {
                    // And if we don't have any choice left, fail.
                    if (choicePoints.Count == 0) {
                        yield break;
                    }
                    // Otherwise, backtrack.
                    foreach (var s in Backtrack()) { yield return s; }
                }
                // Otherwise, for each matched clause (in the order they were matched):
                foreach (var (clause, subGoals) in clauses.ToDictionary(c => c, c => c.Body.Goals)) {
                    // Keep track of all variables for the current goal
                    var vars = Checkpoint(goal);
                    // If the clause is a fact, it means that all variables were unified and this is a solution to our goal.
                    if (goal.Constrain(clause.Head.Term, vars).TryGetValue(out var g) && vars.All(v => v.Instantiation.IsGround())) {
                        yield return new Solution(g, vars);
                    }
                    else {
                        // Solve this clause's body as if it were a query of its own
                        foreach (var ans in Solve(subGoals, graph, choicePoints)) {
                            if (((Goal)clause.Head).Constrain(clause.Head.Term, ans.Variables).TryGetValue(out var gg)) {
                                // Substitute variables with their answers
                                var dict = ans.Variables.ToDictionary(a => a.Variable.Name);
                                for (int i = 0; i < vars.Length; i++) {
                                    if(vars[i].Instantiation is Variable v && dict.TryGetValue(v.Name, out var w)) {
                                        vars[i] = new Solution.TemporaryVariable(vars[i].Variable, i, w.Instantiation);
                                    }
                                }
                            }
                        }
                        if (goal.Constrain(clause.Head.Term, vars).TryGetValue(out var ggg)) {
                            yield return new Solution(ggg, vars);
                        }
                    }
                }
            }

            IEnumerable<Solution> Backtrack()
            {
                foreach (var s in Solve(choicePoints.Pop().Value.Goal, graph, choicePoints)) {
                    yield return s;
                }
            }

            Solution.TemporaryVariable[] Checkpoint(Goal goal)
            {
                var vars = goal.TemporaryVariables();
                if(choicePoints.Count > 0) {
                    var node = graph.AddAfter(choicePoints.Peek(), new Solution(goal, vars));
                    if(node.Value.Variables.Length > 0) {
                        choicePoints.Push(node);
                    }
                }
                else {
                    var node = graph.AddFirst(new Solution(goal, vars));
                    if(node.Value.Variables.Length > 0) {
                        choicePoints.Push(node);
                    }
                }
                return vars;
            }
        }
    }
}
