﻿using System.Diagnostics.CodeAnalysis;

namespace Ergo.Lang;

public partial class ErgoParser
{
    protected static bool IsPunctuation(ErgoLexer.Token token, [NotNull] string p) => token.Type == ErgoLexer.TokenType.Punctuation && p.Equals(token.Value);
    protected static bool IsVariableIdentifier(string s) => s[0] == '_' || char.IsLetter(s[0]) && char.IsUpper(s[0]);
    protected static bool IsAtomIdentifier(string s) => !IsVariableIdentifier(s);
    protected bool Fail(ErgoLexer.StreamState s)
    {
        Lexer.Seek(s);
        return false;
    }
    public bool Expect<T>(ErgoLexer.TokenType type, Func<T, bool> pred, out T value)
    {
        var pos = Lexer.State;
        value = default;
        if (!Lexer.TryReadNextToken(out var token) || token.Type != type || token.Value is not T t || !pred(t))
        {
            return Fail(pos);
        }

        value = t;
        return true;
    }
    public bool ExpectDelimiter(Func<string, bool> condition, out string d)
    {
        var pos = Lexer.State;
        if (Expect(ErgoLexer.TokenType.Punctuation, condition, out d))
        {
            return true;
        }

        if (Expect(ErgoLexer.TokenType.Operator, condition, out d))
        {
            return true;
        }

        return Fail(pos);
    }
    public bool Expect<T>(ErgoLexer.TokenType type, out T value) => Expect(type, _ => true, out value);
    protected bool Parenthesized<T>(Func<(bool Parsed, T Result)> tryParse, out T value)
    {
        var pos = Lexer.State; value = default;
        if (Expect(ErgoLexer.TokenType.Punctuation, str => str.Equals("("), out string _)
        && tryParse() is (true, var res)
        && Expect(ErgoLexer.TokenType.Punctuation, str => str.Equals(")"), out string _))
        {
            value = res;
            return true;
        }

        return Fail(pos);
    }
    protected void Throw(ErgoLexer.StreamState s, ErrorType error, params object[] args)
    {
        var old = Lexer.State;
        Lexer.Seek(s);
        throw new ParserException(error, old, args);
    }
    public bool TryParseSequence(
        Atom functor,
        Atom emptyElement,
        Func<(bool Success, ITerm Term, bool Parens)> tryParseElement,
        string openingDelim,
        Operator separator,
        string closingDelim,
        bool @throw,
        out UntypedSequence seq)
    {
        seq = default;
        var pos = Lexer.State;
        var args = new List<(ITerm Term, bool Parens)>();
        if (openingDelim != null)
        {
            if (!ExpectDelimiter(p => p == openingDelim, out var _))
            {
                return Fail(pos);
            }

            if (closingDelim != null && ExpectDelimiter(p => p == closingDelim, out var _))
            {
                // Empty list
                seq = new UntypedSequence(functor, emptyElement, (openingDelim, closingDelim), ImmutableArray<ITerm>.Empty);
                return true;
            }
        }

        while (tryParseElement() is (true, var term, var parens))
        {
            args.Add((term, parens));
            if (!ExpectDelimiter(p => true, out var q) || !separator.Synonyms.Any(s => q.Equals(s.Value)) && q != closingDelim)
            {
                if (@throw)
                    Throw(pos, ErrorType.ExpectedArgumentDelimiterOrClosedParens, separator, closingDelim);
                return Fail(pos);
            }

            if (closingDelim != null && q == closingDelim)
            {
                break;
            }
        }

        seq = new UntypedSequence(functor, emptyElement, (openingDelim, closingDelim), ImmutableArray.CreateRange(args.Select(a => a.Term)));
        return true;

    }
}
