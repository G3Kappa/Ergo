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

    public class SolutionGraph
    {
        public readonly Node Root;
        public SolutionGraph(Node root)
        {
            Root = root;
        }

        public class Node
        {
            public readonly Node Parent;
            public readonly Query Value;
            public readonly List<Node> Children;

            public Node(Query query, Node parent = null)
            {
                Parent = parent;
                Value = query;
                Children = new List<Node>();
            }
            public override string ToString()
            {
                return Value.ToString();
            }
        }

        public IEnumerable<Solution> Solutions()
        {
            // Walk the graph and enumerate all solutions
            return SolutionsRec(Root);

            IEnumerable<Solution> SolutionsRec(Node node, Dictionary<string, ITerm> variables = null)
            {
                if(variables is null) {
                    variables = node.Value.Variables()
                        .GroupBy(v => v.Name).Select(g => g.First())
                        .ToDictionary(v => v.Name, v => (ITerm)v);
                }
                else if(variables.All(t => t.Value.IsGround())) {
                    yield return new Solution(variables.Select(v => new Solution.Binding(v.Key, v.Value)).ToArray());
                }

                foreach (var c in node.Children) {
                    var cVars = c.Value.Variables()
                        .GroupBy(v => v.Name).Select(g => g.First())
                        .ToDictionary(v => v.Name);
                    var newVars = variables
                        .ToDictionary(v => v.Key, v => cVars.TryGetValue(v.Key, out var cV) ? cV.Value : v.Value);
                    foreach (var s in SolutionsRec(c, newVars))
                        yield return s;
                }

            }
        }
    }

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
            // Replace all arguments of g with variables
            foreach (var clause in Data) {
                if (g.Term.UnifyWith(clause.Head.Term).TryGetValue(out var u)) {
                    yield return ReplaceVariables(clause, Fact.From(u).ValueOrThrow(""));
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
                    Variable v => v.Value,
                    _ => a
                }))
                .Select(t => Goal.From(t).ValueOrThrow("Solver fail!"))
                .ToArray();
            return new Clause(newHead, newGoals);
        }

        private SolutionGraph.Node Solve(Query query, SolutionGraph.Node parent)
        {
            query = query.Goals
                .Select(g => g.Term.ReplaceArguments((i, a) => a switch {
                    Variable v when v.Instantiated => v.Value,
                    _ => a
                }))
                .Select(t => Goal.From(t).ValueOrThrow(""))
                .ToList();

            var current = new SolutionGraph.Node(query, parent);
            for (int i = 0; i < query.Goals.Count; ++i) {
                var goal = query.Goals[i];
                var matches = UnifyClauses(goal)
                    .ToList();
                if (matches.Count == 0) return new SolutionGraph.Node(query, parent);
                if (matches.Count == 1 && goal.Satisfied) {
                    if(i == query.Goals.Count - 1)
                        current.Children.Add(new SolutionGraph.Node(query, parent));
                    continue;
                }

                bool shouldBreak = false;
                foreach (var match in matches) {
                    if (match.Factual) {
                        var node = new SolutionGraph.Node(match.Head, current);
                        // replace the variables that were bound by node in a copy of query
                        // and solve it as a child of current
                        var subQuery = query.Skip(i + 1).ToList();
                        var newBody = ReplaceVariables(new Clause(match.Head, subQuery), (Fact)match.Head)
                            .Body;
                        if(newBody.Count() > 0) {
                            node.Children.Add(Solve(newBody, node));
                        }
                        current.Children.Add(node);
                        shouldBreak = true;
                    }
                    else {
                        current.Children.Add(Solve(match.Body, current));
                    }
                }
                if (shouldBreak) break;
            }
            return current;
        }

        public SolutionGraph Solve(Query query)
        {
            return new SolutionGraph(Solve(query, null));
        }

    }
}