﻿using Ergo.Lang.Ast.Terms.Interfaces;

namespace Ergo.Lang.Ast;

public interface ITerm : IComparable<ITerm>, IEquatable<ITerm>, IExplainable
{
    bool IsGround { get; }
    bool IsQualified { get; }
    bool IsParenthesized { get; }
    IEnumerable<Variable> Variables { get; }
    Maybe<IAbstractTerm> AbstractForm { get; }

    Maybe<Atom> GetFunctor() => this switch
    {
        Atom a => Maybe.Some(a),
        Complex c => Maybe.Some(c.Functor),
        _ => Maybe<Atom>.None
    };

    Maybe<ITerm[]> GetArguments() => this switch
    {
        Atom a => default,
        Complex c => Maybe.Some(c.Arguments),
        _ => default
    };

    ITerm WithFunctor(Atom newFunctor) => this switch
    {
        Atom => newFunctor,
        Variable v => v,
        Complex c => c.WithFunctor(newFunctor),
        var x => x
    };

    ITerm WithAbstractForm(Maybe<IAbstractTerm> abs) => this switch
    {
        Atom a => a.WithAbstractForm(abs),
        Variable v => v.WithAbstractForm(abs),
        Complex c => c.WithAbstractForm(abs),
        var x => x
    };
    ITerm AsParenthesized(bool parens) => this switch
    {
        Atom a => a,
        Variable v => v,
        Complex c => c.AsParenthesized(parens),
        var x => x
    };

    bool TryQualify(Atom m, out ITerm qualified)
    {
        if (IsQualified)
        {
            qualified = this;
            return false;
        }

        qualified = new Complex(WellKnown.Functors.Module.First(), m, this)
            .AsOperator(OperatorAffix.Infix);
        return true;
    }
    bool TryGetQualification(out Atom module, out ITerm value)
    {
        if (!IsQualified || this is not Complex cplx || cplx.Arguments.Length != 2 || cplx.Arguments[0] is not Atom module_)
        {
            module = default;
            value = this;
            return false;
        }

        module = module_;
        value = cplx.Arguments[1];
        return true;
    }

    ITerm Substitute(Substitution s);
    ITerm Instantiate(InstantiationContext ctx, Dictionary<string, Variable> vars = null);
    ITerm Concat(params ITerm[] next)
    {
        if (this is Complex cplx)
            return cplx.WithArguments(cplx.Arguments.Concat(next).ToArray());
        if (this is Atom a)
            return new Complex(a, next);
        return this;
    }

    ITerm Substitute(IEnumerable<Substitution> subs)
    {
        var steps = subs.ToDictionary(s => s.Lhs);
        var variables = Variables.Where(var => steps.ContainsKey(var));
        var @base = this;
        while (variables.Any())
        {
            foreach (var var in variables)
            {
                @base = @base.Substitute(steps[var]);
            }

            variables = @base.Variables.Where(var => steps.ContainsKey(var));
        }

        return @base;
    }
}

