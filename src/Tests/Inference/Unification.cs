using Ergo.Abstractions.Inference;
using Ergo.Parser;
using Ergo.Structures.Inference;
using Ergo.Structures.Monads;
using System;
using System.Linq;
using Xunit;

namespace Tests
{
    public class Unification
    {
        [Theory]
        [InlineData("a", "a", "b")]
        [InlineData("1", "1.00", "75")]
        [InlineData("1.34", "-1.34", "32.2")]
        [InlineData("hello", "'hello'", "'hello world'")]
        public void ConstantShouldUnifyWithEqualConstants(string _a, string _b, string _c)
        {
            ITerm a = ErgolParser.Parse(_a, ErgolParser.TryParseConstant);
            ITerm b = ErgolParser.Parse(_b, ErgolParser.TryParseConstant);
            ITerm c = ErgolParser.Parse(_c, ErgolParser.TryParseConstant);

            Assert.True(a.UnifyWith(a).TryGetValue(out _));
            Assert.True(b.UnifyWith(b).TryGetValue(out _));
            Assert.True(c.UnifyWith(c).TryGetValue(out _));

            Assert.True(a.UnifyWith(b).TryGetValue(out _));
            Assert.True(b.UnifyWith(a).TryGetValue(out _));

            Assert.False(a.UnifyWith(c).TryGetValue(out _));
            Assert.False(c.UnifyWith(a).TryGetValue(out _));

            Assert.False(b.UnifyWith(c).TryGetValue(out _));
            Assert.False(c.UnifyWith(b).TryGetValue(out _));
        }

        [Theory]
        [InlineData("A", "a")]
        [InlineData("Test", "my_const")]
        [InlineData("TestVariable", "testvariable")]
        [InlineData("Test_Variable", "'Test_Variable'")]
        [InlineData("A", "1.33")]
        [InlineData("A", "5390")]
        [InlineData("A", "'Lorem Ipsum'")]
        public void VariablesShouldUnifyWithAnything(string variableName, string constantName)
        {
            ITerm variable = ErgolParser.Parse(variableName, ErgolParser.TryParseVariable);
            ITerm otherVariable = ErgolParser.Parse("Other", ErgolParser.TryParseVariable);
            ITerm constant = ErgolParser.Parse(constantName, ErgolParser.TryParseConstant);

            Assert.True(variable.UnifyWith(variable).TryGetValue(out _));
            Assert.True(otherVariable.UnifyWith(otherVariable).TryGetValue(out _));

            Assert.True(variable.UnifyWith(otherVariable).TryGetValue(out _));
            Assert.True(otherVariable.UnifyWith(variable).TryGetValue(out _));

            Assert.True(variable.UnifyWith(constant).TryGetValue(out _));
            Assert.False(constant.UnifyWith(variable).TryGetValue(out _));
        }

        [Theory]
        [InlineData("functor()", "functor()", true, true)]
        [InlineData("functor(  arg1)", "functor('arg1')", true, true)]
        [InlineData("functor(A)", "functor('arg1'  )", true, false)]
        [InlineData("functor( A,  B,C  )", "functor(A, B,   C)", true, true)]
        [InlineData("functor(A, 2,  \"a b c\")", "functor(1,   2, 'a b c')", true, false)]
        [InlineData("functor(1,   2,  \"a b c\")", "functor(1,  2,  'a b c'  )", true, true)]
        [InlineData("functor(nested(arg1, arg2))", "functor(nested(arg1, arg2))", true, true)]
        [InlineData("functor(nested(A, nested(B)))", "functor(nested(arg1, nested(arg2)))", true, false)]
        [InlineData("Invalid(nested(A, nested(B)))", "functor(nested(arg1, Invalid(arg2)))", false, false)]
        [InlineData("valid(nested(A, nested(B)))", "functor(nested(arg1, valid(arg2)))", false, false)]
        [InlineData("valid(nested(A, N))", "valid(nested(1, nested(2)))", true, false)]
        [InlineData("valid(nested(A, N))", "valid(nested(1, nested(nested(nested(2)))))", true, false)]
        public void ComplexTermsShouldUnifyWithEqualComplexTerms(string str, string cmp, bool expectedLR, bool expectedRL)
        {
            ITerm left = ErgolParser.Parse(str, ErgolParser.TryParseComplex);
            ITerm right = ErgolParser.Parse(cmp, ErgolParser.TryParseComplex);
            Assert.Equal(expectedLR, left.UnifyWith(right).TryGetValue(out _));
            Assert.Equal(expectedRL, right.UnifyWith(left).TryGetValue(out _));
        }
    }
}
