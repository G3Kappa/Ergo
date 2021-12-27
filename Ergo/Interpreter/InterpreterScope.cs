﻿using Ergo.Lang.Ast;
using Ergo.Solver.BuiltIns;
using Ergo.Interpreter.Directives;
using System.Collections.Generic;
using System.Collections.Immutable;
using Ergo.Lang.Exceptions;
using Ergo.Solver;
using System.Linq;
using Ergo.Lang;

namespace Ergo.Interpreter
{
    public readonly struct InterpreterScope
    {
        public readonly ImmutableDictionary<Atom, Module> Modules;
        public readonly ImmutableArray<string> SearchDirectories;
        public readonly bool Runtime;

        public readonly Atom CurrentModule;

        public InterpreterScope(Module userModule)
        {
            Modules = ImmutableDictionary.Create<Atom, Module>()
                .Add(userModule.Name, userModule);
            SearchDirectories = ImmutableArray<string>.Empty
                .Add(string.Empty)
                .Add("./stdlib/");
            CurrentModule = userModule.Name;
            Runtime = userModule.Runtime;
        }

        private InterpreterScope(
            Atom currentModule, 
            ImmutableDictionary<Atom, Module> modules, 
            ImmutableArray<string> dirs,
            bool runtime)
        {
            Modules = modules;
            SearchDirectories = dirs;
            CurrentModule = currentModule;
            Runtime = runtime;
        }

        public InterpreterScope WithCurrentModule(Atom a) => new(a, Modules, SearchDirectories, Runtime);
        public InterpreterScope WithModule(Module m) => new(CurrentModule, Modules.SetItem(m.Name, m), SearchDirectories, Runtime);
        public InterpreterScope WithSearchDirectory(string s) => new(CurrentModule, Modules, SearchDirectories.Add(s), Runtime);
        public InterpreterScope WithRuntime(bool runtime) => new(CurrentModule, Modules, SearchDirectories, runtime);

        public InterpreterScope WithoutModules() => new(CurrentModule, ImmutableDictionary.Create<Atom, Module>().Add(Interpreter.Modules.Prologue, Modules[Interpreter.Modules.Prologue]), SearchDirectories, Runtime);
        public InterpreterScope WithoutSearchDirectories() => new(CurrentModule, Modules, ImmutableArray<string>.Empty, Runtime);

        public IEnumerable<Operator> GetUserDefinedOperators(Maybe<Atom> entry = default, HashSet<Atom> added = null)
        {
            added ??= new();
            var currentModule = CurrentModule;
            var entryModule = entry.Reduce(some => some, () => currentModule);
            if (added.Contains(entryModule) || !Modules.TryGetValue(entryModule, out var module))
            {
                yield break;
            }
            added.Add(entryModule);
            foreach (var import in module.Imports.Contents)
            {
                foreach (var importedOp in GetUserDefinedOperators(Maybe.Some((Atom)import), added))
                {
                    if (!Modules[(Atom)import].Exports.Contents.Any(t =>
                    {
                        var x = TermMarshall.FromTerm(t, new
                        { Predicate = default(string), Arity = default(int) },
                            TermMarshall.MarshallingMode.Positional
                        );
                        return importedOp.Synonyms.Any(s => Equals(s.Value, x.Predicate))
                        && (x.Arity == 1 && importedOp.Affix != OperatorAffix.Infix
                        || x.Arity == 2);
                    }))
                    {
                        continue;
                    }
                    yield return importedOp;
                }
            }
            foreach (var op in module.Operators)
            {
                yield return op;
            }
        }

        public bool TryReplaceLiterals(ITerm term, out ITerm changed, Maybe<Atom> entry = default, HashSet<Atom> added = null)
        {
            changed = default;
            if (term is Variable) 
                return false;
            added ??= new();
            var currentModule = CurrentModule;
            var entryModule = entry.Reduce(some => some, () => currentModule);
            if (added.Contains(entryModule) || !Modules.TryGetValue(entryModule, out var module))
            {
                return false;
            }
            added.Add(entryModule);
            if(term is Atom a && module.Literals.TryGetValue(a, out var literal))
            {
                changed = literal.Value;
                return true;
            }
            if(term is Complex c)
            {
                var args = new ITerm[c.Arguments.Length];
                var any = false;
                for (int i = 0; i < args.Length; i++)
                {
                    if(TryReplaceLiterals(c.Arguments[i], out var arg, entry))
                    {
                        any = true;
                        args[i] = arg;
                    }
                    else
                    {
                        args[i] = c.Arguments[i];
                    }
                }
                changed = new Complex(c.Functor, args);
                return any;
            }
            foreach (var import in module.Imports.Contents)
            {
                if(TryReplaceLiterals(term, out changed, Maybe.Some((Atom)import), added))
                    return true;
            }
            return false;
        }
    }
}