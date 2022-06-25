﻿namespace Ergo.Interpreter;

public readonly struct InterpreterScope
{
    public readonly bool Runtime;
    public readonly ImmutableDictionary<Atom, Module> Modules;
    public readonly ImmutableArray<string> SearchDirectories;
    public readonly ExceptionHandler ExceptionHandler;

    public readonly Atom Module;

    public InterpreterScope(Module userModule)
    {
        var module = Module = userModule.Name;
        var modules = Modules = ImmutableDictionary.Create<Atom, Module>()
            .Add(userModule.Name, userModule);
        SearchDirectories = ImmutableArray<string>.Empty
            .Add(string.Empty)
            .Add("./ergo/stdlib/")
            .Add("./ergo/user/");
        Runtime = userModule.Runtime;
        ExceptionHandler = default;
    }

    private InterpreterScope(
        Atom currentModule,
        ImmutableDictionary<Atom, Module> modules,
        ImmutableArray<string> dirs,
        bool runtime,
        ExceptionHandler handler)
    {
        Modules = modules;
        SearchDirectories = dirs;
        Module = currentModule;
        Runtime = runtime;
        ExceptionHandler = handler;
    }

    public InterpreterScope WithCurrentModule(Atom a) => new(a, Modules, SearchDirectories, Runtime, ExceptionHandler);
    public InterpreterScope WithModule(Module m) => new(Module, Modules.SetItem(m.Name, m), SearchDirectories, Runtime, ExceptionHandler);
    public InterpreterScope WithoutModule(Atom m) => new(Module, Modules.Remove(m), SearchDirectories, Runtime, ExceptionHandler);
    public InterpreterScope WithSearchDirectory(string s) => new(Module, Modules, SearchDirectories.Add(s), Runtime, ExceptionHandler);
    public InterpreterScope WithRuntime(bool runtime) => new(Module, Modules, SearchDirectories, runtime, ExceptionHandler);
    public InterpreterScope WithExceptionHandler(ExceptionHandler newHandler) => new(Module, Modules, SearchDirectories, Runtime, newHandler);
    public InterpreterScope WithoutModules() => new(Module, ImmutableDictionary.Create<Atom, Module>().Add(WellKnown.Modules.Stdlib, Modules[WellKnown.Modules.Stdlib]), SearchDirectories, Runtime, ExceptionHandler);
    public InterpreterScope WithoutSearchDirectories() => new(Module, Modules, ImmutableArray<string>.Empty, Runtime, ExceptionHandler);

    public void Throw(InterpreterError error, params object[] args) => ExceptionHandler.Throw(new InterpreterException(error, this, args));

    private static IEnumerable<(Operator Op, int Depth)> GetOperatorsInner(Atom defaultModule, ImmutableDictionary<Atom, Module> modules, Maybe<Atom> entry = default, HashSet<Atom> added = null, int depth = 0)
    {
        added ??= new();
        var currentModule = defaultModule;
        var entryModule = entry.Reduce(some => some, () => currentModule);
        if (added.Contains(entryModule) || !modules.TryGetValue(entryModule, out var module))
        {
            yield break;
        }

        added.Add(entryModule);
        var depth_ = depth;
        foreach (Atom import in module.Imports.Contents)
        {
            foreach (var importedOp in GetOperatorsInner(defaultModule, modules, Maybe.Some(import), added, ++depth_ * 1000))
            {
                yield return importedOp;
            }
        }

        foreach (var op in module.Operators)
        {
            yield return (op, depth);
        }
        // Add well-known operators in a way that allows for their re-definition by modules down the import chain.
        // An example is the arity indicator (/)/2, that gets re-defined by the math module as the division operator.
        // In practice user code will only ever see the division operator, but the arity indicator ensures proper semantics when the math module is not loaded.
        foreach (var op in WellKnown.Operators.DefinedOperators)
        {
            if (op.DeclaringModule == entryModule)
                yield return (op, int.MaxValue);
        }
    }

    public IEnumerable<Operator> GetOperators()
    {
        var operators = GetOperatorsInner(Module, Modules)
            .ToList();
        foreach (var (op, depth) in operators)
        {
            if (!operators.Any(other => other.Depth < depth && other.Op.Synonyms.SequenceEqual(op.Synonyms)))
                yield return op;
        }
    }

    public IEnumerable<Module> GetLoadedModules()
    {
        return Inner(this, Module);
        IEnumerable<Module> Inner(InterpreterScope scope, Atom entry, HashSet<Atom> seen = default)
        {
            seen ??= new();
            if (seen.Contains(entry))
                yield break;
            var current = scope.Modules[entry];
            yield return current;
            seen.Add(entry);
            foreach (var import in current.Imports.Contents.Cast<Atom>()
                .SelectMany(i => Inner(scope, i, seen)))
            {
                yield return import;
            }
        }
    }

    public bool IsModuleVisible(Atom name, Maybe<Atom> entry = default, HashSet<Atom> added = null)
    {
        added ??= new();
        var currentModule = Module;
        var entryModule = entry.Reduce(some => some, () => currentModule);
        if (added.Contains(entryModule) || !Modules.TryGetValue(entryModule, out var module))
        {
            return false;
        }

        added.Add(entryModule);
        foreach (var import in module.Imports.Contents)
        {
            if (import.Equals(name))
                return true;
            if (IsModuleVisible(name, Maybe.Some((Atom)import), added))
                return true;
        }

        return false;
    }

    public KnowledgeBase BuildKnowledgeBase()
    {
        var kb = new KnowledgeBase();
        foreach (var module in GetLoadedModules())
        {
            foreach (var pred in module.Program.KnowledgeBase)
            {
                var sig = pred.Head.GetSignature();
                // TODO: Don't assert twice, this is awful. Match correctly instead.
                kb.AssertZ(pred.WithModuleName(module.Name).Qualified());
                if (module.Name == Module || module.ContainsExport(sig))
                {
                    kb.AssertZ(pred.WithModuleName(module.Name));
                }
            }
        }

        return kb;
    }
}