﻿using Ergo.Lang.Ast.Terms.Interfaces;

namespace Ergo.Lang.Ast;

public interface ITerm : IComparable<ITerm>, IEquatable<ITerm>, IExplainable
{
    Maybe<ParserScope> Scope { get; }
    bool IsGround { get; }
    bool IsQualified { get; }
    bool IsParenthesized { get; }
    IEnumerable<Variable> Variables { get; }
    Maybe<Atom> GetFunctor() => this switch
    {
        Atom a => a,
        Complex c => c.Functor,
        _ => Maybe<Atom>.None
    };

    ImmutableArray<ITerm> GetArguments() => this switch
    {
        Complex c => c.Arguments,
        _ => ImmutableArray.Create<ITerm>()
    };
    public ITerm GetVariant() => this switch
    {
        Complex c => c.WithArguments(c.Arguments.Select(x => x.GetVariant()).ToImmutableArray()),
        Variable v => v,
        Atom a => new Variable(a.Explain().ToUpper()),
        var x => x
    };

    ITerm WithFunctor(Atom newFunctor) => this switch
    {
        Atom => newFunctor,
        Variable v => v,
        Complex c => c.WithFunctor(newFunctor),
        var x => x
    };

    ITerm WithScope(Maybe<ParserScope> newScope) => this switch
    {
        Atom a => a.WithScope(newScope),
        Variable v => v.WithScope(newScope),
        Complex c => c.WithScope(newScope),
        var x => x
    };
    ITerm AsParenthesized(bool parens) => this switch
    {
        Complex c => c.AsParenthesized(parens),
        AbstractTerm t => t.AsParenthesized(parens),
        var x => x
    };
    ITerm AsQuoted(bool quote) => this switch
    {
        Atom a => a.AsQuoted(quote),
        var x => x
    };

    ITerm Qualified(Atom m)
    {
        return GetQualification(out var head)
            .Select(some => Inner(head))
            .GetOr(Inner(this));
        ITerm Inner(ITerm t)
        {
            return new Complex(WellKnown.Functors.Module.First(), m, t)
                .AsOperator(WellKnown.Operators.NamedArgument);
        }
    }
    Maybe<Atom> GetQualification(out ITerm head)
    {
        head = this;
        if (!IsQualified || this is not Complex cplx || cplx.Arguments.Length != 2 || cplx.Arguments[0] is not Atom module)
            return default;
        head = cplx.Arguments[1];
        return Maybe.Some(module);
    }
    ITerm Substitute(Substitution s);
    ITerm Instantiate(InstantiationContext ctx, Dictionary<string, Variable> vars = null);
    ITerm Concat(params ITerm[] next)
    {
        if (this is Complex cplx)
            return cplx.WithArguments(cplx.Arguments.AddRange(next));
        if (this is Atom a)
            return new Complex(a, next);
        return this;
    }

    ITerm Substitute(IEnumerable<Substitution> subs)
    {
        if (IsGround)
            return this;
        var steps = subs.ToDictionary(s => s.Lhs, s => s.Rhs);
        var variables = Variables.Where(var => steps.ContainsKey(var));
        var @base = this;
        while (variables.Any())
        {
            foreach (var var in variables)
            {
                @base = @base.Substitute(new Substitution(var, steps[var]));
            }

            var newVariables = @base.Variables.Where(var => steps.ContainsKey(var));
            if (variables.SequenceEqual(newVariables))
                break;
            variables = newVariables;
        }
        return @base;
    }

    ITerm StripTemporaryVariables() => Substitute(Variables
        .Where(v => v.Ignored)
        .Select(v => new Substitution(v, WellKnown.Literals.Discard)));

    /// <summary>
    /// Two terms A and B are variants iff there exists a renaming of the variables in A that makes A equivalent (==) to B and vice versa.
    /// </summary>
    bool IsVariantOf(ITerm b)
    {
        return this.NumberVars().Equals(b.NumberVars());
    }
}

