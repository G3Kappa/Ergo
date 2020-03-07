using Ergo.Parser;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Tests.Parser
{
    public class Terms
    {
        [Theory]
        [InlineData("A", false, "", null)]
        [InlineData("A_Variable", false, "", null)]
        [InlineData("'A_Variable'", true, "string", "'A_Variable'")]
        [InlineData("193.20", true, "decimal", "193.20")]
        [InlineData("0", true, "integer", "0")]
        [InlineData("-1", true, "integer", "1")]
        [InlineData("14", true, "integer", "14")]
        [InlineData("hello", true, "atom", "hello")]
        [InlineData("'hello world'", true, "string", "'hello world'")]
        [InlineData("\"hello world\"", true, "string", "\"hello world\"")]
        public void RegexMatchesAllConstantTypes(string arg, bool expectedMatch, string expectedGroup, string expectedValue)
        {
            if (RegularExpressions.Constant.Match(arg) is { Success: true, Groups: { } groups } m) {
                Assert.True(groups[expectedGroup].Success);
                Assert.Equal(expectedValue, groups[expectedGroup].Value);
            }
            else Assert.False(expectedMatch);
        }

        [Theory]
        [InlineData("A", true, "A")]
        [InlineData("A_Variable", true, "A_Variable")]
        [InlineData("a_constant", false, "")]
        [InlineData("1234", false, "")]
        [InlineData("complex(functor(Var, Val))", true, "Var")]
        public void RegexMatchesAllVariableTypes(string arg, bool expectedMatch, string expectedValue)
        {
            if (RegularExpressions.Variable.Match(arg) is { Success: true } m) {
                Assert.True(expectedMatch);
                Assert.Equal(expectedValue, m.Value);
            }
            else Assert.False(expectedMatch);
        }

        [Theory]
        [InlineData("complex(A)", true, "complex", "(A)")]
        [InlineData("complex(   A   )", true, "complex", "(   A   )")]
        [InlineData("complex(A, B , C )", true, "complex", "(A, B , C )")]
        [InlineData("complex(nested(term, Var, 302))", true, "complex", "(nested(term, Var, 302))")]
        public void RegexMatchesAllComplexTypes(string arg, bool expectedMatch, string expectedFunctor, string expectedArguments)
        {
            if (RegularExpressions.Complex.Match(arg) is { Success: true, Groups: var groups }) {
                Assert.True(groups["functor"].Success);
                Assert.True(groups["arguments"].Success);
                Assert.Equal(expectedFunctor, groups["functor"].Value);
                Assert.Equal(expectedArguments, groups["arguments"].Value);
            }
            else Assert.False(expectedMatch);
        }

        [Theory]
        [InlineData("complex(       )", "complex()")]
        [InlineData("complex( A, B )", "complex(A, B)")]
        [InlineData("complex( A, nested(C) )", "complex(A, nested(C))")]
        [InlineData("complex( A, b(C, d( E , F  )) )", "complex(A, b(C, d(E, F)))")]
        [InlineData("complex( A, b(C, d( E , nested(F, 43)  )) )", "complex(A, b(C, d(E, nested(F, 43))))")]
        public void ParserBuildsAllComplexTypes(string input, string canonical)
        {
            var a = ErgolParser.TryParseComplex(input).ValueOrThrow("Parser fail!");
            Assert.Equal(canonical, a.CanonicalRepresentation());
        }
    }
}
