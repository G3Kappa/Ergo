using Ergo.Events;
using Ergo.Events.Interpreter;
using Ergo.Events.Runtime;
using Ergo.Interpreter.Directives;
using Ergo.Lang.Ast.Terms.Interfaces;
using Ergo.Runtime.BuiltIns;

namespace Ergo.Interpreter.Libraries.Expansions;

public class Expansions : Library
{
    private static readonly InstantiationContext InstantiationContext = new("X");

    public override int LoadOrder => 1;

    public override Atom Module => WellKnown.Modules.Expansions;

    protected readonly Dictionary<Signature, HashSet<Expansion>> Table = new();
    public override IEnumerable<BuiltIn> GetExportedBuiltins() => Enumerable.Empty<BuiltIn>()
        ;
    public override IEnumerable<InterpreterDirective> GetExportedDirectives() => Enumerable.Empty<InterpreterDirective>()
        .Append(new DefineExpansion())
        ;

    public override void OnErgoEvent(ErgoEvent evt)
    {
        if (evt is KnowledgeBaseCreatedEvent kce)
        {
            var expansions = new Queue<Predicate>();
            foreach (var pred in kce.KnowledgeBase.ToList())
            {
                foreach (var exp in ExpandPredicate(pred, kce.KnowledgeBase.Scope))
                {
                    if (!exp.IsSameDefinitionAs(pred))
                        expansions.Enqueue(exp);
                }
                if (expansions.Count > 0)
                {
                    // TODO: Verify if this makes sense, consider implementing AssertAfter(Predicate, Predicate) to maintain order
                    if (!kce.KnowledgeBase.Retract(pred))
                        throw new InvalidOperationException();
                    while (expansions.TryDequeue(out var exp))
                    {
                        kce.KnowledgeBase.Retract(exp);
                        kce.KnowledgeBase.AssertZ(exp);
                    }
                    expansions.Clear();
                }
            }
        }
        if (evt is QuerySubmittedEvent qse)
        {
            var expansions = new Queue<Predicate>();
            var topLevelHead = new Complex(WellKnown.Literals.TopLevel, qse.Query.Goals.Contents.SelectMany(g => g.Variables).Distinct().Cast<ITerm>().ToArray());
            foreach (var match in qse.VM.KB.GetMatches(qse.VM.InstantiationContext, topLevelHead, desugar: false)
                .AsEnumerable().SelectMany(x => x))
            {
                var pred = match.Predicate.Substitute(match.Substitutions);
                foreach (var exp in ExpandPredicate(pred, qse.VM.KB.Scope))
                {
                    if (!exp.IsSameDefinitionAs(pred))
                        expansions.Enqueue(exp);
                }
                if (expansions.Count > 0)
                {
                    // TODO: Verify if this makes sense, consider implementing AssertAfter(Predicate, Predicate) to maintain order
                    if (!qse.VM.KB.Retract(pred))
                        throw new InvalidOperationException();
                    while (expansions.TryDequeue(out var exp))
                    {
                        qse.VM.KB.Retract(exp);
                        qse.VM.KB.AssertZ(exp);
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
    public IEnumerable<Predicate> ExpandPredicate(Predicate p, InterpreterScope scope)
    {
        Predicate currentPredicate = p;
        while (true)
        {
            var newPredicate = default(Predicate);
            foreach (var expanded in ExpandPredicateOnce(currentPredicate))
            {
                newPredicate = expanded;
                yield return expanded;
            }
            if (newPredicate.Equals(default) || newPredicate.IsSameDefinitionAs(currentPredicate))
            {
                // No further expansion possible
                break;
            }
            currentPredicate = newPredicate;
        }
        IEnumerable<Predicate> ExpandPredicateOnce(Predicate p)
        {
            if (p.IsBuiltIn)
            {
                yield return p;
                yield break;
            }
            // Predicates are expanded only once, when they're loaded. The same applies to queries.
            // Expansions are defined as lambdas that define a predicate and capture one variable:
            //   - The head of the predicate is matched against the current term; if they unify:
            //      - The body of the expansion is inserted in the current predicate in a sensible location;
            //      - Previous references to the term are replaced with references to the captured variable.
            // TODO: Sensible location means that an expansion should be placed right above the line that invokes it.
            foreach (var headExp in ExpandTerm(p.Head, scope))
            {
                var newHead = headExp.Reduce(e => e.Binding
                    .Select(v => (ITerm)v).GetOr(e.Match), a => a);
                var headClauses = headExp.Reduce(e => e.Expansion.Contents, _ => ImmutableArray<ITerm>.Empty);
                var bodyExpansions = new List<Either<ExpansionResult, ITerm>>[p.Body.Contents.Length];
                for (int i = 0; i < p.Body.Contents.Length; i++)
                {
                    bodyExpansions[i] = [.. ExpandTerm(p.Body.Contents[i], scope)];
                    if (bodyExpansions[i].Count == 0)
                        bodyExpansions[i].Add(Either<ExpansionResult, ITerm>.FromB(p.Body.Contents[i]));
                }
                var cartesian = bodyExpansions.CartesianProduct();
                foreach (var variant in cartesian)
                {
                    var newBody = new List<ITerm>();
                    // We need to add each head clause in a "sensible" place within the body of the predicate
                    newBody.AddRange(headClauses);
                    foreach (var clause in variant)
                    {
                        newBody.AddRange(clause.Reduce(e => e.Expansion.Contents, _ => Enumerable.Empty<ITerm>()));
                        newBody.Add(clause.Reduce(e => e.Binding.Select(x => (ITerm)x).GetOr(e.Match), a => a));
                    }
                    yield return new Predicate(
                        p.Documentation,
                        p.Module,
                        newHead,
                        new(newBody, p.Head.Scope),
                        p.IsDynamic,
                        p.IsExported,
                        default
                    );
                }
            }
        }
    }

    public IEnumerable<Either<ExpansionResult, ITerm>> ExpandTerm(ITerm term, InterpreterScope scope)
    {
        if (term is Variable)
            yield break;
        // If this is an abstract term we expand it through its canonical form, then we parse the result again
        // through its owner parser.
        if (term is AbstractTerm abs)
        {
            foreach (var exp in abs.Expand(this, scope))
            {
                yield return exp;
            }
            yield break;
        }
        foreach (var exp in GetExpansions(term, scope)
            .Select(x => Either<ExpansionResult, ITerm>.FromA(x))
            .DefaultIfEmpty(Either<ExpansionResult, ITerm>.FromB(term)))
        {
            var t = exp.Reduce(e => e.Match, a => a);
            // If this is a complex term, expand all of its arguments recursively and produce a combination of all solutions
            if (t is Complex cplx)
            {
                var expansions = new List<Either<ExpansionResult, ITerm>>[cplx.Arity];
                for (var i = 0; i < cplx.Arity; i++)
                {
                    expansions[i] = [.. ExpandTerm(cplx.Arguments[i], scope).Distinct()];
                    if (expansions[i].Count == 0)
                        expansions[i].Add(Either<ExpansionResult, ITerm>.FromB(cplx.Arguments[i]));
                }
                var cartesian = expansions.CartesianProduct();
                var isLambda = WellKnown.Functors.Lambda.Contains(cplx.Functor) && cplx.Arity == 2
                    && cplx.Arguments[0] is Lang.Ast.List;
                foreach (var argList in cartesian)
                {
                    var newCplx = cplx
                        .WithArguments(argList
                        .Select(x => x.Reduce(exp => exp.Binding
                           .Select(v => (ITerm)v).GetOr(exp.Match), a => a))
                           .ToImmutableArray());
                    var expClauses = new NTuple(
                        exp.Reduce(e => e.Expansion.Contents, _ => Enumerable.Empty<ITerm>())
                           .Concat(argList.SelectMany(x => x
                              .Reduce(e => e.Expansion.Contents, _ => Enumerable.Empty<ITerm>()))),
                        cplx.Scope);
                    if (isLambda)
                    {
                        // Stuff 'expClauses' inside the lambda instead of returning them to the parent predicate
                        var body = newCplx.Arguments[1];
                        var closure = new NTuple(expClauses.Contents.Append(body), cplx.Scope);
                        newCplx = newCplx.WithArguments(newCplx.Arguments.SetItem(1, closure));
                        yield return Either<ExpansionResult, ITerm>.FromB(newCplx);
                    }
                    else
                    {
                        yield return Either<ExpansionResult, ITerm>.FromA(new(newCplx, expClauses, exp.Reduce(e => e.Binding, _ => default)));
                    }
                }
            }
            else
            {
                yield return exp;
            }
        }
    }

    public IEnumerable<ExpansionResult> GetExpansions(ITerm term, InterpreterScope scope)
    {
        var sig = term.GetSignature();
        // Try all modules in import order
        var modules = scope.VisibleModules;
        foreach (var mod in modules.Reverse())
        {
            foreach (var exp in GetDefinedExpansions(mod, sig))
            {
                // Example expansion:
                // [Output] >> (head(A) :- body(A, Output)).
                // 1. Instantiate expansion, keep track of Output:
                // [__K1] >> (head(__K2) :- body(__K2, __K1)).
                // Output = __K1
                // 2. Unify term with expansion head:
                // head(__K2) / head(test)
                // __K2 = test
                // 3. Substitute expansion:
                // [__K1] >> (head(test) :- body(test, __K1)).
                var expVars = new Dictionary<string, Variable>();
                var expInst = exp.Predicate.Instantiate(InstantiationContext, expVars);
                if (!LanguageExtensions.Unify(term, expInst.Head).TryGetValue(out var subs))
                    continue;
                var pred = expInst.Substitute(subs);
                yield return new(pred.Head, pred.Body, expVars[exp.OutVariable.Name]);
            }
        }
    }


}
