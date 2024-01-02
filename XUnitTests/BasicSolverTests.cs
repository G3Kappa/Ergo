

using Ergo.Lang.Ast;

namespace Tests;

public class TermTreeTests : ErgoTests
{
    public TermTreeTests(ErgoTestFixture fixture) : base(fixture) { }

    [Fact]
    public void ShouldSerializeCorrectly()
    {
        var parsed = InterpreterScope.Parse<ITerm>("f(a, g(c, d), E)").GetOrThrow();
        var tree = new TermCache();
        var f = parsed.ToNode(tree);
        Assert.Equal(7, tree.GetFreeId().I);
        Const(new Atom("f"), 1);
        Const(new Atom("a"), 2);
        Const(new Atom("g"), 3);
        Const(new Atom("c"), 4);
        Const(new Atom("d"), 5);
        var E = new Variable("E");
        Var(E, 6);
        Assert.Equal(6, f[tree.DefineVariable(E)].TreeIndex.I);
        Assert.Equal(3, f.Arity);
        Assert.Equal(2, f[(TermCache.StructAddr)0].TreeIndex.I);
        Assert.Equal(3, f[(TermCache.StructAddr)1].TreeIndex.I);
        Assert.Equal(6, f[(TermCache.StructAddr)2].TreeIndex.I);
        var g = f[(TermCache.StructAddr)1];
        Assert.Equal(2, g.Arity);
        Assert.Equal(4, g[(TermCache.StructAddr)0].TreeIndex.I);
        Assert.Equal(5, g[(TermCache.StructAddr)1].TreeIndex.I);
        Assert.Equal(0, tree[(TermCache.NodeAddr)2].Arity);
        Assert.Equal(0, tree[(TermCache.NodeAddr)4].Arity);
        Assert.Equal(0, tree[(TermCache.NodeAddr)5].Arity);
        Assert.Equal(0, tree[(TermCache.NodeAddr)6].Arity);

        void Const(Atom expected, int node)
        {
            var k = ((StaticTermNode)tree[(TermCache.NodeAddr)node]).Functor;
            Assert.Equal(expected, tree[k]);
        }
        void Var(Variable expected, int node)
        {
            var k = ((VariableTermNode)tree[(TermCache.NodeAddr)node]).Variable;
            Assert.Equal(expected, tree[k]);
        }
    }
    [Fact]
    public void ShouldUnifyCorrectly()
    {
        var pA = InterpreterScope.Parse<ITerm>("f(a, X, d)").GetOrThrow();
        var pB = InterpreterScope.Parse<ITerm>("f(Y, g(b, c), d)").GetOrThrow();
        var t = new TermCache();
        var (a, b) = (pA.ToNode(t), pB.ToNode(t));
        var map = new TermTreeSubstitutionMap(t);
        a.Unify(b, map);
        var (ta1, tb1) = (a.ToTerm(), b.ToTerm());
        map.ApplyForwards();
        var (ta2, tb2) = (a.ToTerm(), b.ToTerm());
        map.ApplyBackwards();
        var (ta3, tb3) = (a.ToTerm(), b.ToTerm());
        Assert.Equal(ta1, ta3);
        Assert.Equal(tb1, tb3);
        Assert.Equal(ta2, tb2);
        Assert.NotEqual(ta1, ta2);
        Assert.NotEqual(tb1, tb2);
    }

    [Theory]
    [InlineData("a", "A", false)]
    [InlineData("A", "B", true)]
    [InlineData("x(A, A)", "x(B, C)", false)]
    [InlineData("x(A, A)", "x(B, B)", true)]
    [InlineData("x(A, A)", "x(A, B)", false)]
    [InlineData("x(A, B)", "x(C, D)", true)]
    [InlineData("x(A, B)", "x(B, A)", true)]
    [InlineData("x(A, B)", "x(C, A)", true)]
    public void ShouldProduceCorrectVariantHashCodes(string a, string b, bool equal)
    {
        var pA = InterpreterScope.Parse<ITerm>(a).GetOrThrow();
        var pB = InterpreterScope.Parse<ITerm>(b).GetOrThrow();
        var t = new TermCache();
        var hA = pA.ToNode(t).GetVariantHashCode();
        var hB = pB.ToNode(t).GetVariantHashCode();
        if (equal)
            Assert.Equal(hA, hB);
        else
            Assert.NotEqual(hA, hB);
    }
}

public class BasicSolverTests : ErgoTests
{
    public BasicSolverTests(ErgoTestFixture fixture) : base(fixture) { }
    #region Rows
    [Theory]
    [InlineData("⊥", 0)]
    [InlineData("⊤", 1, "")]
    [InlineData("⊤, ⊤", 1, "")]
    [InlineData("⊤; ⊤", 2, "", "")]
    [InlineData("⊤, ⊥", 0)]
    [InlineData("⊤; ⊥", 1, "")]
    [InlineData("⊥, ⊤", 0)]
    [InlineData("⊤, (⊤, ⊤ ; ⊤, ⊤), ⊤ ; (⊤ ; (⊤, ⊤), ⊤)", 4, "", "", "", "")]
    [InlineData("⊥; ⊤", 1, "")]
    [InlineData("(⊥; ⊤), ⊥", 0)]
    [InlineData("(⊥; ⊤); ⊥", 1, "")]
    [InlineData("(⊤; ⊤), ⊤", 2, "", "")]
    [InlineData("(⊤, ⊤); ⊤", 2, "", "")]
    [InlineData("(⊤; ⊤); ⊤", 3, "", "", "")]
    [InlineData("(⊤, ⊤); (⊤, ⊤)", 2, "", "")]
    [InlineData("(⊤, ⊥); (⊥; ⊤)", 1, "")]
    [InlineData("(⊤ ; (⊤ ; (⊤ ; (⊤ ; ⊤))))", 5, "", "", "", "", "")]
    #endregion
    public void ShouldSolveConjunctionsAndDisjunctions(string query, int numSolutions, params string[] expected)
        => ShouldSolve(query, numSolutions, false, expected);
    #region Rows
    [Theory]
    [InlineData("[a,2,C]", "'[|]'(a,'[|]'(2,'[|]'(C,[])))")]
    [InlineData("[1,2,3|Rest]", "'[|]'(1,'[|]'(2,'[|]'(3,Rest)))")]
    [InlineData("[1,2,3|[a,2,_C]]", "'[|]'(1,'[|]'(2,'[|]'(3,'[|]'(a,'[|]'(2,'[|]'(_C,[]))))))")]
    [InlineData("{1,1,2,2,3,4}", "'{|}'(1,'{|}'(2,'{|}'(3, 4)))")]
    [InlineData("test{x:1, y : cool}", "dict(test, {x:1, y:cool})")]
    [InlineData("test{x:1, y : cool}", "test{x:1, y:cool}")]
    #endregion
    public void ShouldUnifyCanonicals(string term, string canonical)
        => ShouldSolve($"{term}={canonical}", 1, false, "");
    #region Rows
    [Theory]
    [InlineData("a =@= A", 0)]
    [InlineData("A =@= B", 1, "")]
    [InlineData("x(A,A) =@= x(B,C)", 0)]
    [InlineData("x(A,A) =@= x(B,B)", 1, "")]
    [InlineData("x(A,A) =@= x(A,B)", 0)]
    [InlineData("x(A,B) =@= x(C,D)", 1, "")]
    [InlineData("x(A,B) =@= x(B,A)", 1, "")]
    [InlineData("x(A,B) =@= x(C,A)", 1, "")]
    #endregion
    public void ShouldSolveVariants(string query, int numSolutions, params string[] expected)
        => ShouldSolve(query, numSolutions, false, expected);
}
