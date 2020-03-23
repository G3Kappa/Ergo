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
        [Fact]
        public void KnowledgeBaseShouldMatchFacts()
        {
            var kb = new InMemoryKnowledgeBase();
            kb.AssertLast(ErgolParser.Parse("fact.", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("not_a_fact :--> false.", ErgolParser.TryParseClause));
            var res = kb.Solve(ErgolParser.Parse("fact.", ErgolParser.TryParseQuery));
            var sol = res.Solutions().ToList();
            Assert.Single(sol);
            res = kb.Solve(ErgolParser.Parse("not_a_fact.", ErgolParser.TryParseQuery));
            sol = res.Solutions().ToList();
            Assert.Empty(sol);
        }

        [Fact]
        public void VariablesShouldUnifyThroughAssignment()
        {
            var kb = new InMemoryKnowledgeBase();
            kb.AssertLast(ErgolParser.Parse("fact(a, b).", ErgolParser.TryParseClause));
            var res = kb.Solve(ErgolParser.Parse("fact(A, B).", ErgolParser.TryParseQuery));
            var sol = res.Solutions().ToList();
            Assert.Single(sol);
            res = kb.Solve(ErgolParser.Parse("fact(A, A).", ErgolParser.TryParseQuery));
            sol = res.Solutions().ToList();
            Assert.Empty(sol);
            kb.AssertLast(ErgolParser.Parse("fact(one, one, two).", ErgolParser.TryParseClause));
            res = kb.Solve(ErgolParser.Parse("fact(A, A, B).", ErgolParser.TryParseQuery));
            sol = res.Solutions().ToList();
            Assert.Single(sol);
        }

        [Fact]
        public void KnowledgeBaseShouldSolveJealousXY()
        {
            var kb = new InMemoryKnowledgeBase();
            kb.AssertLast(ErgolParser.Parse("loves(marcellus, mia).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("loves(vincent, mia).", ErgolParser.TryParseClause));
            kb.AssertLast(ErgolParser.Parse("jealous(A, B):-\n\tloves(A, C),\n\tloves(B, C).", ErgolParser.TryParseClause));
            var res = kb.Solve(ErgolParser.Parse("jealous(X, Y).", ErgolParser.TryParseQuery));
            var sol = res.Solutions().ToList();
            Assert.Equal(4, sol.Count);
            Assert.Equal("X = marcellus, Y = marcellus", sol[0].Canonical());
            Assert.Equal("X = marcellus, Y = vincent", sol[1].Canonical());
            Assert.Equal("X = vincent, Y = marcellus", sol[2].Canonical());
            Assert.Equal("X = vincent, Y = vincent", sol[3].Canonical());
        }
    }
}
