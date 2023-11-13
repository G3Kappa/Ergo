﻿using Ergo.Lang.Compiler;
using Ergo.Lang.Utils;

namespace Ergo.Lang.Exceptions;

public class RuntimeException : ErgoException
{
    public readonly ErgoVM.ErrorType ErrorType;

    public RuntimeException(ErgoVM.ErrorType error, params object[] args)
        : base(ExceptionUtils.GetRuntimeError(error, args)) => ErrorType = error;
}