using Ergo.Lang.Ast.Terms.Interfaces;
using PeterO.Numbers;
using System.Diagnostics;

namespace Ergo.Lang.Ast;

[DebuggerDisplay("{ Explain() }")]
public readonly struct Atom : ITerm
{
    public Maybe<ParserScope> Scope { get; }

    public bool IsGround => true;
    public bool IsQualified => false;
    public bool IsParenthesized => false;

    public readonly object Value;
    private readonly int HashCode;
    public readonly bool IsQuoted;

    public Atom(object value, Maybe<bool> quoted = default, Maybe<ParserScope> scope = default)
    {
        Value = value;
        Scope = scope;
        if (Value?.IsNumericType() ?? false)
            Value = EDecimal.FromDecimal(Convert.ToDecimal(value));
        HashCode = value?.GetHashCode() ?? 0;
        IsQuoted = quoted.GetOr(value is string s
            && s != (string)WellKnown.Literals.EmptyList.Value
            && s != (string)WellKnown.Literals.EmptyCommaList.Value
            && s != (string)WellKnown.Literals.EmptySet.Value
            && (
                // And if this is not a string that can be confused with a variable name
                char.IsUpper(s.FirstOrDefault())
                // Or if this is a string that contains spaces or weird punctuation
                || s.Any(c => char.IsWhiteSpace(c)
                    || WellKnown.Lexemes.QuotablePunctuation.Contains(c))
            ));
    }
    public TermNode ToNode(TermTree tree) => new StaticTermNode(this, tree, 0);

    public string Explain(bool canonical = false)
    {
        if (Value is null)
        {
            return string.Empty;
        }
        else if (Value is bool b)
        {
            return b ? "⊤" : "⊥";
        }
        else if (Value is string s)
        {
            // In certain cases, the quotes can be omitted
            if (!IsQuoted)
            {
                return s;
            }

            return $"'{Escape(s)}'";
        }
        else if (Value is object
            && Value is not EDecimal)
        {
            return $"'{Escape(Value.ToString())}'";
        }
        return Value.ToString();

        static string Escape(string s) => s
            .Replace("'", "\\'")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    public ITerm Substitute(Substitution s)
    {
        if (Equals(s.Lhs)) return s.Rhs;
        return this;
    }

    public IEnumerable<Variable> Variables => Enumerable.Empty<Variable>();
    public Atom AsQuoted(bool quoted) => new(Value, quoted, scope: Scope);
    public Atom WithScope(Maybe<ParserScope> scope) => new(Value, IsQuoted, scope: scope);

    public override bool Equals(object obj)
    {
        if (obj is AbstractTerm abs)
            return abs.Equals(this);
        if (obj is not Atom other)
            return false;
        if (other.Value is double n && Value is double m)
            return m - n == 0d;
        return Equals(Value, other.Value);
    }
    public bool Equals(ITerm obj) => Equals((object)obj);

    public override int GetHashCode() => HashCode;

    public int CompareTo(ITerm o)
    {
        if (o is AbstractTerm abs) return -abs.CompareTo(this);
        if (o is Variable) return 1;
        if (o is Complex) return -1;
        if (o is not Atom other)
            throw new InvalidCastException();

        if (Value is EDecimal d && other.Value is EDecimal e)
        {
            return d.CompareTo(e);
        }

        if (Value is string s && other.Value is string t)
        {
            return s.CompareTo(t);
        }

        return Explain().CompareTo(other.Explain());
    }

    public ITerm Instantiate(InstantiationContext ctx, Dictionary<string, Variable> vars = null) => this;

    public static bool operator ==(Atom left, Atom right) => left.Equals(right);

    public static bool operator !=(Atom left, Atom right) => !(left == right);
}

