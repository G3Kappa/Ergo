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
                    //var ans = kb.Solve(query).ToList();
                    //if(ans.Count == 0) {
                    //    Console.WriteLine("No.");
                    //    goto input;
                    //}
                    //foreach (var solution in ans) {
                    //    Console.Write("\t ; ");
                    //    Console.WriteLine(solution.Canonical());
                    //}
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
