﻿using Ergo.Interpreter;
using Ergo.Lang;
using Ergo.Lang.Ast;
using Ergo.Lang.Exceptions;
using Ergo.Lang.Utils;
using Ergo.Shell.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ergo.Shell
{
    public partial class ErgoShell
    {
        public readonly ExceptionHandler ExceptionHandler;
        public readonly ErgoInterpreter Interpreter;
        public readonly CommandDispatcher Dispatcher;
        public Func<LogLine, string> LineFormatter { get; set; }
        public Atom CurrentModule { get; set; }

        private volatile bool _repl;

        private volatile bool _trace;
        public bool TraceMode {
            get => _trace;
            set {
                if (_trace = value) {
                    Interpreter.Trace += HandleTrace;
                }
                else {
                    Interpreter.Trace -= HandleTrace;
                }
                void HandleTrace(Solver.SolverTraceType type, string trace)
                {
                    WriteLine(trace, LogLevel.Trc, type);
                }
            }
        }
        private volatile bool _throw;
        public bool ThrowUnhandledExceptions {
            get => _throw;
            set => _throw = value;
        }

        public Parsed<T> Parse<T>(string data, Func<string, T> onParseFail = null, Maybe<Atom> entryModule = default)
        {
            var userDefinedOps = Interpreter.GetUserDefinedOperators(entryModule.Reduce(some => some, () => CurrentModule)).ToArray();
            return new Parsed<T>(data, ExceptionHandler, onParseFail ?? (str => throw new ShellException($"Could not parse '{data}' as {typeof(T).Name}")), userDefinedOps);
        }

        public IEnumerable<Predicate> GetInterpreterPredicates(Maybe<Atom> entryModule = default) => Interpreter
            .GetSolver(entryModule.Reduce(some => some, () => ErgoInterpreter.UserModule))
            .KnowledgeBase.AsEnumerable();
        public IEnumerable<Predicate> GetUserPredicates() => Interpreter.Modules[ErgoInterpreter.UserModule].KnowledgeBase.AsEnumerable();

        public ErgoShell(ErgoInterpreter interpreter = null, Func<LogLine, string> formatter = null)
        {
            Interpreter = interpreter ?? new();
            CurrentModule = ErgoInterpreter.UserModule;
            Dispatcher = new CommandDispatcher(s => WriteLine($"Unknown command: {s}", LogLevel.Err));
            LineFormatter = formatter ?? DefaultLineFormatter;
            ExceptionHandler = new ExceptionHandler(ex => {
                WriteLine(ex.Message, LogLevel.Err);
                if (_throw && !(ex is ShellException || ex is InterpreterException || ex is ParserException || ex is LexerException)) {
                    throw ex;
                }
            });

            AddReflectedCommands();
            SetConsoleOutputCP(65001);
            SetConsoleCP(65001);
            Clear();
#if DEBUG
            ThrowUnhandledExceptions = true;
#endif
        }

        public bool TryAddCommand(ShellCommand s) => Dispatcher.TryAdd(s);

        protected void AddReflectedCommands()
        {
            var assembly = typeof(Save).Assembly;
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsAssignableTo(typeof(ShellCommand))) continue;
                if (!type.GetConstructors().Any(c => c.GetParameters().Length == 0)) continue;
                var inst = (ShellCommand)Activator.CreateInstance(type);
                Dispatcher.TryAdd(inst);
            }
        }

        public virtual void Clear()
        {
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Clear();
        }

        public virtual void Save(string fileName, bool force = true)
        {
            var preds = GetUserPredicates();
            if (File.Exists(fileName) && !force)
            {
                WriteLine($"File already exists: {fileName}", LogLevel.Err);
                return;
            }
            var module = Interpreter.Modules[CurrentModule];
            // TODO: make it easier to save directives
            var dirs = module.Imports.Contents
                .Select(m => new Directive(new Complex(new("use_module"), m)))
                .ToArray();
            var text = new ErgoProgram(dirs, preds.ToArray()).Explain();
            File.WriteAllText(fileName, text);
            WriteLine($"Saved: '{fileName}'.", LogLevel.Inf);
        }

        public virtual void Parse(string code, string fileName = "")
        {
            var preds = GetInterpreterPredicates(Maybe.Some(CurrentModule));
            var oldPredicates = preds.Count();
            if (ExceptionHandler.Try(() => Interpreter.Parse(code, fileName)))
            {
                var newPredicates = preds.Count();
                var delta = newPredicates - oldPredicates;
                WriteLine($"Loaded: '{fileName}'.\r\n\t{Math.Abs(delta)} {(delta >= 0 ? "new" : "")} predicates have been {(delta >= 0 ? "added" : "removed")}.", LogLevel.Inf);
            }
        }

        public virtual void Load(string fileName)
        {
            var preds = GetInterpreterPredicates(Maybe.Some(CurrentModule));
            var oldPredicates = preds.Count();
            if (ExceptionHandler.Try(() => Interpreter.Load(fileName)))
            {
                var newPredicates = preds.Count();
                var delta = newPredicates - oldPredicates;
                WriteLine($"Loaded: '{fileName}'.\r\n\t{Math.Abs(delta)} {(delta >= 0 ? "new" : "")} predicates have been {(delta >= 0 ? "added" : "removed")}.", LogLevel.Inf);
            }
        }

        public virtual void EnterRepl()
        {
            _repl = true;
            do {
                Do(Prompt());
            }
            while (_repl);
        }

        public bool Do(string command)
        {
            return ExceptionHandler.TryGet(() => Dispatcher.Dispatch(this, command), out var success) && success;
        }

        public virtual void ExitRepl()
        {
            _repl = false;
        }
    }
}