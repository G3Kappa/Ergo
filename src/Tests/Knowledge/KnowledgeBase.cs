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
        [InlineData("fact", 0, "fact")]
        [InlineData("fact(_X)", 1, "fact(test)")]
        [InlineData("fact(_X, _Y)", 2, "fact(test, A)")]
        [InlineData("fact(_X, _Y, _Z)", 3, "fact(complex(1), test(A), complex(B, 3))")]
        [InlineData("fact(_X, complex(_Y, _Z))", 2, "fact(test, complex(1, 2))")]
        [InlineData("fact(_X, complex(_Y, _Z), _X)", 3, "fact(test, complex(1, 2), test)")]
        public void KnowledgeBaseShouldUnifyAllKnownFacts(string fact, int arity, string shouldUnifyWith)
        {
            var kb = new InMemoryKnowledgeBase();
            kb.AssertFirst(ErgolParser.Parse($"{fact}.", ErgolParser.TryParseClause));
            var fact_goal = Goal.From(ErgolParser.Parse(fact, ErgolParser.TryParseTerm)).ValueOrThrow("Unreachable");
            var fact_clauses = kb.UnifyClauseHeads(fact_goal).ToList();
            Assert.Single(fact_clauses);
            Assert.Equal($"{fact}/{arity}", fact_clauses.Single().Canonical());
            Assert.Equal($"{fact}.", fact_clauses.Single().ToString());
            var unifyWith_goal = Goal.From(ErgolParser.Parse(shouldUnifyWith, ErgolParser.TryParseTerm)).ValueOrThrow("Unreachable");
            foreach (var k in fact_clauses) {
                var unified = k.Head.Term.UnifyWith(unifyWith_goal.Term).ValueOrThrow("Unreachable");
                Assert.Equal(shouldUnifyWith, unified.Canonical());
            }
        }

        [Theory]
        [InlineData("fact.", "fact.", null)]
        [InlineData("fact(mario, loves, peach).", "fact(X, loves, Y).", "X = mario, Y = peach")]
        [InlineData("fact(mario, loves, both(peach, coins)).", "fact(X, loves, Y).", "X = mario, Y = both(peach, coins)")]
        [InlineData("fact(mario, loves, both(peach, coins)).", "fact(X, loves, both(Y, Z)).", "X = mario, Y = peach, Z = coins")]
        public void KnowledgeBaseShouldSolveAllKnownFacts(string fact, string query, string solution)
        {
            var kb = new InMemoryKnowledgeBase();
            kb.AssertFirst(ErgolParser.Parse(fact, ErgolParser.TryParseClause));
            var ans = kb.Solve(ErgolParser.Parse(query, ErgolParser.TryParseQuery));
            var slv = ans.ToList();
            Assert.True(ans.Result.TryGetValue(out var res) && res);
            if(solution != null) {
                Assert.Single(slv);
                Assert.Equal(solution, slv.Single().Canonical());
            }
        }

    }
}
