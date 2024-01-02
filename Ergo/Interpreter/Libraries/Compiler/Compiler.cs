using Ergo.Events;
using Ergo.Events.Runtime;
using Ergo.Interpreter.Directives;
using Ergo.Runtime.BuiltIns;

namespace Ergo.Interpreter.Libraries.Compiler;

using Ergo.Events.Interpreter;
using Ergo.Lang.Compiler;
using Ergo.Lang.Exceptions.Handler;
using Ergo.Runtime;
using System.Collections.Generic;

public class Compiler : Library
{
    public override int LoadOrder => 100;

    public Compiler()
    {

    }

    protected readonly HashSet<Signature> InlinedPredicates = new();
    public override Atom Module => WellKnown.Modules.Compiler;
    public override IEnumerable<BuiltIn> GetExportedBuiltins() { yield break; }
    public override IEnumerable<InterpreterDirective> GetExportedDirectives() { yield break; }
    public void AddInlinedPredicate(Signature sig)
    {
        InlinedPredicates.Add(sig);
    }

    static Maybe<Predicate> TryCompile(Predicate clause, ExceptionHandler handler, DependencyGraph depGraph, bool optimize)
    {
        return handler.TryGet(() =>
        {
            var graph = clause.ToExecutionGraph(depGraph);
            if (optimize)
                graph = graph.Optimized();
            return clause.WithExecutionGraph(graph);
        });
    }

    public override void OnErgoEvent(ErgoEvent evt)
    {
        if (evt is KnowledgeBaseCreatedEvent kbc)
        {
            // This library reacts last to ErgoEvents (in a standard environment), so all predicates
            // that have been marked for inlining are known by now. It's time for some static analysis.
            var depGraph = kbc.KnowledgeBase.DependencyGraph;
            depGraph.Rebuild();
            // The concept is similar to term expansions, but instead of recursively expanding each term,
            // inlining works at the goal level, allowing it to be qualified with a module and thus be more specific.
            foreach (var root in depGraph.GetRootNodes())
            {
                ProcessNode(root);
            }
            if (kbc.Flags.HasFlag(CompilerFlags.EnableInlining))
            {
                foreach (var inlined in InlineInContext(kbc.KnowledgeBase.Scope, depGraph))
                {
                    foreach (var (pred, clause) in inlined.Clauses.Zip(inlined.InlinedClauses))
                    {
                        kbc.KnowledgeBase.Replace(pred, clause);
                    }
                    inlined.Clauses.Clear();
                    inlined.Clauses.AddRange(inlined.InlinedClauses);
                }
            }
            foreach (var node in depGraph.GetAllNodes())
            {
                for (int i = 0; i < node.Clauses.Count; i++)
                {
                    var clause = node.Clauses[i];
                    if (clause.IsBuiltIn || clause.ExecutionGraph.TryGetValue(out _))
                        continue;
                    if (TryCompile(clause, kbc.KnowledgeBase.Scope.ExceptionHandler, depGraph, optimize: true).TryGetValue(out var newClause))
                    {
                        newClause.ExecutionGraph.Do(x => x.Compile());
                        // Hooks can be defined in non-existent modules, but otherwise we want to cache the compiled predicate.
                        if (kbc.KnowledgeBase.Scope.Modules.TryGetValue(newClause.Module, out var module))
                            module.Program.KnowledgeBase
                                 .Replace(clause, newClause);
                        kbc.KnowledgeBase.Replace(clause, newClause);
                        node.Clauses[i] = newClause;
                    }
                }
            }
        }
        else if (evt is QuerySubmittedEvent qse)
        {
            var topLevelHead = new Complex(WellKnown.Literals.TopLevel, qse.Query.Goals.Contents.SelectMany(g => g.Variables).Distinct().Cast<ITerm>().ToArray());
            foreach (var match in qse.VM.KB.GetMatches(qse.VM.InstantiationContext, topLevelHead, desugar: false)
                .AsEnumerable().SelectMany(x => x))
            {
                var topLevel = match.Predicate;
                if (TryCompile(topLevel, qse.VM.KB.Scope.ExceptionHandler, qse.VM.KB.DependencyGraph, qse.Flags.HasFlag(CompilerFlags.EnableOptimizations)).TryGetValue(out var newClause))
                    qse.VM.KB.Replace(topLevel, newClause);
                else
                    qse.VM.KB.Replace(topLevel, topLevel.WithExecutionGraph(new ExecutionGraph(topLevel.Head, FalseNode.Instance)));
            }
        }

        void ProcessNode(DependencyGraphNode node, HashSet<Signature> visited = null)
        {
            visited ??= new HashSet<Signature>();
            if (visited.Contains(node.Signature))
            {
                node.IsCyclical = true;
                return; // Mark as cyclical and return. Don't throw error here.
            }
            node.InlinedClauses ??= new(node.Clauses);
            visited.Add(node.Signature);
            foreach (var child in node.Dependents.Cast<DependencyGraphNode>())
            {
                var copy = visited.ToHashSet();
                ProcessNode(child, copy);
            }
            // Only inline if not cyclical and if marked for inlining.
            if (!node.IsInlined && !node.IsCyclical && InlinedPredicates.Contains(node.Signature))
            {
                node.IsInlined = true;
            }
        }
    }

    public IEnumerable<DependencyGraphNode> InlineInContext(InterpreterScope scope, DependencyGraph graph)
    {
        foreach (var node in graph.GetRootNodes()
            .SelectMany(r => InlineNodeWithContext(scope, r)))
        {
            yield return node;
        }
    }

    IEnumerable<DependencyGraphNode> InlineNodeWithContext(InterpreterScope scope, DependencyGraphNode node, DependencyGraphNode dependent, HashSet<Signature> processed = null)
    {
        processed ??= new HashSet<Signature>();
        processed.Add(node.Signature);
        if (node.IsInlined)
        {
            // At this point we should take a step back and see if other nodes that 'dependent' depends on may be confused for this one.
            // In that case they should either be merged, being mindful of which ones should be inlined and which ones shouldn't, or they should not be inlined at all.
            // An example comes when overloading an operator, such as =/2 or :=/2. In particular =/2 is overloaded by the dict module.
            // The result is that when the dict module's definition matches, the default implementation is ignored, cut away.
            // These semantics should be preserved when inlining =/2 and similar predicates. A naive approach might elide other definitions.
            // A smart approach will inline them individually and merge them as a disjunction. A pragmatic approach will just avoid this head scratcher.
            var lookup = dependent.Dependencies
                .Except([node])
                .ToLookup(x => x.Signature.WithModule(default));
            var sig = node.Signature.WithModule(default);
            if (lookup.Contains(sig))
            {
                var clashes = lookup[sig];
                // If we're inlining prologue:=/2, then clashes will contain dict:=/2.
                // If we can unambiguously tell which definition is being called by dependent, we can inline that definition only.
                // Otherwise we'll take the pragmatic path since inlining disjunctions is as efficient as regular matching.
                yield break;
            }
            else
            {
                foreach (var ret in Inline(node, dependent))
                    yield return ret;
            }
        }
        if (!processed.Contains(dependent.Signature))
        {
            foreach (var inner in InlineNodeWithContext(scope, dependent, processed))
                yield return inner;
        }


        IEnumerable<DependencyGraphNode> Inline(DependencyGraphNode node, DependencyGraphNode dependent)
        {
            if (node.InlinedClauses.Count == 1)
            {
                var inlined = node.InlinedClauses.Single();
                foreach (var ret in InlineInner(inlined, dependent))
                    yield return ret;
            }
            else
            {
                // Inlining predicates with multiple clauses is a bit hairier.
                // The bulk of the work is done by Predicate.InlineHead, which turns all arguments into variables
                // and moves their unification to a precondition in the body. This normalizes all variants to the same form.
                var normalized = node.InlinedClauses.Select(x => Predicate.InlineHead(new("_TMP"), x)).ToList();
                var newBody = normalized
                    .Select(g => (ITerm)g.Body)
                     .Aggregate((a, b) => WellKnown.Operators.Disjunction.ToComplex(a, Maybe.Some(b)));
                // We can pick any one normalized head to act as the "most general"
                var inlined = normalized[0].WithBody(new NTuple(new[] { newBody }));
                foreach (var ret in InlineInner(inlined, dependent))
                    yield return ret;
            }
        }

        IEnumerable<DependencyGraphNode> InlineInner(Predicate inlined, DependencyGraphNode dependent)
        {
            var newClauses = new List<Predicate>();
            foreach (var clause in dependent.InlinedClauses)
            {
                if (clause.IsBuiltIn)
                {
                    newClauses.Add(clause);
                    continue;
                }
                var newBody = Predicate.ExpandGoals(clause.Body, g =>
                {
                    if (g.Unify(inlined.Head).TryGetValue(out var subs))
                        return inlined.Body.Substitute(subs);
                    return g;
                });
                // ExpandGoals already removes empty statements (true), but at the end it may return an empty list.
                if (newBody.Contents.Length == 1 && newBody.Contents.Single().Equals(WellKnown.Literals.EmptyCommaList))
                    // In that case the predicate simply turns into a fact.
                    newBody = new NTuple(new ITerm[] { WellKnown.Literals.True });
                newClauses.Add(clause.WithBody(newBody));
            }
            dependent.InlinedClauses.Clear();
            dependent.InlinedClauses.AddRange(newClauses);
            yield return dependent;
        }
    }

    IEnumerable<DependencyGraphNode> InlineNodeWithContext(InterpreterScope scope, DependencyGraphNode node, HashSet<Signature> processed = null)
    {
        processed ??= new HashSet<Signature>();
        foreach (var inline in node.Dependents.Cast<DependencyGraphNode>()
            .SelectMany(d => InlineNodeWithContext(scope, node, d, processed)))
        {
            yield return inline;
        }
    }
}