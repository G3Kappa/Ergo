using Ergo.Parser;
using Ergo.Structures.Knowledge;
using System;
using System.Linq;

namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            var kb = new InMemoryKnowledgeBase();
            kb.AssertLast(ErgolParser.Parse("fact(bowser, loves, peach).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("fact(mario, loves, peach).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("fact(luigi, loves, daisy).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("fact(waluigi, loves, daisy).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("fact(wario, loves, wario).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("fact(A, jealous, B) :-\n\tfact(A, loves, C),\n\tfact(B, loves, C).", ErgolParser.TryParseClause));
            Console.Write("?- ");
            while (Console.ReadLine() is { } line && line != "q") {
                if (ErgolParser.TryParseQuery(line).TryGetValue(out var query)) {
                    var ans = kb.Solve(query);
                    var slv = ans.ToList();
                    if (ans.Result.ValueOrThrow("Unreachable")) {
                        if (slv.Count == 0) {
                            Console.WriteLine("Yes.");
                            goto input;
                        }
                        foreach (var solution in slv) {
                            Console.Write("\t");
                            Console.Write(solution.Canonical());
                            Console.WriteLine(" ;");
                        }
                    }
                    Console.WriteLine("No.");
                }
                else {
                    Console.WriteLine($"Bad input: {line}.");
                }
            input:
                Console.Write("?- ");
            }
        }
    }
}
