using Ergo.Abstractions.Inference;
using Ergo.Structures.Inference;
using Ergo.Structures.Monads;
using System;
using System.Linq;

namespace Ergo.Parser
{
    public static class ErgolParser
    {
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
                    .Where(t => t.c == ',' && args[0..t.i].Count(x => x == '(') == 0)
                    .Select(t => t.i)
                    .ToArray();
                var matches = topLevelCommas
                    .SelectMany(i => new[] { args[..i], args[i..] })
                    .Select(m => m.Trim())
                    .Where(m => m.Length > 0);
                if(topLevelCommas.Length == 0) {
                    matches = new[] { args };
                }
                matches = matches
                    .Select(m => m.Trim())
                    .Where(m => m.Length > 0);

                if(matches.Count() > 0) {
                    var arguments = matches
                        .Select(s => TryParseComplex(s).Match(c => c,
                            () => TryParseVariable(s).Match(v => (ITerm)v,
                                () => TryParseConstant(s).ValueOrThrow("Unreachable"))))
                        .ToArray();
                    return new CompoundTerm(functor, arguments);
                }
                return new CompoundTerm(functor, Array.Empty<ITerm>());
            }
            return Maybe.None;
        }
    }
}
