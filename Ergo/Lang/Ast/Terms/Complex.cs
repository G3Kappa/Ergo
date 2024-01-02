using Ergo.Lang.Ast.Terms.Interfaces;
using System.Diagnostics;

namespace Ergo.Lang.Ast;

[DebuggerDisplay("{ Explain() }")]
public readonly partial struct Complex : ITerm
{
    public Maybe<ParserScope> Scope { get; }
    public bool IsGround => Arguments.All(arg => arg.IsGround);
    public readonly bool IsQualified { get; }

    public readonly Maybe<Operator> Operator;
    public readonly bool IsParenthesized { get; }

    public readonly Atom Functor;
    public readonly ImmutableArray<ITerm> Arguments;
    public readonly int Arity => Arguments.Length;

    private readonly int HashCode;

    public Complex(Atom functor, ImmutableArray<ITerm> args, Maybe<ParserScope> scope = default)
    {
        Functor = functor;
        Arguments = args;
        Scope = scope;
        HashCode = Arguments.Aggregate(Functor.GetHashCode(), (hash, a) => System.HashCode.Combine(hash, a));
        IsQualified = args.Length == 2 && WellKnown.Functors.Module.Contains(functor);
        Operator = Maybe<Operator>.None;
        IsParenthesized = false;
    }
    public Complex(Atom functor, params ITerm[] args)
        : this(functor, args.ToImmutableArray()) { }

    private Complex(Maybe<Operator> op, bool parenthesized, Atom functor, ImmutableArray<ITerm> args, Maybe<ParserScope> scope)
        : this(functor, args, scope)
    {
        Operator = op;
        IsParenthesized = parenthesized;
    }
    public Complex AsOperator(Maybe<Operator> affix) => new(affix, IsParenthesized, Functor, Arguments, Scope);
    public Complex AsOperator(Operator affix) => new(affix, IsParenthesized, Functor, Arguments, Scope);
    public Complex AsParenthesized(bool parens) => new(Operator, parens, Functor, Arguments, Scope);
    public Complex WithFunctor(Atom functor) => new(Operator, IsParenthesized, functor, Arguments, Scope);
    public Complex WithArguments(ImmutableArray<ITerm> args) => new(Operator, IsParenthesized, Functor, args, Scope);
    public Complex WithScope(Maybe<ParserScope> scope) => new(Operator, IsParenthesized, Functor, Arguments, scope);
    public TermNode ToNode(TermTree tree)
    {
        var node = new StaticTermNode(Functor, tree, Arity);
        for (var i = 0; i < Arity; ++i)
            node.SetArg(0, Arguments[i].ToNode(tree));
        return node;
    }
    public string Explain(bool canonical = false)
    {
        if (!canonical && IsParenthesized)
            return ParenthesizeUnlessRedundant(Inner(this));
        return Inner(this);

        string ParenthesizeUnlessRedundant(string s) => s.StartsWith('(') && s.EndsWith(')') ? s : $"({s})";

        string Inner(Complex c)
        {
            var f = c.Functor.AsQuoted(false).Explain(canonical);
            if (c.Operator.TryGetValue(out var op))
            {
                var ps = op.Fixity == Fixity.Infix ? " " : "";
                var ls = ps == "" ? RequiresSpace(f.First()) ? " " : "" : ps;
                var rs = ps == "" ? RequiresSpace(f.Last()) ? " " : "" : ps;
                return op.Fixity switch
                {
                    Fixity.Infix when !c.Functor.IsQuoted => $"{c.Arguments[0].Explain(canonical)}{ls}{f}{rs}{c.Arguments[1].Explain(canonical)}",
                    Fixity.Postfix when !c.Functor.IsQuoted => $"{c.Arguments.Single().Explain(canonical)}{f}",
                    _ when !c.Functor.IsQuoted && !canonical && c.Operator.TryGetValue(out _) => $"{f}{c.Arguments.Single().Explain(canonical)}",
                    _ => $"{c.Functor.Explain(canonical)}({c.Arguments.Join(arg => arg.Explain(canonical))})",
                };
            }
            else
            {
                return $"{c.Functor.Explain(canonical)}({c.Arguments.Join(arg => arg.Explain(canonical))})";
            }
        }

        static bool RequiresSpace(char c) => char.IsLetter(c)
            || c == '=';
    }

    public ITerm Substitute(Substitution s)
    {
        if (IsGround)
            return this;
        if (Equals(s.Lhs))
            return s.Rhs;
        var newArgs = new ITerm[Arguments.Length];
        for (var i = 0; i < newArgs.Length; i++)
        {
            newArgs[i] = Arguments[i].Substitute(s);
        }

        var ret = WithArguments(newArgs.ToImmutableArray());
        return ret;
    }

    public IEnumerable<Variable> Variables => Arguments.SelectMany(arg => arg.Variables);

    public bool Matches(Complex other) => Arity == other.Arity && Equals(Functor, other.Functor);

    public override bool Equals(object obj)
    {
        if (obj is AbstractTerm abs)
            return abs.Equals(this);
        if (obj is not Complex other)
            return false;
        if (!Matches(other))
            return false;
        var args = Arguments;
        return args.SequenceEqual(other.Arguments);
    }
    public bool Equals(ITerm obj) => Equals((object)obj);

    public override int GetHashCode() => HashCode;

    public int CompareTo(ITerm o)
    {
        if (o is AbstractTerm abs)
            return -abs.CompareTo(this);
        if (o is Atom) return 1;
        if (o is Variable) return 1;
        if (o is not Complex other)
            throw new InvalidCastException();

        if (Arity.CompareTo(other.Arity) is var cmpArity && cmpArity != 0)
            return cmpArity;
        if (Functor.CompareTo(other.Functor) is var cmpFunctor && cmpFunctor != 0)
            return cmpFunctor;
        return Arguments.Select((a, i) => a.CompareTo(other.Arguments[i]))
            .DefaultIfEmpty(0)
            .FirstOrDefault(cmp => cmp != 0);
    }

    public ITerm Instantiate(InstantiationContext ctx, Dictionary<string, Variable> vars = null)
    {
        vars ??= new();
        var builder = Arguments.ToBuilder();
        for (int i = 0; i < Arity; i++)
        {
            builder[i] = Arguments[i].Instantiate(ctx, vars);
        }
        return new Complex(Operator, IsParenthesized, Functor, builder.ToImmutableArray(), Scope);
    }

    public static bool operator ==(Complex left, Complex right) => left.Equals(right);

    public static bool operator !=(Complex left, Complex right) => !(left == right);
}
