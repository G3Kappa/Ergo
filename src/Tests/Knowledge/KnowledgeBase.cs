using Ergo.Extensions.Inference;
using Ergo.Parser;
using Ergo.Structures.Inference;
using Ergo.Structures.Knowledge;
using Ergo.Structures.Monads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Tests.Knowledge
{
    public class KnowledgeBase
    {
        [Theory]
        [InlineData("fact", "fact")]
        [InlineData("fact(_X)", "fact(test)")]
        [InlineData("fact(_X, _Y)", "fact(test, A)")]
        [InlineData("fact(_X, _Y, _Z)", "fact(complex(1), test(A), complex(B, 3))")]
        [InlineData("fact(_X, complex(_Y, _Z))", "fact(test, complex(1, 2))")]
        [InlineData("fact(_X, complex(_Y, _Z), _X)", "fact(test, complex(1, 2), test)")]
        public void KnowledgeBaseShouldUnifyAllKnownFacts(string fact, string shouldUnifyWith)
        {
            //var kb = new InMemoryKnowledgeBase();
            //kb.AssertFirst(ErgolParser.Parse($"{fact}.", ErgolParser.TryParseClause));
            //var fact_goal = Goal.From(ErgolParser.Parse(fact, ErgolParser.TryParseTerm)).ValueOrThrow("Unreachable");
            //var fact_clauses = kb.UnifyClauses(fact_goal).ToList();
            //Assert.Single(fact_clauses);
            //var unifyWith_goal = Goal.From(ErgolParser.Parse(shouldUnifyWith, ErgolParser.TryParseTerm)).ValueOrThrow("Unreachable");
            //foreach (var k in fact_clauses) {
            //    var unified = k.Head.Term.UnifyWith(unifyWith_goal.Term).ValueOrThrow("Unreachable");
            //    Assert.Equal(shouldUnifyWith, unified.Canonical());
            //}
        }

        [Theory]
        [InlineData("fact.", "fact.", null)]
        [InlineData("fact(mario, loves, peach).", "fact(X, loves, Y).", "X = mario, Y = peach")]
        [InlineData("fact(mario, loves, both(peach, coins)).", "fact(X, loves, Y).", "X = mario, Y = both(peach, coins)")]
        [InlineData("fact(mario, loves, both(peach, coins)).", "fact(X, loves, both(Y, Z)).", "X = mario, Y = peach, Z = coins")]
        public void KnowledgeBaseShouldSolveAllKnownFacts(string clause, string query, string solution)
        {
            //var kb = new InMemoryKnowledgeBase();
            //kb.AssertFirst(ErgolParser.Parse(clause, ErgolParser.TryParseClause));
            //var ans = kb.Solve(ErgolParser.Parse(query, ErgolParser.TryParseQuery));
            //var slv = ans.ToList();
            //Assert.True(ans.Result.TryGetValue(out var res) && res);
            //if (solution != null) {
            //    Assert.Single(slv);
            //    Assert.Equal(solution, slv.Single().Canonical());
            //}
        }

        [Theory]
        [InlineData("fact :-\n\tother_fact1,\n\tother_fact2.", "fact.", null, "other_fact1.", "other_fact2.")]
        [InlineData("fact(_) :-\n\tother_fact.", "fact(X).", null, "other_fact.")]
        //[InlineData("fact(_) :-\n\tother_fact(_).", "fact(X).", null, "other_fact(A) :-\n\ttest(A).", "test(_).")]
        [InlineData("fact(A) :-\n\tother_fact(A).", "fact(A).", "A = mario", "other_fact(mario).")]
        [InlineData("fact(A) :-\n\ttest(A),\n\ttest_2(A),\n\ttest_3(A).", "fact(A).", "A = mario", "test(mario).", "test_2(mario).", "test_3(mario).")]
        [InlineData("fact(A) :-\n\ttest(A),\n\ttest_2(A),\n\ttest_3(A).", "fact(X).", "X = mario", "test(mario).", "test_2(mario).", "test_3(mario).")]
        [InlineData("fact(A) :-\n\tother_fact(A).", "fact(A).", "A = luigi", "other_fact(B) :-\n\tother_other_fact(B).", "other_other_fact(luigi).")]
        [InlineData("jealous(A, B) :-\n\tloves(A, C),\n\tloves(B, C).", "jealous(A, B).", "A = luigi, B = luigi ; A = luigi, B = waluigi ; A = waluigi, B = waluigi ; A = waluigi, B = luigi", "loves(luigi, daisy).", "loves(waluigi, daisy).")]
        [InlineData("jealous(A, B, C) :-\n\tloves(A, C),\n\tloves(B, C).", "jealous(A, B, C).", "A = luigi, B = luigi, C = daisy ; A = luigi, B = waluigi ; A = waluigi, B = waluigi ; A = waluigi, B = luigi", "loves(luigi, daisy).", "loves(waluigi, daisy).")]
        [InlineData("test(A) :-\n\tfact(A).", "test(A).", "A = mario ; A = luigi", "fact(mario).", "fact(luigi).")]
        public void KnowledgeBaseShouldSolveAllKnownPredicates(string clause, string query, string solution, params string[] assertions)
        {
            //var kb = new InMemoryKnowledgeBase();
            //foreach (var str in assertions) {
            //    kb.AssertLast(ErgolParser.Parse(str, ErgolParser.TryParseClause));
            //}
            //kb.AssertLast(ErgolParser.Parse(clause, ErgolParser.TryParseClause));
            //var ans = kb.Solve(ErgolParser.Parse(query, ErgolParser.TryParseQuery));
            //var slv = ans.ToList();
            //Assert.True(ans.Result.TryGetValue(out var res) && res);
            //if (solution != null) {
            //    Assert.Equal(solution, String.Join(" ; ", slv.Select(s => s.Canonical())));
            //}

            var kb = new InMemoryKnowledgeBase();
            kb.AssertLast(ErgolParser.Parse("loves(marcellus, mia).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("loves(vincent, mia).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("indirect_loves(A, B) :-\n\tloves(A, B).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("indirect_loves2(F, E) :-\n\tindirect_loves(F, E).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("jealous(A, B):-\n\tloves(A, C),\n\tloves(B, C).", ErgolParser.TryParseClause));

            var res = kb.Solve(Goal.From(ErgolParser.Parse("jealous(X, Y)", ErgolParser.TryParseTerm)).ValueOrThrow("Unreachable"));
        }
    }
}
