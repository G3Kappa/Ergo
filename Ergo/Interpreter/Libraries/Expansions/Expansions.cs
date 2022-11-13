﻿using Ergo.Events;
using Ergo.Events.Solver;
using Ergo.Interpreter.Directives;
using Ergo.Solver;
using Ergo.Solver.BuiltIns;

namespace Ergo.Interpreter.Libraries.Expansions;

public class Expansions : Library
{
    public override Atom Module => WellKnown.Modules.Expansions;

    protected readonly Dictionary<Signature, HashSet<Expansion>> Table = new();
    public override IEnumerable<SolverBuiltIn> GetExportedBuiltins() => Enumerable.Empty<SolverBuiltIn>()
        ;
    public override IEnumerable<InterpreterDirective> GetExportedDirectives() => Enumerable.Empty<InterpreterDirective>()
        .Append(new DefineExpansion())
        ;

    public override void OnErgoEvent(ErgoEvent evt)
    {
        if (evt is SolverInitializingEvent sie)
        {
            var expansions = new Queue<Predicate>();
            var tmpScope = sie.Solver.CreateScope(sie.Scope);
            foreach (var pred in sie.Solver.KnowledgeBase.ToList())
            {
                foreach (var exp in ExpandPredicate(pred, tmpScope))
                {
                    if (!exp.Head.Equals(pred.Head) || !exp.Body.Contents.SequenceEqual(pred.Body.Contents))
                        expansions.Enqueue(exp);
                }
                if (expansions.Count > 0)
                {
                    if (!sie.Solver.KnowledgeBase.Retract(pred.Head))
                        throw new InvalidOperationException();
                    while (expansions.TryDequeue(out var exp))
                    {
                        sie.Solver.KnowledgeBase.AssertZ(exp);
                    }
                    expansions.Clear();
                }
            }
        }
        else if (evt is QuerySubmittedEvent qse)
        {
            var expansions = new Queue<Predicate>();
            var tmpScope = qse.Solver.CreateScope(qse.Scope.InterpreterScope);
            var topLevelHead = new Complex(WellKnown.Literals.TopLevel, qse.Query.Goals.Contents.SelectMany(g => g.Variables).Cast<ITerm>().ToArray());
            foreach (var match in qse.Solver.KnowledgeBase.GetMatches(qse.Scope.InstantiationContext, topLevelHead, desugar: false))
            {
                var pred = Predicate.Substitute(match.Rhs, match.Substitutions.Select(x => x.Inverted()));

                foreach (var exp in ExpandPredicate(pred, tmpScope))
                {
                    if (!exp.Equals(pred))
                        expansions.Enqueue(exp);
                }
                if (expansions.Count > 0)
                {
                    if (!qse.Solver.KnowledgeBase.Retract(pred.Head))
                        throw new InvalidOperationException();
                    while (expansions.TryDequeue(out var exp))
                    {
                        qse.Solver.KnowledgeBase.AssertZ(exp);
                    }
                    expansions.Clear();
                }
            }
        }
    }

    public IEnumerable<Expansion> GetDefinedExpansions() => Table.SelectMany(x => x.Value);
    public IEnumerable<Expansion> GetDefinedExpansions(Atom module, Signature sig) => Table.TryGetValue(sig, out var exp)
        ? exp.Where(e => e.DeclaringModule.Equals(module))
        : Enumerable.Empty<Expansion>();

    public void AddExpansion(Atom module, Variable outVar, Predicate pred)
    {
        var signature = pred.Head.GetSignature();
        if (!Table.TryGetValue(signature, out var set))
            set = Table[signature] = new();
        set.Add(new(module, outVar, pred));
    }


    // See: https://github.com/G3Kappa/Ergo/issues/36
    public IEnumerable<Predicate> ExpandPredicate(Predicate p, SolverScope scope)
    {
        // Predicates are expanded only once, when they're loaded. The same applies to queries.
        // Expansions are defined as lambdas that define a predicate and capture one variable:
        //   - The head of the predicate is matched against the current term; if they unify:
        //      - The body of the expansion is inserted in the current predicate in a sensible location;
        //      - Previous references to the term are replaced with references to the captured variable.
        foreach (var headExp in ExpandTerm(p.Head, scope))
        {
            var newHead = headExp.Reduce(e => e.Binding
                .Select(v => (ITerm)v).GetOr(e.Match), a => a);
            var headClauses = headExp.Reduce(e => e.Expansion.Contents, _ => ImmutableArray<ITerm>.Empty);
            var bodyExpansions = new List<Either<ExpansionResult, ITerm>>[p.Body.Contents.Length];
            for (int i = 0; i < p.Body.Contents.Length; i++)
            {
                bodyExpansions[i] = new();
                foreach (var bodyExp in ExpandTerm(p.Body.Contents[i], scope))
                    bodyExpansions[i].Add(bodyExp);
                if (bodyExpansions[i].Count == 0)
                    bodyExpansions[i].Add(Either<ExpansionResult, ITerm>.FromB(p.Body.Contents[i]));
            }
            var cartesian = bodyExpansions.CartesianProduct();
            foreach (var variant in cartesian)
            {
                var newBody = new List<ITerm>();
                foreach (var clause in variant)
                {
                    newBody.AddRange(clause.Reduce(e => e.Expansion.Contents, _ => Enumerable.Empty<ITerm>()));
                    newBody.Add(clause.Reduce(e => e.Binding.Select(x => (ITerm)x).GetOr(e.Match), a => a));
                }
                newBody.AddRange(headClauses);
                yield return new Predicate(
                    p.Documentation,
                    p.DeclaringModule,
                    newHead,
                    new(newBody),
                    p.IsDynamic,
                    p.IsExported
                );
            }
        }

        IEnumerable<Either<ExpansionResult, ITerm>> ExpandTerm(ITerm term, SolverScope scope)
        {
            if (term is Variable)
                yield break;
            foreach (var exp in GetExpansions(term, scope)
                .Select(x => Either<ExpansionResult, ITerm>.FromA(x))
                .DefaultIfEmpty(Either<ExpansionResult, ITerm>.FromB(term)))
            {
                // If this is a complex term, expand all of its arguments recursively and produce a combination of all solutions
                if (exp.Reduce(e => e.Match, a => a) is Complex cplx)
                {
                    var expansions = new List<Either<ExpansionResult, ITerm>>[cplx.Arity];
                    for (var i = 0; i < cplx.Arity; i++)
                    {
                        expansions[i] = new();
                        foreach (var argExp in ExpandTerm(cplx.Arguments[i], scope))
                            expansions[i].Add(argExp);
                        if (expansions[i].Count == 0)
                            expansions[i].Add(Either<ExpansionResult, ITerm>.FromB(cplx.Arguments[i]));
                    }
                    var cartesian = expansions.CartesianProduct();
                    foreach (var argList in cartesian)
                    {
                        // TODO: This might mess with abstract forms!
                        var newCplx = cplx.WithArguments(argList
                            .Select(x => x.Reduce(exp => exp.Binding
                               .Select(v => (ITerm)v).GetOr(exp.Match), a => a))
                               .ToImmutableArray());
                        var expClauses = new NTuple(
                            exp.Reduce(e => e.Expansion.Contents, _ => Enumerable.Empty<ITerm>())
                               .Concat(argList.SelectMany(x => x
                                  .Reduce(e => e.Expansion.Contents, _ => Enumerable.Empty<ITerm>()))));
                        yield return Either<ExpansionResult, ITerm>.FromA(new(newCplx, expClauses, exp.Reduce(e => e.Binding, _ => default)));
                    }
                }
                else yield return exp;
            }
        }

        IEnumerable<ExpansionResult> GetExpansions(ITerm term, SolverScope scope)
        {
            var sig = term.GetSignature();
            // Try all modules in import order
            var modules = scope.InterpreterScope.GetVisibleModules();
            foreach (var mod in modules.Reverse())
            {
                scope = scope.WithModule(mod.Name);
                foreach (var exp in GetDefinedExpansions(mod.Name, sig))
                {
                    // [Output] >> (head :- body(Output)).
                    if (!exp.Predicate.Head.Unify(term).TryGetValue(out var subs))
                        continue;
                    var pred = Predicate.Substitute(exp.Predicate, subs);
                    // Instantiate the OutVariable, but leave the others intact
                    var vars = new Dictionary<string, Variable>();
                    foreach (var var in pred.Head.Variables.Concat(pred.Body.CanonicalForm.Variables)
                        .Where(var => !var.Name.Equals(exp.OutVariable.Name)))
                    {
                        vars[var.Name] = var;
                    }
                    pred = pred.Instantiate(scope.InstantiationContext, vars);
                    yield return new(pred.Head, pred.Body, vars[exp.OutVariable.Name]);
                }
            }
        }
    }


}