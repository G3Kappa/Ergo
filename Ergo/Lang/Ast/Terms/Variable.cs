using Ergo.Lang.Ast.Terms.Interfaces;
using System.Diagnostics;

namespace Ergo.Lang.Ast;

[DebuggerDisplay("{ Explain() }")]
public readonly struct Variable : ITerm
{
    public Maybe<ParserScope> Scope { get; }

    public bool IsGround => false;
    public bool IsQualified => false;
    public bool IsParenthesized => false;

    public readonly string Name;
    /// <summary>
    /// True if this variable is not to be included in the final solution.
    /// </summary>
    public readonly bool Ignored;

    private readonly int HashCode;


    public Variable(string name, Maybe<ParserScope> scope = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name[0] != char.ToUpper(name[0]))
        {
            throw new InvalidOperationException("Variables must have a name that starts with an uppercase letter.");
        }
        Name = name;
        Ignored = name.StartsWith('_');
        HashCode = Name.GetHashCode();
        Scope = scope;
    }

    public TermNode ToNode(TermTree tree) => new VariableTermNode(this, tree);

    public string Explain(bool canonical = false)
    {
        return Name;
    }

    public ITerm Substitute(Substitution s)
    {
        if (Equals(s.Lhs))
        {
            return s.Rhs;
        }
        return this;
    }

    public Variable WithScope(Maybe<ParserScope> scope) => new(Name, scope);

    public IEnumerable<Variable> Variables => Enumerable.Empty<Variable>().Append(this);

    public ITerm Instantiate(InstantiationContext ctx, Dictionary<string, Variable> vars = null)
    {
        vars ??= new();
        if (vars.TryGetValue(Name, out var inst))
        {
            return inst;
        }
        return vars[Name] = ctx.GetFreeVariable();
    }
    public override bool Equals(object obj)
    {
        if (obj is AbstractTerm abs)
            return abs.Equals(this);
        if (obj is not Variable other)
            return false;
        return Equals(Name, other.Name);
    }
    public bool Equals(ITerm obj) => Equals((object)obj);

    public override int GetHashCode() => HashCode;

    public int CompareTo(ITerm o)
    {
        if (o is AbstractTerm abs)
            return -abs.CompareTo(this);
        if (o is Atom) return -1;
        if (o is Complex) return -1;
        if (o is not Variable other)
            throw new InvalidCastException();

        return Name.CompareTo(other.Name);
    }

    public static bool operator ==(Variable left, Variable right) => left.Equals(right);

    public static bool operator !=(Variable left, Variable right) => !(left == right);
}

