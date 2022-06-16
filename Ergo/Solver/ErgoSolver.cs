﻿using Ergo.Interpreter;
using Ergo.Shell;
using Ergo.Solver.BuiltIns;

namespace Ergo.Solver;

public partial class ErgoSolver : IDisposable
{
    protected volatile bool Cut = false;
    protected volatile CancellationTokenSource ExceptionCts;

    public readonly SolverFlags Flags;
    public readonly ShellScope ShellScope;
    public readonly InterpreterScope InterpreterScope;
    public readonly Dictionary<Signature, BuiltIn> BuiltIns;
    public readonly ErgoInterpreter Interpreter;

    public readonly KnowledgeBase KnowledgeBase;
    public readonly HashSet<Atom> DataSinks = new();
    public readonly Dictionary<Signature, HashSet<DataSource>> DataSources = new();

    public event Action<SolverTraceType, string> Trace;
    public event Action<ErgoSolver, ITerm> DataPushed;
    public event Action<ErgoSolver> Disposing;

    public ErgoSolver(ErgoInterpreter i, InterpreterScope interpreterScope, KnowledgeBase kb, SolverFlags flags = SolverFlags.Default, ShellScope shellScope = default)
    {
        Interpreter = i;
        Flags = flags;
        KnowledgeBase = kb;
        InterpreterScope = interpreterScope;
        ShellScope = shellScope;
        BuiltIns = new();
        AddBuiltInsByReflection();
    }

    public void Throw(Exception e)
    {
        ShellScope.ExceptionHandler.Throw(ShellScope, e);
        ExceptionCts.Cancel(false);
    }

    public static Signature GetDataSignature<T>(Maybe<Atom> functor = default)
        where T : new()
    {
        var term = TermMarshall.ToTerm(new T());
        var signature = term.GetSignature();
        signature = signature.Tag.HasValue && WellKnown.Functors.Dict.Contains(signature.Functor)
            ? functor.Reduce(some => signature.WithTag(Maybe.Some(some)), () => signature)
            : functor.Reduce(some => signature.WithFunctor(some), () => signature);
        return signature;
    }

    public void BindDataSource<T>(DataSource<T> data)
        where T : new()
    {
        var signature = GetDataSignature<T>(Maybe.Some(data.Functor)).WithModule(Maybe.None<Atom>());
        if (!DataSources.TryGetValue(signature, out var hashSet))
        {
            DataSources[signature] = hashSet = new();
        }

        hashSet.Add(data.Source);
    }

    public void BindDataSink<T>(DataSink<T> sink)
        where T : new()
    {
        DataSinks.Add(sink.Functor);
        sink.Connect(this);
        Disposing += _ =>
        {
            sink.Disconnect(this);
            DataSinks.Remove(sink.Functor);
        };
    }

    public bool RemoveDataSources<T>(Atom functor)
        where T : new()
    {
        var signature = GetDataSignature<T>(Maybe.Some(functor));
        if (DataSources.TryGetValue(signature, out var hashSet))
        {
            hashSet.Clear();
            DataSources.Remove(signature);
            return true;
        }

        return false;
    }

    public void PushData(ITerm data) => DataPushed?.Invoke(this, data);

    public bool TryAddBuiltIn(BuiltIn b) => BuiltIns.TryAdd(b.Signature, b);

    protected void AddBuiltInsByReflection()
    {
        var assembly = typeof(Write).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsAssignableTo(typeof(BuiltIn))) continue;
            if (!type.GetConstructors().Any(c => c.GetParameters().Length == 0)) continue;
            var inst = (BuiltIn)Activator.CreateInstance(type);
            BuiltIns[inst.Signature] = inst;
        }
    }

    public async IAsyncEnumerable<KnowledgeBase.Match> GetDataSourceMatches(ITerm head)
    {
        // Allow enumerating all data sources by binding to a variable
        if (head is Variable)
        {
            foreach (var sig in DataSources.Keys)
            {
                var anon = sig.Arity.Reduce(some => sig.Functor.BuildAnonymousTerm(some), () => new Dict(sig.Tag.GetOrThrow()).CanonicalForm);
                if (!head.Unify(anon).TryGetValue(out var subs))
                {
                    continue;
                }

                await foreach (var item in GetDataSourceMatches(anon.Substitute(subs)))
                {
                    yield return item;
                }
            }

            yield break;
        }

        var signature = head.GetSignature();
        // Return results from data sources 
        if (DataSources.TryGetValue(signature.WithModule(Maybe.None<Atom>()), out var sources))
        {
            signature = DataSources.Keys.Single(k => k.Equals(signature));
            foreach (var source in sources)
            {
                await foreach (var item in source)
                {
                    var predicate = new Predicate(
                        "data source",
                        Modules.CSharp,
                        item.WithFunctor(signature.Tag.Reduce(some => some, () => signature.Functor)),
                        Lang.Ast.Tuple.Empty,
                        dynamic: true
                    );
                    if (predicate.Unify(head).TryGetValue(out var matchSubs))
                    {
                        predicate = Predicate.Substitute(predicate, matchSubs);
                        yield return new KnowledgeBase.Match(head, predicate, matchSubs);
                    }
                    else if (source.Reject(item))
                    {
                        break;
                    }
                }
            }
        }
    }

    protected async IAsyncEnumerable<Evaluation> ResolveGoal(ITerm qt, SolverScope scope, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var any = false;
        var sig = qt.GetSignature();
        if (!qt.TryGetQualification(out var qm, out var term))
        {
            // Try resolving the built-in's module automatically
            foreach (var key in BuiltIns.Keys)
            {
                if (!InterpreterScope.IsModuleVisible(key.Module.GetOrDefault()))
                    continue;
                var withoutModule = key.WithModule(default);
                if (withoutModule.Equals(sig) || withoutModule.Equals(sig.WithArity(Maybe<int>.None)))
                {
                    term.TryQualify(key.Module.GetOrDefault(), out qt);
                    sig = key;
                    break;
                }
            }
        }

        while (BuiltIns.TryGetValue(sig, out var builtIn)
        || BuiltIns.TryGetValue(sig = sig.WithArity(Maybe<int>.None), out builtIn))
        {
            LogTrace(SolverTraceType.Resv, $"{{{sig.Explain()}}} {qt.Explain()}", scope.Depth);
            ct.ThrowIfCancellationRequested();
            ExceptionCts.Token.ThrowIfCancellationRequested();
            var args = term.Reduce(
                a => Array.Empty<ITerm>(),
                v => Array.Empty<ITerm>(),
                c => c.Arguments
            );
            await foreach (var eval in builtIn.Apply(this, scope, args))
            {
                LogTrace(SolverTraceType.Resv, $"\t-> {eval.Result.Explain()} {{{string.Join("; ", eval.Substitutions.Select(s => s.Explain()))}}}", scope.Depth);
                ct.ThrowIfCancellationRequested();
                qt = eval.Result;
                sig = qt.GetSignature();
                yield return eval;
                any = true;
            }
        }

        if (!any) yield return new(qt);
    }

    private void LogTrace(SolverTraceType type, ITerm term, int depth = 0) => LogTrace(type, term.Explain(), depth);

    private void LogTrace(SolverTraceType type, string s, int depth = 0) => Trace?.Invoke(type, $"{type}: ({depth:00}) {s}");

    public async Task<Maybe<ITerm>> TryExpandTerm(ITerm term, SolverScope scope = default, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested || ExceptionCts.Token.IsCancellationRequested)
        {
            return default;
        }

        if (term is Variable)
            return default;
        var sig = term.GetSignature();
        // Try all modules in import order
        var modules = InterpreterScope.GetLoadedModules();
        foreach (var mod in modules.Reverse())
        {
            if (!mod.Expansions.TryGetValue(sig, out var expansions))
                continue;
            scope = scope.WithModule(mod.Name);
            foreach (var exp in expansions)
            {
                if (!exp.Head.Unify(term).TryGetValue(out var subs))
                    continue;
                if (!exp.Value.Variables.Contains(WellKnown.Literals.ExpansionOutput))
                    return Maybe.Some(exp.Value);
                // Assume exp.Value is callable
                var query = exp.Value.Substitute(subs);
                await foreach (var sol in Solve(new Query(query), Maybe.Some(scope)))
                {
                    if (ct.IsCancellationRequested || ExceptionCts.Token.IsCancellationRequested)
                    {
                        return default;
                    }

                    if (!sol.Simplify().Links.Value.TryGetValue(WellKnown.Literals.ExpansionOutput, out var expanded))
                    {
                        if (!sol.Substitutions.Any(s => s.Rhs.Equals(WellKnown.Literals.ExpansionOutput)))
                        {
                            Throw(new SolverException(SolverError.ExpansionLacksEvalVariable, scope));
                            return default;
                        }

                        return Maybe.Some<ITerm>(WellKnown.Literals.Discard);
                    }

                    return Maybe.Some(expanded);
                }
            }
        }

        if (term is Complex cplx)
        {
            var newArgs = new List<ITerm>();
            foreach (var arg in cplx.Arguments)
            {
                var exp = await TryExpandTerm(arg, scope, ct: ct);
                newArgs.Add(exp.Reduce(some => some, () => arg));
            }

            return Maybe.Some<ITerm>(cplx.WithArguments(newArgs.ToArray()));
        }

        return default;
    }

    protected async IAsyncEnumerable<Solution> Solve(SolverScope scope, ITerm goal, List<Substitution> subs = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested || ExceptionCts.Token.IsCancellationRequested)
        {
            yield break;
        }

        subs ??= new List<Substitution>();
        // Treat comma-expression complex ITerms as proper expressions
        if (Lang.Ast.Tuple.TryUnfold(goal, out var expr))
        {
            await foreach (var s in Solve(scope, new Lang.Ast.Tuple(expr), subs, ct: ct))
            {
                yield return s;
            }

            Cut = false;
            yield break;
        }
        // If goal resolves to a builtin, it is called on the spot and its solutions enumerated (usually just ⊤ or ⊥, plus a list of substitutions)
        // If goal does not resolve to a builtin it is returned as-is, and it is then matched against the knowledge base.
        await foreach (var resolvedGoal in ResolveGoal(goal, scope, ct: ct))
        {
            if (ct.IsCancellationRequested || ExceptionCts.Token.IsCancellationRequested)
            {
                yield break;
            }

            if (resolvedGoal.Result.Equals(WellKnown.Literals.False) || resolvedGoal.Result is Variable)
            {
                LogTrace(SolverTraceType.Retn, "⊥", scope.Depth);
                yield break;
            }

            if (resolvedGoal.Result.Equals(WellKnown.Literals.True))
            {
                LogTrace(SolverTraceType.Retn, $"⊤ {{{string.Join("; ", subs.Select(s => s.Explain()))}}}", scope.Depth);
                yield return new Solution(subs.Concat(resolvedGoal.Substitutions).ToArray());
                if (goal.Equals(WellKnown.Literals.Cut))
                {
                    Cut = true;
                    yield break;
                }

                continue;
            }

            var expandedGoal = resolvedGoal.Result;
            // Cyclic literal definitions throw an error, so this replacement loop always terminates
            while (await TryExpandTerm(expandedGoal, scope, ct: ct) is { HasValue: true } exp)
            {
                if (ct.IsCancellationRequested || ExceptionCts.Token.IsCancellationRequested)
                {
                    yield break;
                }

                var newGoal = exp.GetOrDefault();
                if (newGoal.Unify(expandedGoal).Reduce(some => !some.Any(), () => false))
                    break;
                expandedGoal = newGoal;
            }
            // Attempts qualifying a goal with a module, then finds matches in the knowledge base
            var (qualifiedGoal, matches) = QualifyGoal(InterpreterScope.Modules[InterpreterScope.Module], expandedGoal);
            LogTrace(SolverTraceType.Call, qualifiedGoal, scope.Depth);
            foreach (var m in matches)
            {
                var innerScope = scope.WithDepth(scope.Depth + 1)
                    .WithModule(m.Rhs.DeclaringModule)
                    .WithCallee(scope.Callee)
                    .WithCaller(m.Rhs);
                var solve = Solve(innerScope, m.Rhs.Body, new List<Substitution>(m.Substitutions.Concat(resolvedGoal.Substitutions)), ct: ct);
                await foreach (var s in solve)
                {
                    LogTrace(SolverTraceType.Exit, m.Rhs.Head, innerScope.Depth);
                    yield return s;
                }

                if (Cut)
                {
                    Cut = false;
                    yield break;
                }
            }
        }

        (ITerm Qualified, IEnumerable<KnowledgeBase.Match> Matches) QualifyGoal(Module module, ITerm goal)
        {
            var matches = KnowledgeBase.GetMatches(goal);
            if (matches.Any())
            {
                return (goal, matches);
            }

            var isDynamic = false;
            if (!goal.IsQualified)
            {
                if (goal.TryQualify(scope.Module, out var qualified)
                    && ((isDynamic |= module.DynamicPredicates.Contains(qualified.GetSignature())) || true))
                {
                    matches = KnowledgeBase.GetMatches(qualified);
                    if (matches.Any())
                    {
                        return (qualified, matches);
                    }
                }

                if (scope.Callers.Length > 0 && scope.Callers.First() is { } clause)
                {
                    if (goal.TryQualify(clause.DeclaringModule, out qualified)
                        && ((isDynamic |= InterpreterScope.Modules[clause.DeclaringModule].DynamicPredicates.Contains(qualified.GetSignature())) || true))
                    {
                        matches = KnowledgeBase.GetMatches(qualified);
                        if (matches.Any())
                        {
                            return (qualified, matches);
                        }
                    }
                }
            }

            var signature = goal.GetSignature();
            var dynModule = signature.Module.Reduce(some => some, () => scope.Module);
            if (!KnowledgeBase.TryGet(signature, out var predicates) && !(isDynamic |= InterpreterScope.Modules.TryGetValue(dynModule, out var m) && m.DynamicPredicates.Contains(signature)))
            {
                if (Flags.HasFlag(SolverFlags.ThrowOnPredicateNotFound))
                {
                    Throw(new SolverException(SolverError.UndefinedPredicate, scope, signature.Explain()));
                    return (goal, Enumerable.Empty<KnowledgeBase.Match>());
                }
            }

            return (goal, Enumerable.Empty<KnowledgeBase.Match>());
        }
    }

    // TODO: Figure out cuts once and for all
    protected async IAsyncEnumerable<Solution> Solve(SolverScope scope, Lang.Ast.Tuple query, List<Substitution> subs = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        subs ??= new List<Substitution>();
        if (query.IsEmpty)
        {
            yield return new Solution(subs.ToArray());
            yield break;
        }

        var goals = query.Contents;
        var subGoal = goals.First();
        goals = goals.RemoveAt(0);
        Cut = false;
        // Get first solution for the current subgoal
        await foreach (var s in Solve(scope, subGoal, subs, ct: ct))
        {
            var rest = new Lang.Ast.Tuple(goals.Select(x => x.Substitute(s.Substitutions)));
            await foreach (var ss in Solve(scope, rest, subs, ct: ct))
            {
                yield return new Solution(s.Substitutions.Concat(ss.Substitutions).Distinct().ToArray());
            }

            if (Cut)
            {
                yield break;
            }
        }
    }

    public IAsyncEnumerable<Solution> Solve(Query goal, Maybe<SolverScope> scope = default, CancellationToken ct = default)
    {
        ExceptionCts = new();
        return Solve(scope.Reduce(some => some, () => new SolverScope(0, InterpreterScope.Module, default, ImmutableArray<Predicate>.Empty)), goal.Goals, ct: ct);
    }

    public void Dispose()
    {
        Disposing?.Invoke(this);
        GC.SuppressFinalize(this);
    }
}
