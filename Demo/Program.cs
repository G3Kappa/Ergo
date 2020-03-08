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
            kb.AssertFirst(ErgolParser.Parse("fact(mario, loves, peach).", ErgolParser.TryParseClause));
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
                            Console.WriteLine(solution.Canonical());
                        }
                    }
                    Console.WriteLine("No.");
                }
                else {
                    Console.WriteLine($"Bad input: {query}.");
                }
            input:
                Console.Write("?- ");
            }
        }
    }
}
