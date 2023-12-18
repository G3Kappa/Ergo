﻿namespace Ergo.Interpreter.Libraries;


public class Hook
{
    private static readonly InstantiationContext ctx = new("_H");
    private readonly ITerm[] args;
    public readonly Signature Signature;
    private Maybe<ErgoVM.Op> cached = default;
    private Complex head = default;
    public Hook(Signature sig)
    {
        Signature = sig;
        args = new ITerm[Signature.Arity.GetOr(ErgoVM.MAX_ARGUMENTS)];
        for (int i = 0; i < args.Length; i++)
            args[i] = ctx.GetFreeVariable();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetArg(int i, ITerm arg)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, args.Length);
        args[i] = arg;
    }

    public ErgoVM.Op Compile(bool throwIfNotDefined = true)
    {
        return vm =>
        {
            if (cached.TryGetValue(out var op))
            {
                op(vm);
                return;
            }
            // Compile and cache the hook the first time it's called
            // TODO: Invalidate cache when any predicate matching this hook is asserted or retracted
            if (!vm.KnowledgeBase.Get(Signature).TryGetValue(out var preds))
            {
                if (throwIfNotDefined)
                    vm.Throw(ErgoVM.ErrorType.UndefinedPredicate, Signature.Explain());
                else
                    vm.Fail();
                return;
            }
            var ops = new ErgoVM.Op[preds.Count];
            for (int i = 0; i < preds.Count; i++)
            {
                var predHead = preds[i].Unqualified().Head;
                var graph = preds[i].ExecutionGraph.GetOr(default).Compile();
                ops[i] = vm =>
                {
                    vm.SetArg(0, head);
                    vm.SetArg(1, predHead);
                    ErgoVM.Goals.Unify2(vm);
                    if (vm.State == ErgoVM.VMState.Fail)
                        return;
                    graph(vm);
                };
            }
            var branch = ErgoVM.Ops.Or(ops);
            cached = op = vm =>
            {
                head = new Complex(Signature.Functor, args);
                branch(vm);
            };
            op(vm);
        };
    }

}