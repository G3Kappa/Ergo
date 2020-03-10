using Ergo.Abstractions.Inference;
using Ergo.Exceptions;
using Ergo.Extensions.Inference;
using Ergo.Structures.Inference;
using Ergo.Structures.Monads;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ergo.Parser
{
    public static class ErgolParser
    {
        /// <summary>
        /// Assertive wrapper of TryParse that fails with an exception when the result is Maybe.None.
        /// </summary>
        /// <param name="token">The token to parse.</param>
        /// <param name="parser">An ErgolParser.TryParse* function reference.</param>
        /// <returns>The parsed token, or throws an ErgolParserException.</returns>
        public static T Parse<T>(string token, Func<string, Maybe<T>> parser)
        {
            if (parser(token).TryGetValue(out var ret))
                return ret;
            throw new ErgolParserException($"Unable to parse token: --> {token} <--");
        }

        public static Maybe<AtomicTerm> TryParseConstant(string str)
        {
            if (RegularExpressions.Constant.Match(str) is { Success: true, Groups: var groups }) {
                var match = groups.Skip(1).Single(g => g.Success);
                return match.Name switch
                {
                    "string" => new AtomicTerm(match.Value),
                    "atom" => new AtomicTerm(match.Value),
                    "decimal" => new AtomicTerm(double.Parse(match.Value)),
                    "integer" => new AtomicTerm(int.Parse(match.Value)),
                    _ => throw new ArgumentException(nameof(str))
                };
            }
            return Maybe.None;
        }

        public static Maybe<Variable> TryParseVariable(string str)
        {
            if (RegularExpressions.Variable.Match(str) is { Success: true, Value: var name }) {
                return new Variable(name);
            }
            return Maybe.None;
        }

        public static Maybe<CompoundTerm> TryParseComplex(string str)
        {
            if (RegularExpressions.Complex.Match(str) is { Success: true, Groups: { } groups }) {
                var functor = groups["functor"].Value;
                var args = groups["arguments"].Value
                    .Trim()[1..^1];
                var topLevelCommas = args
                    .Select((c, i) => (c, i))
                    .Where(t => t.c == ',' && args[..t.i].Count(x => x == '(') == args[..t.i].Count(x => x == ')'))
                    .Select(t => t.i)
                    .ToArray();
                var matches = topLevelCommas
                    .SelectMany(i => new[] { ClearnArg(args[(i+1)..]) })
                    .Prepend(args[..topLevelCommas.FirstOrDefault()])
                    .Select(m => m.Trim())
                    .Where(m => m.Length > 0);
                if (topLevelCommas.Length == 0) {
                    matches = new[] { args };
                }
                matches = matches
                    .Select(m => m.Trim())
                    .Where(m => m.Length > 0);

                if (matches.Count() > 0) {
                    var arguments = matches
                        .Select(s => TryParseTerm(s).ValueOrThrow($"Unrecognized atom: {s}."))
                        .ToArray();
                    return new CompoundTerm(functor, arguments);
                }
                return new CompoundTerm(functor, Array.Empty<ITerm>());
            }
            return Maybe.None;

            string ClearnArg(string arg)
            {
                return RegularExpressions.ArgCleaner.Replace(arg, m => m.Groups["arg"].Value);
            }
        }

        public static Maybe<ITerm> TryParseTerm(string s)
        {
            return TryParseComplex(s).Match(c => Maybe.Some((ITerm)c),
                        () => TryParseVariable(s).Match(v => Maybe.Some((ITerm)v),
                            () => TryParseConstant(s).Match(k => Maybe.Some((ITerm)k),
                                () => Maybe.None)));
        }

        public static Maybe<Query> TryParseQuery(string query)
        {
            var goals = Goals();

            if(goals.Any(g => !g.TryGetValue(out _))) {
                return Maybe.None;
            }

            var q = (Query)goals.Select(g => g.ValueOrThrow("Unreachable")).ToArray();
            return Maybe.Some(q.Clone(false));

            IEnumerable<Maybe<Goal>> Goals()
            {
                foreach (var l in query.Split(new[] { "\n", "->" }, StringSplitOptions.RemoveEmptyEntries)) {
                    if (RegularExpressions.ClauseBody.Match(l) is { Success: true, Groups: var bg }) {
                        yield return TryParseTerm(bg["body"].Value)
                            .Map(t => Goal.From(t).ValueOrThrow("Unreachable"));
                    }
                    else {
                        yield return Maybe.None;
                    }
                }
            }
        }

        public static Maybe<Clause> TryParseClause(string clause)
        {
            var lines = clause.Split(new[] { "\n", "->" }, StringSplitOptions.RemoveEmptyEntries);
            var strHead = lines.First();
            if (RegularExpressions.ClauseHead.Match(strHead) is { Success: true, Groups: var hg }) {
                var head = TryParseTerm(hg["head"].Value)
                    .Map(t => Fact.From(t).ValueOrThrow("Unreachable"))
                    .ValueOrThrow("Unreachable");

                switch(hg["token"].Value) {
                    case "." when (lines.Length > 1):
                            throw new ErgolParserException("Clause was terminated before the end of its statement list.");
                    case ".":
                        var fact = new Clause(head, Array.Empty<Goal>());
                        ThrowIfClauseHasSingletons(fact);
                        return Maybe.Some(fact);
                }

                var body = lines.Skip(1)
                    .Select(l => {
                        if(RegularExpressions.ClauseBody.Match(l) is {  Success: true, Groups: var bg }) {
                            return TryParseTerm(bg["body"].Value)
                                .Map(t => Goal.From(t).ValueOrThrow("Unreachable"));
                        }
                        return Maybe.None;
                    })
                    .Select(m => m.ValueOrThrow("Unreachable"))
                    .ToArray();

                var ret = new Clause(head, body);
                ThrowIfClauseHasSingletons(ret);
                return ret;
            }
            return Maybe.None;

            void ThrowIfClauseHasSingletons(Clause clause)
            {
                var singletons = SingletonVariables(clause.Body.Goals
                    .SelectMany(g => g.Term.Variables())
                    .Concat(clause.Head.Term.Variables()));
                if (singletons.Length > 0) {
                    throw new ErgolParserException($"Clause {clause.Canonical()} has singleton variables: {String.Join(", ", singletons)}.");
                }
            }
        }

        private static string[] SingletonVariables(IEnumerable<Variable> vars)
        {
            return vars
                .GroupBy(v => v.Name)
                .Where(g => !g.Key.StartsWith("_") && g.Count() == 1)
                .Select(g => g.Key)
                .ToArray();
        }
    }
}
