using Ergo.Abstractions.Inference;
using Ergo.Abstractions.Knowledge;
using Ergo.Exceptions;
using Ergo.Extensions.Inference;
using Ergo.Parser;
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
            //AssertFirst(ErgolParser.Parse("", ErgolParser.TryParseClause));
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
            var any = false;
            var gFunc = g.Term switch { CompoundTerm c => c.Functor, AtomicTerm a => a.Atom, _ => null };
            foreach (var clause in Data) {
                var hFunc = clause.Head.Term switch { CompoundTerm c => c.Functor, AtomicTerm a => a.Atom, _ => null };
                any |= gFunc.UnifyWith(hFunc).TryGetValue(out _);
                if (g.Term.UnifyWith(clause.Head.Term).TryGetValue(out var u)) {
                    yield return ReplaceVariables(clause, Fact.From(u).ValueOrThrow(""));
                }
            }
            if(!any)
                throw new UndefinedPredicateException(new Clause(g, new List<Goal>()).Canonical());
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
                    Variable v => v,
                    _ => new Variable("_", Maybe.Some(a))
                }))
                .Select(t => Goal.From(t).ValueOrThrow(""))
                .ToList();

            var current = new SolutionGraph.Node(query, parent);
            for (int i = 0; i < query.Goals.Count; ++i) {
                var goal = query.Goals[i];
                if (Fact.IsFalse(goal.Term)) {
                    return null;
                }
                else if (Fact.IsTrue(goal.Term)) {
                    current.Children.Add(new SolutionGraph.Node(goal, parent));
                    continue;
                }
                var matches = UnifyClauses(goal)
                    .ToList();
                if (matches.Count == 0)
                    return null;

                bool shouldBreak = false;
                foreach (var match in matches) {
                    if (match.Factual) {
                        var node = new SolutionGraph.Node(match.Head, current);
                        // replace the variables that were bound by node in a copy of query
                        // and solve it as a child of current
                        var subQuery = query.Skip(i + 1).ToList();
                        var newBody = ReplaceVariables(new Clause(match.Head, subQuery), (Fact)match.Head)
                            .Body;
                        if (newBody.Count() > 0) {
                            if (Solve(newBody, node) is { } s) {
                                node.Children.Add(s);
                            }
                        }
                        current.Children.Add(node);
                        shouldBreak = true;
                    }
                    else if (Solve(match.Body, current) is { } s) {
                        current.Children.Add(s);
                    }
                }
                if (shouldBreak) break;
            }
            return current;
        }

        public SolutionGraph Solve(Query query)
        {
            var graph = new SolutionGraph(Solve(query, null) ?? new SolutionGraph.Node(query));
            return graph;
        }
    }

}

