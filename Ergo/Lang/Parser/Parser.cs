﻿using Ergo.Facade;
using Ergo.Lang.Ast.Terms.Interfaces;
using Ergo.Lang.Parser;
using System.Diagnostics;

namespace Ergo.Lang;

public sealed class DiagnosticProbe : IDisposable
{
    private record struct Datum(int Hits, int Leaves, int Recursion, TimeSpan TotalTime, TimeSpan AverageTime, ImmutableDictionary<string, int> Counters);
    private Dictionary<string, Datum> _data = new();

    public string GetCurrentMethodName([CallerMemberName] string callerName = "") => callerName;
    public Stopwatch Enter([CallerMemberName] string callerName = "")
    {
        var sw = new Stopwatch();
        sw.Start();
        if (!_data.TryGetValue(callerName, out var datum))
        {
            _data[callerName] = datum = new(0, 0, 0, default, default, ImmutableDictionary.Create<string, int>());
        }
        _data[callerName] = datum with { Hits = datum.Hits + 1 };
        return sw;
    }

    public void Leave(Stopwatch sw, [CallerMemberName] string callerName = "")
    {
        if (_data.TryGetValue(callerName, out var datum))
        {
            _data[callerName] = datum with { Leaves = datum.Leaves + 1, TotalTime = sw.Elapsed + datum.TotalTime, AverageTime = datum.TotalTime / (float)datum.Hits };
            sw.Stop();
        }
        else throw new InvalidOperationException(callerName);
    }

    public void Count(string counter, int amount = 1, [CallerMemberName] string callerName = "")
    {
        if (_data.TryGetValue(callerName, out var datum))
        {
            if (datum.Counters.TryGetValue(counter, out var oldAmount))
                amount += oldAmount;
            _data[callerName] = datum with { Counters = datum.Counters.SetItem(counter, amount) };
        }
        else throw new InvalidOperationException(callerName);
    }

    public void Dispose()
    {
        if (_data.FirstOrDefault(d => d.Value.Hits != d.Value.Leaves || d.Value.Recursion != 0) is { Key: { } } item)
            throw new InvalidOperationException($"{item.Key}: {item.Value}");
    }

    public string GetDiagnostics()
    {
        var totalTime = _data.Values.Sum(x => x.TotalTime.TotalMilliseconds);
        return _data
            .Select(kv => (kv.Key, kv.Value, SelfTimePct: (float)(kv.Value.TotalTime.TotalMilliseconds / totalTime) * 100))
            .OrderBy(kv => kv.SelfTimePct)
            .Select(x => $"{x.Key,20}: HIT={x.Value.Hits:00000} TOT={x.Value.TotalTime.TotalMilliseconds:00000.000000} AVG={x.Value.AverageTime.TotalMilliseconds:00000.000000} SLF={x.SelfTimePct:000.00}%{(x.Value.Counters.Any() ? $"\r\n\t{{ {x.Value.Counters.Select(kv => $"{kv.Key}={kv.Value:00000}").Join()} }}" : "")}")
            .Join("\r\n") + "\r\n";
    }
}

public partial class ErgoParser : IDisposable
{
    private InstantiationContext _discardContext;
    private HashSet<string> _memoizationTable = new();

    private readonly DiagnosticProbe Probe = new();

    private string GetMemoKey(ErgoLexer.StreamState state, string callerName) => $"{state}@{callerName}";

    private bool IsFailureMemoized(ErgoLexer.StreamState state, [CallerMemberName] string callerName = "")
    {
#if ERGO_PARSER_DISABLE_MEMOIZATION
        return default;
#endif
        if (_memoizationTable.Contains(GetMemoKey(state, callerName)))
        {
            if (callerName != "Atom")
                ;
            Probe.Count("MEMO_HIT", 1, callerName);
            return true;
        }
        return false;
    }

    private void MemoizeFailure(ErgoLexer.StreamState state, [CallerMemberName] string callerName = "")
    {
#if ERGO_PARSER_DISABLE_MEMOIZATION
        return parsed;
#endif
        var key = GetMemoKey(state, callerName);
        if (!_memoizationTable.Contains(key))
        {
            _memoizationTable.Add(key);
            Probe.Count("MEMO_NEW", 1, callerName);
        }
    }

    private Maybe<T> MemoizeAndFail<T>(ErgoLexer.StreamState state, [CallerMemberName] string callerName = "")
    {
        MemoizeFailure(state, callerName);
        return Fail<T>(state);
    }

    public readonly ErgoLexer Lexer;
    public readonly ErgoFacade Facade;

    protected Dictionary<Type, IAbstractTermParser> AbstractTermParsers { get; private set; } = new();

    internal ErgoParser(ErgoFacade facade, ErgoLexer lexer)
    {
        Facade = facade;
        Lexer = lexer;
        _discardContext = new(string.Empty);
    }

    public bool RemoveAbstractParser<T>(out IAbstractTermParser<T> parser)
        where T : IAbstractTerm
    {
        parser = default;
        if (!AbstractTermParsers.Remove(typeof(T), out var parser_))
            return false;
        parser = (IAbstractTermParser<T>)parser_;
        return true;
    }
    public void AddAbstractParser<T>(IAbstractTermParser<T> parser)
        where T : IAbstractTerm => AbstractTermParsers.Add(typeof(T), parser);

    public Maybe<IEnumerable<Operator>> GetOperatorsFromFunctor(Atom functor)
    {
        var match = Lexer.AvailableOperators
            .Where(op => op.Synonyms.Any(s => functor.Equals(s)));
        if (!match.Any())
        {
            return default;
        }

        return Maybe.Some(match);
    }

    public Maybe<T> Abstract<T>()
        where T : IAbstractTerm => Abstract(typeof(T)).Select(x => (T)x);

    public Maybe<IAbstractTerm> Abstract(Type type)
    {
        var watch = Probe.Enter();
        if (!AbstractTermParsers.TryGetValue(type, out var parser))
        {
            Probe.Leave(watch);
            return default;
        }
        return parser.Parse(this)
            .Do(() => Probe.Leave(watch));
    }

    public Maybe<Atom> Atom()
    {
        var watch = Probe.Enter();
        var pos = Lexer.State;
        if (IsFailureMemoized(pos))
        {
            Probe.Leave(watch);
            return default;
        }
        return Expect<string>(ErgoLexer.TokenType.String)
                .Select(x => new Atom(x))
            .Or(() => Expect<double>(ErgoLexer.TokenType.Number)
                .Select(x => new Atom(x)))
            .Or(() => Expect<string>(ErgoLexer.TokenType.Keyword, kw => ErgoLexer.BooleanSymbols.Contains(kw))
                .Select(x => new Atom(ErgoLexer.TrueSymbols.Contains(x))))
            .Or(() => Expect<string>(ErgoLexer.TokenType.Keyword, kw => ErgoLexer.CutSymbols.Contains(kw))
                .Select(x => WellKnown.Literals.Cut))
            .Or(() => Expect<string>(ErgoLexer.TokenType.Term)
                .Where(x => IsAtomIdentifier(x))
                .Select(x => new Atom(x)))
            .Or(() => MemoizeAndFail<Atom>(pos))
            .Do(() => Probe.Leave(watch))
            ;

    }
    public Maybe<Variable> Variable()
    {
        var watch = Probe.Enter();
        var pos = Lexer.State;
        if (IsFailureMemoized(pos))
        {
            Probe.Leave(watch);
            return default;
        }
        return Expect<string>(ErgoLexer.TokenType.Term)
        .Where(term => IsVariableIdentifier(term))
        .Map(term => Maybe.Some(term)
            .Where(term => !term.StartsWith("__K"))
            .Do(none: () => Throw(pos, ErrorType.TermHasIllegalName, term))
            .Where(term => !term.Equals(WellKnown.Literals.Discard.Explain()))
            .Or(() => $"_{_discardContext.VarPrefix}{_discardContext.GetFreeVariableId()}"))
        .Select(t => new Variable(t))
        .Or(() => MemoizeAndFail<Variable>(pos))
        .Do(() => Probe.Leave(watch))
        ;
    }

    public Maybe<Complex> Complex()
    {
        var watch = Probe.Enter();
        var pos = Lexer.State;
        if (IsFailureMemoized(pos))
        {
            Probe.Leave(watch);
            return default;
        }
        return Atom()
            .Map(functor => Abstract<NTuple>()
                .Select(args => new Complex(functor, args.Contents.ToArray())))
            .Or(() => MemoizeAndFail<Complex>(pos))
            .Do(() => Probe.Leave(watch))
            ;
    }

    public Maybe<ITerm> ExpressionOrTerm()
    {
        var watch = Probe.Enter();
        var pos = Lexer.State;
        if (IsFailureMemoized(pos))
        {
            Probe.Leave(watch);
            return default;
        }
        return Expression()
                .Select<ITerm>(e => e.Complex)
            .Or(() => Term())
            .Or(() => MemoizeAndFail<ITerm>(pos))
            .Do(() => Probe.Leave(watch))
            ;
    }

    public Maybe<ITerm> Term()
    {
        var watch = Probe.Enter();
        var pos = Lexer.State;
        if (IsFailureMemoized(pos))
        {
            Probe.Leave(watch);
            return default;
        }
        return Parenthesized("(", ")", () => Expression())
                    .Select<ITerm>(x => x.Complex.AsParenthesized(true))
            .Or(() => Parenthesized("(", ")", () => Inner())
                .Select(x => x.AsParenthesized(true)))
            .Or(() => Inner())
            .Or(() => MemoizeAndFail<ITerm>(pos))
            .Do(() => Probe.Leave(watch))
            ;

        Maybe<ITerm> Inner()
        {
            var pos = Lexer.State;
            var primary = () => Variable().Select(x => (ITerm)x)
                .Or(() => Complex().Select(x => (ITerm)x))
                .Or(() => Atom().Select(x => (ITerm)x))
                .Or(() => Fail<ITerm>(pos));
            if (AbstractTermParsers.Values.Any())
            {
                var parsers = AbstractTermParsers.Values.ToArray();
                var abstractFold = parsers.Skip(1)
                    .Aggregate(parsers.First().Parse(this).Or(() => Fail<IAbstractTerm>(pos)),
                        (a, b) => a.Or(() => b.Parse(this).Or(() => Fail<IAbstractTerm>(pos))))
                    .Select(x => x.CanonicalForm);
                return abstractFold
                    .Or(primary);
            }

            return primary();
        }
    }

    public Maybe<Operator> ExpectOperator(Func<Operator, bool> match)
    {
        var watch = Probe.Enter();
        return Expect<string>(ErgoLexer.TokenType.Operator)
            .Map(str => GetOperatorsFromFunctor(new Atom(str)))
            .Where(ops => ops.Any(match))
            .Select(ops => ops.Single(match))
            .Do(() => Probe.Leave(watch))
            ;
    }

    public Expression BuildExpression(Operator op, ITerm lhs, Maybe<ITerm> maybeRhs = default, bool exprParenthesized = false)
    {
        var watch = Probe.Enter();
        return maybeRhs
            .Select(rhs => Associate(lhs, rhs))
            .Do(() => Probe.Leave(watch))
            .GetOr(new Expression(op, lhs, Maybe<ITerm>.None, lhs.IsParenthesized || exprParenthesized))
            ;

        Expression Associate(ITerm lhs, ITerm rhs)
        {
            // When the lhs represents an expression with the same precedence as this (and thus associativity, by design)
            // and right associativity, we have to swap the arguments around until they look right.
            if (!lhs.IsParenthesized
            && TryConvertExpression(lhs, out var lhsExpr, exprParenthesized)
            && lhsExpr.Operator.Affix == OperatorAffix.Infix
            && lhsExpr.Operator.Associativity == OperatorAssociativity.Right
            && lhsExpr.Operator.Precedence == op.Precedence)
            {
                // a, b, c -> ','(','(','(a, b), c)) -> ','(a, ','(b, ','(c))
                var lhsRhs = lhsExpr.Right.GetOrThrow(new InvalidOperationException());
                var newRhs = BuildExpression(lhsExpr.Operator, lhsRhs, Maybe.Some(rhs), exprParenthesized);
                return BuildExpression(op, lhsExpr.Left, Maybe.Some<ITerm>(newRhs.Complex), exprParenthesized);
            }
            // Special case for comma-lists: always parse them as NTuples
            if (WellKnown.Operators.Conjunction.Equals(op))
            {
                var list = (Complex)new NTuple(new[] { lhs, rhs }).CanonicalForm;
                return new Expression(list);
            }
            return new Expression(op, lhs, Maybe.Some(rhs), exprParenthesized);
        }

        bool TryConvertExpression(ITerm t, out Expression expr, bool exprParenthesized = false)
        {
            expr = default;
            if (t is not Complex cplx)
                return false;
            if (!GetOperatorsFromFunctor(cplx.Functor).TryGetValue(out var ops))
                return false;
            var op = ops.Where(op => cplx.Arity switch
            {
                1 => op.Affix != OperatorAffix.Infix,
                _ => op.Affix == OperatorAffix.Infix
            }).MinBy(x => x.Precedence);
            if (cplx.Arguments.Length == 1)
            {
                expr = BuildExpression(op, cplx.Arguments[0], exprParenthesized: exprParenthesized);
                return true;
            }

            if (cplx.Arguments.Length == 2)
            {
                expr = BuildExpression(op, cplx.Arguments[0], Maybe.Some(cplx.Arguments[1]), exprParenthesized: exprParenthesized);
                return true;
            }

            return false;
        }
    }

    public Maybe<Expression> Prefix()
    {
        var watch = Probe.Enter();
        var pos = Lexer.State;
        if (IsFailureMemoized(pos))
        {
            Probe.Leave(watch);
            return default;
        }
        return ExpectOperator(op => op.Affix == OperatorAffix.Prefix)
            .Map(op => Term()
                .Select(arg => BuildExpression(op, arg, exprParenthesized: arg.IsParenthesized)))
            .Or(() => MemoizeAndFail<Expression>(pos))
            .Do(() => Probe.Leave(watch))
            ;
    }

    public Maybe<Expression> Postfix()
    {
        var watch = Probe.Enter();
        var pos = Lexer.State;
        if (IsFailureMemoized(pos))
        {
            Probe.Leave(watch);
            return default;
        }
        return Term()
            .Map(arg => ExpectOperator(op => op.Affix == OperatorAffix.Postfix)
                .Select(op => BuildExpression(op, arg, exprParenthesized: arg.IsParenthesized)))
            .Or(() => MemoizeAndFail<Expression>(pos))
            .Do(() => Probe.Leave(watch))
            ;
    }

    public Maybe<Expression> Expression()
    {
        var watch = Probe.Enter();
        var pos = Lexer.State;
        if (IsFailureMemoized(pos))
        {
            Probe.Leave(watch);
            return default;
        }
        if (Primary().TryGetValue(out var lhs))
        {
            if (WithMinPrecedence(lhs, 0).TryGetValue(out var expr))
            {
                return Maybe.Some(expr).Do(() => Probe.Leave(watch));
            }
            // Special case for unary expressions
            if (lhs is not Complex cplx
                || cplx.Arguments.Length > 1
                || !GetOperatorsFromFunctor(cplx.Functor).TryGetValue(out var ops))
            {
                return MemoizeAndFail<Expression>(pos).Do(() => Probe.Leave(watch));
            }

            var op = ops.Single(op => op.Affix != OperatorAffix.Infix);
            expr = BuildExpression(op, cplx.Arguments[0], Maybe<ITerm>.None);
            return Maybe.Some(expr)
                .Do(() => Probe.Leave(watch));
        }

        return MemoizeAndFail<Expression>(pos).Do(() => Probe.Leave(watch));

    }
    Maybe<Expression> WithMinPrecedence(ITerm lhs, int minPrecedence)
    {
        var watch = Probe.Enter();
        var pos = Lexer.State;
        if (!PeekNextOperator().TryGetValue(out var lookahead))
        {
            return Fail<Expression>(pos)
                .Do(() => Probe.Leave(watch))
                ;
        }

        if (lookahead.Affix != OperatorAffix.Infix || lookahead.Precedence < minPrecedence)
        {
            return Fail<Expression>(pos)
                .Do(() => Probe.Leave(watch))
                ;
        }

        var expr = default(Expression);
        while (lookahead.Affix == OperatorAffix.Infix && lookahead.Precedence >= minPrecedence)
        {
            Lexer.ReadNext();
            var op = lookahead;

            if (!Primary().TryGetValue(out var rhs))
            {
                return Fail<Expression>(pos)
                .Do(() => Probe.Leave(watch))
                ;
            }

            if (!PeekNextOperator().TryGetValue(out lookahead))
            {
                expr = BuildExpression(op, lhs, Maybe.Some(rhs));
                break;
            }

            while (lookahead.Affix == OperatorAffix.Infix && lookahead.Precedence > op.Precedence
                || lookahead.Associativity == OperatorAssociativity.Right && lookahead.Precedence == op.Precedence)
            {
                if (!WithMinPrecedence(rhs, op.Precedence + 1).TryGetValue(out var newRhs))
                {
                    break;
                }

                rhs = newRhs.Complex;
                if (!PeekNextOperator().TryGetValue(out lookahead))
                {
                    break;
                }
            }

            lhs = (expr = BuildExpression(op, lhs, Maybe.Some(rhs))).Complex;
        }

        return Maybe.Some(expr)
                .Do(() => Probe.Leave(watch))
                ;
    }

    Maybe<ITerm> Primary()
    {
        var watch = Probe.Enter();
        var pos = Lexer.State;
        if (IsFailureMemoized(pos))
        {
            Probe.Leave(watch);
            return default;
        }
        return Prefix().Select<ITerm>(p => p.Complex)
            .Or(() => Postfix().Select<ITerm>(p => p.Complex))
            .Or(() => Term())
            .Or(() => MemoizeAndFail<ITerm>(pos))
            .Do(() => Probe.Leave(watch))
            ;
    }

    Maybe<Operator> PeekNextOperator()
    {
        var watch = Probe.Enter();
        try
        {
            return Lexer.PeekNext()
                .Map(lookahead => GetOperatorsFromFunctor(new Atom(lookahead.Value))
                    .Select(ops => ops.Where(op => op.Affix == OperatorAffix.Infix).MinBy(x => x.Precedence)))
                .Do(() => Probe.Leave(watch))
                ;
        }
        catch (InvalidOperationException)
        {
            Probe.Leave(watch);
            return default;
        }
    }

    public Maybe<Directive> Directive()
    {
        var watch = Probe.Enter();
        var pos = Lexer.State;
        if (IsFailureMemoized(pos))
        {
            Probe.Leave(watch);
            return default;
        }
        if (Expect<string>(ErgoLexer.TokenType.Comment, p => p.StartsWith(":")).TryGetValue(out var desc))
        {
            desc = desc[1..].TrimStart();
            while (Expect<string>(ErgoLexer.TokenType.Comment, p => p.StartsWith(":")).TryGetValue(out var newDesc))
            {
                if (!string.IsNullOrEmpty(newDesc))
                {
                    desc += "\n" + newDesc[1..].TrimStart();
                }
            }
        }

        if (Lexer.Eof)
            return MemoizeAndFail<Directive>(pos).Do(() => Probe.Leave(watch));

        desc ??= " ";
        return Expression()
            .Where(op => WellKnown.Operators.UnaryHorn.Equals(op.Operator))
            .Map(op => ExpectDelimiter(p => p.Equals("."))
                .Do(none: () => Throw(pos, ErrorType.UnterminatedClauseList))
                .Select(_ => op))
            .Select(op => new Directive(op.Left, desc))
            .Or(() => MemoizeAndFail<Directive>(pos))
            .Do(() => Probe.Leave(watch))
            ;
    }

    public Maybe<Predicate> Predicate()
    {
        var watch = Probe.Enter();
        var pos = Lexer.State;
        if (IsFailureMemoized(pos))
        {
            Probe.Leave(watch);
            return default;
        }
        if (Expect<string>(ErgoLexer.TokenType.Comment, p => p.StartsWith(":")).TryGetValue(out var desc))
        {
            desc = desc[1..].TrimStart();
            while (Expect<string>(ErgoLexer.TokenType.Comment, p => p.StartsWith(":")).TryGetValue(out var newDesc))
            {
                if (!string.IsNullOrEmpty(newDesc))
                {
                    desc += "\n" + newDesc[1..].TrimStart();
                }
            }
        }

        if (Lexer.Eof)
            return MemoizeAndFail<Predicate>(pos).Do(() => Probe.Leave(watch));

        desc ??= " ";
        return Expression()
            .Map(op => Maybe.Some(op)
                .Where(op => WellKnown.Operators.BinaryHorn.Equals(op.Operator))
                .Or(() => new Expression(WellKnown.Operators.BinaryHorn, op.Complex, Maybe.Some<ITerm>(WellKnown.Literals.True), false)))
            .Map(op => Maybe.Some(op.Right.GetOrThrow(new InvalidOperationException()))
                .Map(rhs => NTuple.FromPseudoCanonical(rhs, default, hasEmptyElement: false)
                    .Or(() => new NTuple(new[] { rhs })))
                .Select(body => (head: op.Left, body)))
            .Or(() => Term()
                .Select(head => (head, body: new NTuple(new ITerm[] { WellKnown.Literals.True }))))
            .Do(none: () => Throw(pos, ErrorType.ExpectedClauseList))
            .Map(x => MakePredicate(pos, desc, x.head, x.body))
            .Map(p => ExpectDelimiter(p => p.Equals("."))
                .Do(none: () => Throw(pos, ErrorType.UnterminatedClauseList))
                .Select(_ => p))
            .Or(() => MemoizeAndFail<Predicate>(pos))
            .Do(() => Probe.Leave(watch))
            ;

        Maybe<Predicate> MakePredicate(ErgoLexer.StreamState pos, string desc, ITerm head, NTuple body)
        {
            var headVars = head.Variables
                .Where(v => !v.Equals(WellKnown.Literals.Discard));
            var bodyVars = body.Contents.SelectMany(t => t.Variables)
                .Distinct();
            var singletons = headVars.Where(v => !v.Ignored && !bodyVars.Contains(v) && headVars.Count(x => x.Name == v.Name) == 1)
                .Select(v => v.Explain());
            if (singletons.Any())
            {
                Throw(pos, ErrorType.PredicateHasSingletonVariables, head.GetSignature().Explain(), singletons.Join());
            }

            return new Predicate(desc, WellKnown.Modules.User, head, body, false, false);
        }
    }

    public Maybe<ErgoProgram> Program()
    {
        var watch = Probe.Enter();
        var directives = new List<Directive>();
        var predicates = new List<Predicate>();
        while (Directive().TryGetValue(out var directive))
        {
            directives.Add(directive);
        }

        while (Predicate().TryGetValue(out var predicate))
        {
            predicates.Add(predicate);
        }

        var moduleArgs = directives.Single(x => x.Body.GetFunctor().GetOr(default).Equals(new Atom("module")))
            .Body.GetArguments();

        if (moduleArgs.Length < 2 || !moduleArgs[1].IsAbstract<List>().TryGetValue(out var exported))
        {
            exported = List.Empty;
        }

        var exportedPredicates = predicates.Select(p =>
        {
            var sign = p.Head.GetSignature();
            var form = new Complex(WellKnown.Functors.Arity.First(), sign.Functor, new Atom((decimal)sign.Arity.GetOrThrow(new NotSupportedException())))
                .AsOperator(OperatorAffix.Infix);
            if (exported.Contents.Any(x => x.Equals(form)))
                return p.Exported();
            return p;
        });
        return Maybe.Some(new ErgoProgram(directives.ToArray(), exportedPredicates.ToArray())
            .AsPartial(false))
            .Do(() => Probe.Leave(watch))
            ;
    }

    public IEnumerable<Operator> OperatorDeclarations()
    {
        var watch = Probe.Enter();
        var pos = Lexer.State;
        var moduleName = WellKnown.Modules.Stdlib;
        var ret = new List<Operator>();
        try
        {
            while (Directive().TryGetValue(out var directive))
            {
                if (directive.Body is not Complex cplx)
                    continue;
                if (cplx.Functor.Equals(new Atom("module")))
                    moduleName = (Atom)cplx.Arguments[0];

                if (cplx.Functor.Equals(new Atom("op")))
                {
                    if (!cplx.Arguments[0].Matches<int>(out var precedence))
                        continue;
                    if (!cplx.Arguments[1].Matches<OperatorType>(out var type))
                        continue;
                    if (!cplx.Arguments[2].IsAbstract<List>().TryGetValue(out var syns))
                        continue;
                    ret.Add(new(moduleName, type, precedence, syns.Contents.Cast<Atom>().ToHashSet()));
                }
            }
        }
        catch
        {
            // The parser reached a point where a newly-declared operator was used. Probably.
        }

        Lexer.Seek(pos);
        Probe.Leave(watch);
        return ret;
    }

    public Maybe<ErgoProgram> ProgramDirectives()
    {
        var watch = Probe.Enter();
        var ret = true;
        var directives = new List<Directive>();
        try
        {
            while (Directive().TryGetValue(out var directive))
            {
                directives.Add(directive);
            }
        }
        catch (LexerException le) when (le.ErrorType == ErgoLexer.ErrorType.UnrecognizedOperator)
        {
            // The parser reached a point where a newly-declared operator was used. Probably.
        }

        if (!ret)
            return default;
        return Maybe.Some(new ErgoProgram(directives.ToArray(), Array.Empty<Predicate>())
            .AsPartial(true))
            .Do(() => Probe.Leave(watch))
            ;
    }

    public void Dispose()
    {
#if ERGO_PARSER_DIAGNOSTICS
        Console.WriteLine(Lexer.Stream.FileName);
        Console.WriteLine(Probe.GetDiagnostics());
#endif
        Probe.Dispose();
        Lexer.Dispose();
        GC.SuppressFinalize(this);
    }
}
