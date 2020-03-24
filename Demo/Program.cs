using Ergo.Exceptions;
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
            kb.AssertLast(ErgolParser.Parse("loves(john, jane).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("loves(jack, jane).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("jealous(A, B) :-\n\tloves(A, C),\n\tloves(B, C).", ErgolParser.TryParseClause));
            Console.Write("?- ");
            while (Console.ReadLine() is { } line && line != "q") {
                try {
                    if (line.StartsWith(":") && ErgolParser.TryParseClause(line[1..]).TryGetValue(out var clause)) {
                        kb.AssertLast(clause);
                        Console.WriteLine($"\tAsserted: {clause.Canonical()}.");
                        goto input;
                    }
                    else if (ErgolParser.TryParseQuery(line).TryGetValue(out var query)) {
                        SolutionGraph graph;
                        try {
                            graph = kb.Solve(query);
                        }
                        catch (ErgoException ex) {
                            Console.WriteLine($"\tERROR: {ex.Message}");
                            goto input;
                        }
                        var ans = graph
                            .Solutions()
                            .ToList();
                        if (ans.Count == 1 && graph.Root.Children.Count == 0) {
                            Console.WriteLine("\tNo.");
                            goto input;
                        }
                        var interestingSolutions = ans.Where(s => s.Bindings.Length > 0).ToList();
                        if (interestingSolutions.Count == 0) {
                            Console.WriteLine("\tYes.");
                            goto input;
                        }
                        Console.Write("\t   ");
                        foreach (var solution in interestingSolutions) {
                            Console.WriteLine(solution.Canonical());
                            Console.Write("\t ; ");
                            Console.ReadKey(true);
                        }
                        Console.Write("No.\n");
                    }
                }
                catch(ErgolParserException ex) {
                    Console.WriteLine($"\tERROR: {ex.Message}");
                    goto input;
                }
            input:
                Console.Write("?- ");
            }
        }
    }
}
