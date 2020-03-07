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

        //[Theory]
        //[InlineData("A", true, "A")]
        //[InlineData("A_Variable", true, "A_Variable")]
        //[InlineData("a_constant", false, "")]
        //[InlineData("1234", false, "")]
        //[InlineData("complex(functor(Var, Val))", true, "Var")]
        //public void RegexMatchesAllComplexTypes(string arg, bool expectedMatch, string expectedValue)
        //{
        //    if (RegularExpressions.Variable.Match(arg) is { Success: true } m) {
        //        Assert.True(expectedMatch);
        //        Assert.Equal(expectedValue, m.Value);
        //    }
        //    else Assert.False(expectedMatch);
        //}
    }
}
