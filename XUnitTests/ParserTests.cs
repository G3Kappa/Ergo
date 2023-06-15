﻿using Ergo.Lang.Ast;
namespace Tests;

public static class MockWellKnown
{
    public static class Operators
    {
        public static readonly Operator Addition = new(WellKnown.Modules.Math, Fixity.Infix, OperatorAssociativity.None, 500, WellKnown.Functors.Addition);
        public static readonly Operator Subtraction = new(WellKnown.Modules.Math, Fixity.Infix, OperatorAssociativity.None, 500, WellKnown.Functors.Subtraction);
        public static readonly Operator DictAccess = new(WellKnown.Modules.Math, Fixity.Infix, OperatorAssociativity.Left, 900, WellKnown.Functors.DictAccess);
    }
}

public class ParserTests : ErgoTests
{
    public ParserTests(ErgoTestFixture fixture) : base(fixture) { }
    [Theory]
    [InlineData("0", 0)]
    [InlineData("0.5", 0.5)]
    [InlineData("0  .5", 0.5)]
    [InlineData("0. 5", 0.5)]
    [InlineData("0 .  5", 0.5)]
    [InlineData(".5", .5)]
    [InlineData(".   5", .5)]
    public void ShouldParseDecimals(string query, object constructor)
        => ShouldParse(query, new Atom(constructor));
    [Theory]
    [InlineData("+26", +26)]
    [InlineData("+ 63", +63)]
    [InlineData("+06.4592", +06.4592)]
    [InlineData("-.19438", -.19438)]
    [InlineData("-. 19438", -.19438)]
    [InlineData("+.19438", +.19438)]
    [InlineData("+. 19438", +.19438)]
    [InlineData("-2", -2)]
    [InlineData("- 3", -3)]
    [InlineData("+ .  0", +.0)]
    [InlineData("+.  015", +.015)]
    public void ShouldParseSignedNumbers(string query, decimal number)
    {
        var op = number < 0 ? MockWellKnown.Operators.Subtraction : MockWellKnown.Operators.Addition;
        ShouldParse(query, new Expression(new Complex(op.CanonicalFunctor, new Atom(Math.Abs(number))).AsOperator(op), InterpreterScope));
    }

    [Fact]
    public void ShouldParsePathologicalCases_ParensInArgs1()
        => ShouldParse("f((V,L,R))",
            new Complex(new Atom("f"),
                new NTuple(new ITerm[] { new Variable("V"), new Variable("L"), new Variable("R") }).CanonicalForm.AsParenthesized(true)));
    [Fact]
    public void ShouldParsePathologicalCases_ParensInArgs2()
        => ShouldParse("f(N, n, (V,L,R))",
            new Complex(new Atom("f"), new Variable("N"), new Atom("n"),
                new NTuple(new ITerm[] { new Variable("V"), new Variable("L"), new Variable("R") }).CanonicalForm.AsParenthesized(true)));
    [Fact]
    public void ShouldParsePathologicalCases_PeriodAsInfix()
        => ShouldParse("a.b",
            new Expression(new Complex(MockWellKnown.Operators.DictAccess.CanonicalFunctor, new Atom("a"), new Atom("b"))
                .AsOperator(MockWellKnown.Operators.DictAccess), InterpreterScope));
}
