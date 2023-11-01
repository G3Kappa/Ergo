﻿using Ergo.Solver.BuiltIns;
using System.Diagnostics;

namespace Ergo.Lang.Ast;

[DebuggerDisplay("{ Explain(false) }")]
public readonly struct Predicate : IExplainable
{
    public readonly Atom DeclaringModule;
    public readonly ITerm Head;
    public readonly NTuple Body;
    public readonly string Documentation;
    public readonly bool IsDynamic;
    public readonly bool IsExported;
    public readonly bool IsTailRecursive;
    public readonly bool IsFactual;
    public readonly bool IsBuiltIn;
    public readonly bool IsVariadic;
    public readonly Maybe<SolverBuiltIn> BuiltIn;
    //public readonly bool IsDeterminate;
    //public bool IsLastCallOptimizable => IsTailRecursive && IsDeterminate;

    private static bool GetIsTailRecursive(ITerm head, NTuple body)
    {
        if (head.Equals(WellKnown.Literals.TopLevel))
            return false;
        return IsLastCall(head, body);
    }

    public static bool IsLastCall(ITerm head, NTuple body)
    {
        if (head is Variable)
            return false;
        var calls = 0;
        var sign = head.GetSignature();
        var anon = sign.Functor.BuildAnonymousTerm(sign.Arity.GetOr(0));
        foreach (var (goal, i) in body.Contents.Select((g, i) => (g, i)))
        {
            if (goal is not Variable && LanguageExtensions.Unify(anon, goal).TryGetValue(out _))
            {
                if (++calls > 1)
                    return false;
                if (i == body.Contents.Length - 1)
                    return true;
            }
        }
        return false;
    }

    public bool IsSameDeclarationAs(Predicate other)
    {
        if (BuiltIn.TryGetValue(out var b1) && other.BuiltIn.TryGetValue(out var b2))
            return b1 == b2;
        if (other.DeclaringModule != DeclaringModule)
            return false;
        if (!other.Head.GetSignature().Equals(Head.GetSignature()))
            return false;
        return true;
    }

    public bool IsSameDefinitionAs(Predicate other)
    {
        if (!IsSameDeclarationAs(other))
            return false;
        if (!Head.NumberVars().Equals(other.Head.NumberVars()))
            return false;
        if (!Body.NumberVars().Equals(other.Body.NumberVars()))
            return false;
        return true;
    }

    public static IEnumerable<ITerm> GetGoals(Predicate p)
    {
        var goals = new List<ITerm>();
        ExpandGoals(p.Body, t => { goals.Add(t); return t; });
        return goals;
    }

    public static NTuple ExpandGoals(NTuple goals, Func<ITerm, ITerm> expandGoal)
    {
        // These operators are treated in the same way as commas, i.e. as pure control flow operators.
        // Both their lhs and their rhs are goals that can be expanded.
        var binaryControlOps = new[] { WellKnown.Operators.Disjunction, WellKnown.Operators.If };
        // Same for these unary operators
        var unaryControlOps = new[] { WellKnown.Operators.Not };
        // TODO: also consider callables like lambdas (>>/2).
        return new NTuple(Inner(goals));
        IEnumerable<ITerm> Inner(NTuple body)
        {
            foreach (var goal in body.Contents)
            {
                // Special case: unwrap tuples
                ITerm ret;
                if (goal is NTuple { IsParenthesized: var parens } inner)
                {
                    var tup = new NTuple(Inner(inner), parenthesized: parens);
                    if (tup.Contents.Length > 0)
                        yield return tup;
                    continue;
                }
                else if (goal is Complex { Functor: var functor, Arguments: { Length: var len } args, Operator: var originalOp })
                {
                    if (len == 1)
                    {
                        var expandable = unaryControlOps.Where(o => o.Synonyms.Contains(functor))
                            .Select(x => Maybe.Some(x))
                            .SingleOrDefault();
                        if (expandable.TryGetValue(out var op))
                        {
                            var arg = ExpandGoals(new NTuple(new ITerm[] { args[0] }), expandGoal).SingleOrSelf();
                            var argEmpty = arg.Equals(WellKnown.Literals.EmptyCommaList);
                            if (argEmpty)
                            {
                                yield return WellKnown.Literals.True;
                                continue;
                            }
                            ret = expandGoal(op.ToComplex(arg, default).AsOperator(originalOp));
                        }
                        else
                        {
                            ret = expandGoal(goal);
                        }
                    }
                    else if (len == 2)
                    {
                        var expandable = binaryControlOps.Where(o => o.Synonyms.Contains(functor))
                            .Select(x => Maybe.Some(x))
                            .SingleOrDefault();
                        if (expandable.TryGetValue(out var op))
                        {
                            var left = ExpandGoals(new NTuple(new ITerm[] { args[0] }), expandGoal).SingleOrSelf();
                            var right = ExpandGoals(new NTuple(new ITerm[] { args[1] }), expandGoal).SingleOrSelf();
                            var leftEmpty = left.Equals(WellKnown.Literals.EmptyCommaList);
                            var rightEmpty = right.Equals(WellKnown.Literals.EmptyCommaList);
                            if (leftEmpty && rightEmpty)
                            {
                                yield return WellKnown.Literals.True;
                                continue;
                            }
                            else if (!leftEmpty && rightEmpty)
                            {
                                yield return left;
                                continue;
                            }
                            else if (!rightEmpty && leftEmpty)
                            {
                                yield return right;
                                continue;
                            }
                            ret = expandGoal(op.ToComplex(left, Maybe.Some(right)));
                        }
                        else
                        {
                            ret = expandGoal(goal);
                        }
                    }
                    else
                    {
                        ret = expandGoal(goal);
                    }
                }
                else
                {
                    ret = expandGoal(goal);
                }
                if (ret.Equals(WellKnown.Literals.True))
                    continue;
                yield return ret;
            }
        }
    }

    public Predicate(string desc, Atom module, ITerm head, NTuple body, bool dynamic, bool exported, bool tailRecursive)
    {
        Documentation = desc;
        DeclaringModule = module;
        Head = head;
        Body = body;
        IsDynamic = dynamic;
        IsExported = exported;
        IsTailRecursive = tailRecursive;
        IsFactual = Body.Contents.Length == 0 || Body.Contents.Length == 1 && Body.Contents.Single().Equals(WellKnown.Literals.True);
        IsBuiltIn = false;
        BuiltIn = default;
        IsVariadic = false;
    }

    public Predicate(string desc, Atom module, ITerm head, NTuple body, bool dynamic, bool exported)
        : this(desc, module, head, body, dynamic, exported, GetIsTailRecursive(head, body))
    {
    }

    public Predicate(SolverBuiltIn builtIn, Maybe<ITerm> head = default)
    {
        Documentation = $"<builtin> {builtIn.Documentation}";
        DeclaringModule = builtIn.Signature.Module.GetOr(WellKnown.Modules.Stdlib);
        if (!builtIn.Signature.Arity.TryGetValue(out var arity))
        {
            IsVariadic = true;
            arity = 0;
        }
        Head = head.GetOr(builtIn.Signature.Functor.BuildAnonymousTerm(arity));
        Body = NTuple.Empty;
        IsDynamic = false;
        IsExported = true;
        IsTailRecursive = false;
        IsFactual = arity == 0 && !IsVariadic;
        IsBuiltIn = true;
        BuiltIn = builtIn;
    }

    public Predicate Instantiate(InstantiationContext ctx, Dictionary<string, Variable> vars = null)
    {
        if (IsBuiltIn)
            return this;
        vars ??= new Dictionary<string, Variable>();
        return new Predicate(
            Documentation
            , DeclaringModule
            , Head.Instantiate(ctx, vars)
            , new NTuple(Body.Contents.Select(x => x.Instantiate(ctx, vars)), Head.Scope)
            , IsDynamic
            , IsExported
            , IsTailRecursive
        );
    }

    /// <summary>
    /// Moves all non-variable terms in pred's head to the beginning of its body, unifying them with free variables.
    /// </summary>
    public static Predicate InlineHead(InstantiationContext ctx, Predicate pred)
    {
        if (pred.IsBuiltIn)
            return pred;
        var inst = pred.Instantiate(ctx);
        if (inst.Head is not Complex cplx)
            return inst;
        var unify = new Atom("unify");
        var varArgs = cplx.Arguments
            .Select(x => x is Variable ? x : ctx.GetFreeVariable())
            .ToImmutableArray();
        var any = false;
        var preconditions = new List<ITerm>();
        for (int i = 0; i < cplx.Arity; i++)
        {
            if (cplx.Arguments[i] is Variable)
                continue;
            any = true;
            var unif = new Complex(unify, varArgs[i], cplx.Arguments[i]);
            preconditions.Add(unif);
        }
        if (!any)
            return inst;
        var newHead = cplx.WithArguments(varArgs);
        if (pred.IsFactual)
        {
            return inst.WithHead(newHead).WithBody(new NTuple(preconditions));
        }
        var newBodyStart = new NTuple(preconditions);
        var ifThenConstruct = WellKnown.Operators.If.ToComplex(newBodyStart, pred.Body);
        return inst.WithHead(newHead).WithBody(new NTuple(new ITerm[] { ifThenConstruct }));
    }

    public static Predicate Substitute(Predicate k, IEnumerable<Substitution> s)
    {
        if (k.IsBuiltIn)
            return k.WithHead(k.Head.Substitute(s));
        return new(k.Documentation, k.DeclaringModule, k.Head.Substitute(s), (NTuple)k.Body
            .Substitute(s), k.IsDynamic, k.IsExported, k.IsTailRecursive);
    }

    public Predicate WithHead(ITerm newHead)
    {
        if (BuiltIn.TryGetValue(out var builtIn))
            return new(builtIn, Maybe.Some(newHead));
        return new(Documentation, DeclaringModule, newHead, Body, IsDynamic, IsExported, IsTailRecursive);
    }
    public Predicate WithBody(NTuple newBody)
    {
        if (IsBuiltIn)
            throw new NotSupportedException();
        return new(Documentation, DeclaringModule, Head, newBody, IsDynamic, IsExported, IsTailRecursive);
    }
    public Predicate WithModuleName(Atom module)
    {
        if (IsBuiltIn)
            throw new NotSupportedException();
        return new(Documentation, module, Head, Body, IsDynamic, IsExported, IsTailRecursive);
    }
    public Predicate Dynamic()
    {
        if (IsBuiltIn)
            throw new NotSupportedException();
        return new(Documentation, DeclaringModule, Head, Body, true, IsExported, IsTailRecursive);
    }
    public Predicate Exported()
    {
        if (IsBuiltIn)
            return this;
        return new(Documentation, DeclaringModule, Head, Body, IsDynamic, true, IsTailRecursive);
    }
    public Predicate Qualified()
    {
        if (IsBuiltIn || Head.IsQualified)
            return this;
        return WithHead(Head.Qualified(DeclaringModule));
    }
    public Predicate Unqualified()
    {
        if (!Head.GetQualification(out var head).TryGetValue(out var _))
            return this;
        return WithHead(head);
    }

    public static bool FromCanonical(ITerm term, Atom defaultModule, out Predicate pred)
    {
        if (term is Complex c && WellKnown.Functors.Horn.Contains(c.Functor))
        {
            var head = c.Arguments[0];
            var body = c.Arguments[1].IsAbstract<NTuple>()
                .GetOr(new NTuple(new[] { c.Arguments[1] }, c.Scope));

            var mod = head.GetQualification(out head).GetOr(defaultModule);
            pred = new("(dynamic)", mod, head, body, true, false);
            return true;
        }

        var module = term.GetQualification(out term).GetOr(defaultModule);
        pred = new("(dynamic)", module, term, new NTuple(ImmutableArray<ITerm>.Empty.Add(WellKnown.Literals.True), term.Scope), true, false);
        return true;
    }

    public string Explain(bool canonical)
    {
        string expl;
        var doc = Documentation.Replace("\r", "").Split('\n').AsEnumerable().Select(r => "%: " + r).Join("\r\n");
        if (Body.IsEmpty || Body.Contents.SequenceEqual(new ITerm[] { WellKnown.Literals.True }))
        {
            expl = $"{Head.Explain()}.";
            if (!canonical && !string.IsNullOrWhiteSpace(Documentation))
                expl = $"{doc}\r\n{expl}";
        }
        else
        {
            expl = $"{Head.Explain()}{(canonical ? '←' : " ←\r\n\t")}{Body.Contents.Join(x => x.Explain(canonical), canonical ? "," : ",\r\n\t")}.";
            if (!canonical && !string.IsNullOrWhiteSpace(Documentation))
                expl = $"{doc}\r\n{expl}";
        }

        return expl;
    }
}