using Ergo.Lang.Compiler;

namespace Ergo.Runtime.BuiltIns;

public sealed class Unify : BuiltIn
{
    public Unify()
        : base("", new("unify"), Maybe<int>.Some(2), WellKnown.Modules.Prologue)
    {
    }

    public override bool IsDeterminate(ImmutableArray<ITerm> args) => true;

    public static Complex MakeComplex(ITerm lhs, ITerm rhs) => new Complex(WellKnown.Signatures.Unify.Functor, lhs, rhs);
    public static ImmutableArray<ITerm> MakeArgs(ImmutableArray<ITerm> lhs, ImmutableArray<ITerm> rhs)
    {
        var builder = ImmutableArray<ITerm>.Empty.ToBuilder();
        builder.Add(new Complex(_u, lhs));
        builder.Add(new Complex(_u, rhs));
        return builder.ToImmutable();
    }

    public override int OptimizationOrder => base.OptimizationOrder + 10;
    public override List<ExecutionNode> OptimizeSequence(List<ExecutionNode> nodes)
    {
        PropagateConstants();
        RemoveDeadUnifications();
        return nodes;
        void PropagateConstants()
        {
            // TODO:  unify(between_(1,3,1,X),between_(X,3,1,X)))

            // By now most evaluations on constants have reduced down to the form:
            // unify(__Kx, c)
            // When we find this pattern, we can remove the unification and replace __Kx with c wherever it occurs.
            var constantUnifications = nodes.Where(x => x is BuiltInNode b && b.BuiltIn is Unify && IsConstUnif(b.Goal.GetArguments()))
                .ToDictionary(x => (Variable)((BuiltInNode)x).Goal.GetArguments()[0]);
            var subs = constantUnifications
                .Select(kv => new Substitution(kv.Key, ((BuiltInNode)kv.Value).Goal.GetArguments()[1]))
                .ToList();
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                var current = nodes[i];
                if (constantUnifications.Values.Contains(current))
                {
                    nodes.RemoveAt(i);
                    continue;
                }
                nodes[i] = current.Substitute(subs);
            }
            bool IsConstUnif(ImmutableArray<ITerm> args) => args[0] is Variable { Ignored: true } && args[1].IsGround;
        }

        void RemoveDeadUnifications()
        {
            // When compiling queries and hooks, unification stubs are generated for all arguments.
            // If these variables are not used, however, they result in dead code of the form:
            // unify(_X, _)
            // Where _X isn't referenced anywhere else. These are safe to remove.
            var refCounts = nodes.Where(x => x is DynamicNode).Cast<DynamicNode>()
                .SelectMany(x => x.Goal.Variables)
                .ToLookup(v => v.Name)
                .ToDictionary(l => l.Key, l => l.Count());
            var deadUnifications = nodes.Where(x => x is BuiltInNode b && b.BuiltIn is Unify && IsDeadUnif(b.Goal.GetArguments()))
                .ToDictionary(x => (Variable)((BuiltInNode)x).Goal.GetArguments()[0]);
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                var current = nodes[i];
                if (deadUnifications.Values.Contains(current))
                {
                    nodes.RemoveAt(i);
                    continue;
                }
            }
            bool IsDeadUnif(ImmutableArray<ITerm> args) => args[0] is Variable { Ignored: true, Name: var name } && refCounts[name] == 1;
        }
    }
    // TODO: Maybe use a name that can't be typed by the user.
    private static readonly Atom _u = new Atom("_u").AsQuoted(false);
    public override ExecutionNode Optimize(BuiltInNode node)
    {
        var args = node.Goal.GetArguments();
        // If this node was already optimized, nevermind.
        if (args[0].GetFunctor().Select(x => x.Equals(_u)).GetOr(false))
            return node;
        // If two terms don't unify they don't unify, regardless of whether they're ground or not.
        if (!args[1].Unify(args[0]).TryGetValue(out var subs))
            return FalseNode.Instance;
        // However, if the unification produced substitutions then we can't just ignore them. We can actually use them.
        if (!node.Goal.IsGround && subs.Any())
        {
            // So we put all the lvalues on one side, all the rvalues on the other, and wrap them in a complex to
            // prevent constant propagation from destroying their relationship.
            // This turns an equation like:
            // dict { a: c(1, d(2), dict { e: 3 }) } = dict { a: c(X, d(Y), _ { e: Z }) }
            // into:
            // _u(1, 2, 3) = _u(X, Y, Z)
            var lhs = new Complex(_u, subs.Select(x => x.Lhs).ToArray());
            var rhs = new Complex(_u, subs.Select(x => x.Rhs).ToArray());
            // The resulting unification has most of the redundant structure factored out so that it executes faster at runtime.
            // This should be especially noticeable in cases where there's very large structures with deep nesting like dicts.
            // TODO: This might potentially break some abstract forms like EntityAsTerm in FieroEngine. Test this rigorously.
            return new BuiltInNode(node.Dependency, MakeComplex(lhs, rhs), node.BuiltIn);
        }
        return TrueNode.Instance;
    }

    public override ErgoVM.Op Compile() => ErgoVM.Goals.Unify2;
}
