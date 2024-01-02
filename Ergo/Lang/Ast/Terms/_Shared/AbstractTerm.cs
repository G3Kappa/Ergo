using Ergo.Interpreter;
using Ergo.Interpreter.Libraries.Expansions;

namespace Ergo.Lang.Ast.Terms.Interfaces;

public abstract class AbstractTerm : ITerm
{
    public Maybe<ParserScope> Scope { get; }
    public abstract ITerm CanonicalForm { get; set; }
    public abstract bool IsGround { get; }
    public abstract bool IsQualified { get; }
    public abstract bool IsParenthesized { get; }
    public abstract IEnumerable<Variable> Variables { get; }
    public AbstractTerm(Maybe<ParserScope> scope)
    {
        Scope = scope;
    }

    /// <summary>
    /// Expands an abstract term by expanding its canonical form, then parsing the result.
    /// </summary>
    public virtual IEnumerable<Either<ExpansionResult, ITerm>> Expand(Expansions lib, InterpreterScope scope)
    {
        /*
            Since expansions work by recursively expanding complex terms, and abstract terms are not complex terms,
            we first expand the canonical form of this term, which is going to be a normal Atom|Variable|Complex.
            The expansion is going to be in canonical form, so we need a way to turn it back into its abstract form.
            A convenient way to do this is by representing the canonical form as a string, then parsing that string
            with the same parser that produced this term. The result is going to have the same type as this term.
         */
        foreach (var termOrExp in lib.ExpandTerm(CanonicalForm, scope))
        {
            var result = termOrExp.Reduce(exp => exp.Binding.Select(v => (ITerm)v).GetOr(exp.Match),
                                      x => x);
            if (result.Equals(CanonicalForm))
            {
                yield return this;
                continue;
            }
            var canon = result.Explain(canonical: true);
            var parsed = Parsed.Abstract(scope.Facade, canon, scope.VisibleOperators, GetType());
            if (parsed.TryGetValue(out var abs))
            {
                if (termOrExp.TryGetA(out var exp))
                {
                    var expClauses = new NTuple(exp.Expansion.Contents);
                    yield return new ExpansionResult(abs, expClauses, exp.Binding);
                }
                else yield return abs;
            }
            else yield return termOrExp;
        }
    }

    public abstract AbstractTerm AsParenthesized(bool parenthesized);
    public abstract Signature GetSignature();
    public abstract int CompareTo(ITerm other);
    public abstract bool Equals(ITerm other);
    public abstract string Explain(bool canonical = false);
    public abstract ITerm Instantiate(InstantiationContext ctx, Dictionary<string, Variable> vars = null);
    public abstract ITerm Substitute(Substitution s);
    public ITerm Substitute(IEnumerable<Substitution> s) => s.Aggregate((ITerm)this, (a, b) => a.Substitute(b));
    public abstract Maybe<SubstitutionMap> Unify(ITerm other);
    public virtual TermNode ToNode(TermCache tree) => CanonicalForm.ToNode(tree);
    public override bool Equals(object obj)
    {
        if (obj is ITerm t)
            return Equals(t);
        return base.Equals(obj);
    }

    public override int GetHashCode() => base.GetHashCode();
}
