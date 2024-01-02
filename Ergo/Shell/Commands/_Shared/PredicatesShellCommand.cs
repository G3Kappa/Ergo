using System.Text.RegularExpressions;

namespace Ergo.Shell.Commands;

public abstract class PredicatesShellCommand : ShellCommand
{
    public readonly bool Explain;

    public PredicatesShellCommand(string[] names, string desc, bool explain)
        : base(names, desc, @"(?<term>[^\s].*)?", true, 90) => Explain = explain;

    public override async IAsyncEnumerable<ShellScope> Callback(ErgoShell shell, ShellScope scope, Match m)
    {
        var term = m.Groups["term"];
        var shellScope = scope;
        var interpreterScope = scope.InterpreterScope;
        var predicates = scope.KnowledgeBase.AsEnumerable();
        if (term?.Success ?? false)
        {
            var parsed = interpreterScope.Parse<ITerm>($"{term.Value}, true");
            if (!parsed.TryGetValue(out var t))
            {
                shell.No();
                yield return scope;
                yield break;
            }

            var yes = interpreterScope.ExceptionHandler.TryGet(() =>
            {
                predicates = scope.KnowledgeBase
                    .Where(x => x.Module.Equals(t) || x.Unify(t).TryGetValue(out _));
                if (predicates.Any())
                {
                    shellScope = shellScope.WithInterpreterScope(interpreterScope);
                    return true;
                }
                return false;
            });

            if (!yes.GetOr(false))
            {
                shell.No();
                scope = shellScope;
                yield return scope;
                yield break;
            }

            scope = shellScope;
        }

        if (!Explain)
        {
            predicates = predicates.DistinctBy(p => p.Head.GetSignature());
        }

        var explanations = predicates
            .OrderBy(x => x switch
            {
                { IsDynamic: true } => 1,
                { IsExported: true } => 10,
                _ => 100
            })
            .Select(p => Explain
                ? new[] { p.Head.GetSignature().Explain(), p.Module.Explain(canonical: false), p.Explain(canonical: !Explain) }
                : new[] { p.Head.GetSignature().Explain(), p.Module.Explain(canonical: false), p.Documentation })
            .ToArray();
        if (explanations.Length == 0)
        {
            shell.No();
            yield return scope;
            yield break;
        }

        var cols = Explain
            ? new[] { "Predicate", "Module", "Explanation" }
            : new[] { "Predicate", "Module", "Documentation" }
            ;
        shell.WriteTable(cols, explanations, Explain ? ConsoleColor.DarkMagenta : ConsoleColor.DarkCyan);
        yield return scope;
    }
}
