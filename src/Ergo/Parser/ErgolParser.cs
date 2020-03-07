using Ergo.Abstractions.Inference;
using Ergo.Structures.Inference;
using Ergo.Structures.Monads;
using System;
using System.Linq;

namespace Ergo.Parser
{
    public static class ErgolParser
    {
        public static Maybe<ConstantTerm> TryParseConstant(string str)
        {
            if (RegularExpressions.Constant.Match(str) is { Success: true, Groups: var groups }) {
                var match = groups.Skip(1).Single(g => g.Success);
                return match.Name switch
                {
                    "string" => new ConstantTerm(match.Value),
                    "atom" => new ConstantTerm(match.Value),
                    "decimal" => new ConstantTerm(double.Parse(match.Value)),
                    "integer" => new ConstantTerm(int.Parse(match.Value)),
                    _ => throw new ArgumentException(nameof(str))
                };
            }
            return Maybe.None;
        }
        public static Maybe<VariableTerm> TryParseVariable(string str)
        {
            if (RegularExpressions.Variable.Match(str) is { Success: true, Value: var name }) {
                return new VariableTerm(name);
            }
            return Maybe.None;
        }
        public static Maybe<ComplexTerm> TryParseComplex(string str)
        {
            if (RegularExpressions.Complex.Match(str) is { Success: true, Groups: { } groups }) {
                var functor = groups["functor"].Value;
                var matches = RegularExpressions.Argument.Matches(groups["arguments"].Value);
                if(matches.Count() > 0) {
                    var arguments = matches
                        .Select(a => a.Groups["arg"].Value)
                        .Select(s => TryParseConstant(s).Match(c => c,
                            () => TryParseVariable(s).Match(v => (ITerm)v,
                                () => TryParseComplex(s).ValueOrThrow("Unreachable"))))
                        .ToArray();
                    return new ComplexTerm(functor, arguments);
                }
                return new ComplexTerm(functor, Array.Empty<ITerm>());
            }
            return Maybe.None;
        }
    }
}
