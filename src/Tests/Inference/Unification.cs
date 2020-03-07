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
            ITerm a = ErgolParser.TryParseConstant(_a).ValueOrThrow(nameof(_a));
            ITerm b = ErgolParser.TryParseConstant(_b).ValueOrThrow(nameof(_b));
            ITerm c = ErgolParser.TryParseConstant(_c).ValueOrThrow(nameof(_c));

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
            ITerm variable = ErgolParser.TryParseVariable(variableName).ValueOrThrow("Parser fail!");
            ITerm otherVariable = ErgolParser.TryParseVariable("Other").ValueOrThrow("Parser fail!");
            ITerm constant = ErgolParser.TryParseConstant(constantName).ValueOrThrow("Parser fail!");

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
        [InlineData("valid(nested(A, valid(B)))", "functor(nested(arg1, valid(arg2)))", false, false)]
        public void ComplexTermsShouldUnifyWithEqualComplexTerms(string str, string cmp, bool expectedLR, bool expectedRL)
        {
            ITerm left  = ErgolParser.TryParseComplex(str).ValueOrThrow("Parser fail!");
            ITerm right = ErgolParser.TryParseComplex(cmp).ValueOrThrow("Parser fail!");
            Assert.Equal(expectedLR, left.UnifyWith(right).TryGetValue(out _));
            Assert.Equal(expectedRL, right.UnifyWith(left).TryGetValue(out _));
        }
    }
}
