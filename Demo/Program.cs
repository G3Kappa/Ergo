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
            kb.AssertLast(ErgolParser.Parse("fact.", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("fact(john).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("fact(jack).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("fact(jane).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("loves(john, jane).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("loves(jack, jane).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("jealous(A, B) :-\n\tloves(A, C),\n\tloves(B, C).", ErgolParser.TryParseClause));
            Console.Write("?- ");
            while (Console.ReadLine() is { } line && line != "q") {
                if (line.StartsWith(":") && ErgolParser.TryParseClause(line[1..]).TryGetValue(out var clause)) {
                    kb.AssertLast(clause);
                    Console.WriteLine($"Asserted: {clause.Canonical()}.");
                    goto input;
                }
                else if (ErgolParser.TryParseQuery(line).TryGetValue(out var query)) {
                    var graph = kb.Solve(query);
                    var ans = graph
                        .Solutions()
                        .ToList();
                    if(ans.Count == 0) {
                        Console.WriteLine("No.");
                        goto input;
                    }
                    if(ans.Count == 1 && ans.Single().Bindings.Length == 0) {
                        Console.WriteLine("Yes.");
                        goto input;
                    }

                    Console.Write("\t   ");
                    foreach (var solution in ans) {
                        Console.WriteLine(String.Join(", ", solution.Bindings.Where(v => !v.VariableName.StartsWith("_")).Select(v => v.Canonical())));
                        Console.Write("\t ; ");
                        Console.ReadKey(true);
                    }
                    Console.Write("No.\n");
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
