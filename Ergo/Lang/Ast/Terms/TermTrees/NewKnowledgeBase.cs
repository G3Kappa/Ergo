namespace Ergo.Lang.Ast;

public class NewKnowledgeBase(TermStore cache)
{
    public readonly record struct Index(int I) { public static explicit operator Index(int a) => new(a); };
    public readonly record struct IndexedPredicate(TermNode Head, ErgoVM.Op Body);
    public readonly record struct Match(IndexedPredicate Predicate, TermTreeSubstitutionMap Substitutions);

    public readonly TermStore Cache = cache;
    internal readonly List<IndexedPredicate> store = [];
    internal readonly Dictionary<Signature, Index> index = [];
    internal readonly Dictionary<Signature, int> counts = [];


    public void AssertA(Predicate p) => Assert(p, false);
    public void AssertZ(Predicate p) => Assert(p, true);

    protected void Assert(Predicate p, bool z)
    {
        var sign = p.IsExported
            ? p.Head.GetSignature()
            : p.Head.Qualified(p.Module).GetSignature();
        var head = p.Head.ToNode(Cache);
        var body = p.ExecutionGraph.GetOrThrow().Compile();
        var record = new IndexedPredicate(head, body);
        if (!index.TryGetValue(sign, out var i))
            i = index[sign] = new Index(store.Count);
        if (!counts.TryGetValue(sign, out var c))
            c = 0;
        counts[sign] = c + 1;
        store.Insert(i.I + (z ? c : 0), record);
    }

    public IEnumerable<Match> GetMatches(Maybe<Atom> module, TermNode head)
    {
        if (head is not StaticTermNode { Functor: var f })
            yield break;
        var functor = head.Tree[f];
        var sign = new Signature(functor, head.Arity, module, default);
    rest:
        if (!index.TryGetValue(sign, out var i))
            yield break;
        var to = i.I + counts[sign];
        for (int from = i.I; from < to; from++)
        {
            var record = store[from];
            var map = new TermTreeSubstitutionMap(Cache);
            if (!head.Unify(record.Head, map))
                continue;
            yield return new(record, map);
        }
        if (sign.Module.HasValue)
        {
            sign = sign.WithModule(default);
            goto rest;
        }
    }

    public bool Retract(Maybe<Atom> module, TermNode head)
    {
        if (head is not StaticTermNode { Functor: var f })
            return false;
        var functor = head.Tree[f];
        var sign = new Signature(functor, head.Arity, module, default);
    }

}

