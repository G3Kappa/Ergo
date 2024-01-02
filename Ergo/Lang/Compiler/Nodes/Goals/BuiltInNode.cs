using Ergo.Runtime.BuiltIns;

namespace Ergo.Lang.Compiler;

public class BuiltInNode : GoalNode
{
    public BuiltIn BuiltIn { get; }
    protected ErgoVM.Op CompiledBuiltIn { get; private set; }
    public readonly ImmutableArray<ITerm> Args;

    public BuiltInNode(DependencyGraphNode node, ITerm goal, BuiltIn builtIn, bool compile = true) : base(node, goal)
    {
        BuiltIn = builtIn;
        Goal.GetQualification(out var head);
        Args = head.GetArguments();
        if (compile)
        {
            CompiledBuiltIn = BuiltIn.Compile();
            RuntimeHelpers.PrepareDelegate(CompiledBuiltIn);
        }
    }
    public override ErgoVM.Op Compile() => vm =>
    {
        vm.Arity = Args.Length;
        for (int i = 0; i < Args.Length; i++)
            vm.SetArg(i, Args[i].Substitute(vm.Environment));
        vm.SetFlag(VMFlags.ContinuationIsDet, IsContinuationDet);
        CompiledBuiltIn(vm);
    };

    public override int OptimizationOrder => base.OptimizationOrder + BuiltIn.OptimizationOrder;
    public override bool IsDeterminate => BuiltIn.IsDeterminate(Args);
    public override ExecutionNode Optimize()
    {
        return BuiltIn.Optimize(this);
    }
    public override List<ExecutionNode> OptimizeSequence(List<ExecutionNode> nodes)
    {
        return BuiltIn.OptimizeSequence(nodes);
    }

    public override ExecutionNode Instantiate(InstantiationContext ctx, Dictionary<string, Variable> vars = null)
    {
        if (IsGround) return this;
        return new BuiltInNode(Dependency, Goal.Instantiate(ctx, vars), BuiltIn, compile: false)
        {
            CompiledBuiltIn = CompiledBuiltIn
        };
    }
    public override ExecutionNode Substitute(IEnumerable<Substitution> s)
    {
        if (IsGround) return this;
        return new BuiltInNode(Dependency, Goal.Substitute(s), BuiltIn, compile: false)
        {
            CompiledBuiltIn = CompiledBuiltIn
        };
    }
}
