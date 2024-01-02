using Ergo.Interpreter;
using System.Collections;
using System.Collections.Specialized;

namespace Ergo.Lang;

public partial class KnowledgeBase : IReadOnlyCollection<Predicate>
{
    protected readonly OrderedDictionary Predicates = new();

    public readonly InterpreterScope Scope;
    public readonly DependencyGraph DependencyGraph;

    public KnowledgeBase(InterpreterScope scope)
    {
        Scope = scope;
        DependencyGraph = new(this);
    }

    private KnowledgeBase(InterpreterScope scope, OrderedDictionary predicates, DependencyGraph dependencyGraph)
    {
        Scope = scope;
        Predicates = predicates;
        DependencyGraph = dependencyGraph;
    }

    public int Count => Predicates.Values.Cast<List<Predicate>>().Sum(l => l.Count);
    public void Clear() => Predicates.Clear();

    public KnowledgeBase Clone()
    {
        var inner = new OrderedDictionary();
        foreach (DictionaryEntry kv in Predicates)
        {
            inner.Add(kv.Key, kv.Value);
        }
        return new(Scope, inner, DependencyGraph);
    }

    private List<Predicate> GetOrCreate(Signature key, bool append = false)
    {
        if (!Predicates.Contains(key))
        {
            if (append)
            {
                Predicates.Add(key, new List<Predicate>());
            }
            else
            {
                Predicates.Insert(0, key, new List<Predicate>());
            }
        }

        return (List<Predicate>)Predicates[key];
    }

    private Maybe<List<Predicate>> GetImpl(Signature key)
    {
        if (Predicates.Contains(key))
        {
            return Maybe.Some((List<Predicate>)Predicates[key]);
        }
        return default;
    }

    public Maybe<IList<Predicate>> Get(Signature sig)
    {
        // Direct match
        if (GetImpl(sig).TryGetValue(out var list))
            return Maybe.Some<IList<Predicate>>(list);
        // Variadic match (write/*, call/*)
        if (GetImpl(sig.WithArity(default)).TryGetValue(out list))
            return Maybe.Some<IList<Predicate>>(list);
        // Matching exported predicates with qualification
        if (sig.Module.TryGetValue(out var module) && Get(sig.WithModule(default)).TryGetValue(out var list_))
            return Maybe.Some<IList<Predicate>>(list_.Where(p => p.IsExported || p.Module.Equals(module)).ToArray());
        return default;
    }

    public Maybe<IEnumerable<KBMatch>> GetMatches(InstantiationContext ctx, ITerm goal, bool desugar)
    {
        if (desugar)
        {
            // if head is in the form predicate/arity (or its built-in equivalent),
            // do some syntactic de-sugaring and convert it into an actual anonymous complex
            if (goal is Complex c
                && WellKnown.Functors.Division.Contains(c.Functor))
            {
                if (c.Matches(out var match, new { Predicate = default(string), Arity = default(int) }))
                {
                    goal = new Atom(match.Predicate).BuildAnonymousTerm(match.Arity);
                }
            }
        }
        // Return predicate matches
        var sig = goal.GetSignature();
        return Get(sig).Select(Inner);
        IEnumerable<KBMatch> Inner(IList<Predicate> list)
        {
            var buf = list.ToArray();
            foreach (var k in buf)
            {
                if (k.BuiltIn.TryGetValue(out _))
                {
                    yield return new KBMatch(goal, k, null);
                    yield break;
                }
                var predicate = k.Instantiate(ctx);
                if (predicate.Unify(goal).TryGetValue(out var matchSubs))
                {
                    yield return new KBMatch(goal, predicate, matchSubs);
                }
            }
        }
    }

    List<Predicate> Assert_(Predicate k, bool append)
    {
        var sig = k.Head.GetSignature();
        if (k.IsVariadic)
            sig = sig.WithArity(default);
        return GetOrCreate(sig, append: append);
    }

    public void AssertA(Predicate k)
    {
        Assert_(k, append: false).Insert(0, k);
    }
    public void AssertZ(Predicate k)
    {
        Assert_(k, append: true).Add(k);
    }
    public bool Retract(ITerm head)
    {
        if (Get(head.GetSignature()).TryGetValue(out var matches))
        {
            for (var i = matches.Count - 1; i >= 0; i--)
            {
                var predicate = matches[i];
                if (predicate.IsBuiltIn)
                    continue;
                if (predicate.Unify(head).TryGetValue(out _))
                {
                    matches.RemoveAt(i);
                    return true;
                }
            }
        }

        return false;
    }
    /// <summary>
    /// NOTE: Unlike other flavors of retract, this one allows you to retract built-ins.
    /// </summary>
    public bool Retract(Predicate pred)
    {
        if (Get(pred.Head.GetSignature()).TryGetValue(out var matches))
        {
            for (var i = matches.Count - 1; i >= 0; i--)
            {
                var predicate = matches[i];
                if (predicate.IsSameDefinitionAs(pred))
                {
                    matches.RemoveAt(i);
                    return true;
                }
            }
        }

        return false;
    }
    public bool Replace(Predicate pred, Predicate other)
    {
        if (Get(pred.Head.GetSignature()).TryGetValue(out var matches))
        {
            for (var i = matches.Count - 1; i >= 0; i--)
            {
                var predicate = matches[i];
                if (predicate.IsSameDefinitionAs(pred))
                {
                    matches.RemoveAt(i);
                    matches.Insert(i, other);
                    return true;
                }
            }
        }

        return false;
    }

    public int RetractAll(ITerm head)
    {
        var retracted = 0;
        if (Get(head.GetSignature()).TryGetValue(out var matches))
        {
            for (var i = matches.Count - 1; i >= 0; i--)
            {
                var predicate = matches[i];
                if (predicate.IsBuiltIn)
                    continue;
                if (predicate.Unify(head).TryGetValue(out _))
                {
                    retracted++;
                    matches.RemoveAt(i);
                }
            }
        }

        return retracted;
    }

    public IEnumerator<Predicate> GetEnumerator()
    {
        return Predicates.Values
            .Cast<List<Predicate>>()
            .SelectMany(l => l)
            .GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

