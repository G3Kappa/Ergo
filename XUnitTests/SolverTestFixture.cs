﻿using Ergo.Facade;
using Ergo.Interpreter;
using Ergo.Lang.Ast;
using Ergo.Lang.Exceptions.Handler;

namespace Tests;

public sealed class SolverTestFixture : IDisposable
{
    public readonly ExceptionHandler NullExceptionHandler = default;
    public readonly ExceptionHandler ThrowingExceptionHandler = new(ex => throw ex);
    public readonly ErgoInterpreter Interpreter;
    public readonly InterpreterScope InterpreterScope;

    public SolverTestFixture()
    {
        // Run at start
        var basePath = Directory.GetCurrentDirectory();
        var stdlibPath = Path.Combine(basePath, @"..\..\..\..\Ergo\ergo");
        var testsPath = Path.Combine(basePath, @"..\..\..\ergo");
        var moduleName = new Atom("tests");

        Interpreter = ErgoFacade.Standard
            .BuildInterpreter(InterpreterFlags.Default);
        var scope = Interpreter.CreateScope(x => x
            .WithExceptionHandler(ThrowingExceptionHandler)
            .WithoutSearchDirectories()
            .WithSearchDirectory(testsPath)
            .WithSearchDirectory(stdlibPath)
        );
        var module = Interpreter.Load(ref scope, moduleName).GetOrThrow(new InvalidOperationException());
        InterpreterScope = scope
            .WithModule(module)
            .WithCurrentModule(module.Name);
    }

    ~SolverTestFixture()
    {
        Dispose();
    }

    public void Dispose() => GC.SuppressFinalize(this);// Run at end
}
