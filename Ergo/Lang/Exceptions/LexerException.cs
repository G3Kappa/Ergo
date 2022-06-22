﻿using Ergo.Lang.Utils;

namespace Ergo.Lang.Exceptions;

public class LexerException : ErgoException
{
    public readonly Lexer.ErrorType ErrorType;

    public LexerException(Lexer.ErrorType error, Lexer.StreamState state, params object[] args)
        : base(ExceptionUtils.GetMessage(state, ExceptionUtils.GetLexerError(error, args))) => ErrorType = error;
}
