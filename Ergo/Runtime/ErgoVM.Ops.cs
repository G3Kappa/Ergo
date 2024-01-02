namespace Ergo.Runtime;

public readonly record struct RuntimePredicate(Signature Signature, ErgoVM.Op Op);

public class KnowledgeRepo
{
    protected readonly List<RuntimePredicate> predicates = new();

    protected static int GetVariantHashCode(ITerm head)
    {
        return head.NumberVars().GetHashCode();
    }
}



public partial class ErgoVM
{
    public static class Ops
    {
        public static Op NoOp => _ => { };
        public static Op Fail => vm => vm.Fail();
        public static Op Throw(ErrorType ex, params object[] args) => vm => vm.Throw(ex, args);
        public static Op Cut => vm => vm.Cut();
        public static Op Solution => vm => vm.Solution();
        public static Op And2(Op a, Op b) => vm =>
        {
            // Cache continuation so that goals calling PushChoice know where to continue from.
            var @continue = vm.@continue;
            vm.@continue += b;
            a(vm);
            // Restore previous continuation before potentially yielding control to another And.
            vm.@continue = @continue;
            switch (vm.State)
            {
                case VMState.Fail: return;
                case VMState.Solution: vm.MergeEnvironment(); break;
            }
            b(vm);
        };
        public static Op And(params Op[] goals)
        {
            if (goals.Length == 0)
                return NoOp;
            if (goals.Length == 1)
                return goals[0];
            return vm =>
            {
                ContinueFrom(vm, 0);
                void ContinueFrom(ErgoVM vm, int j)
                {
                    if (j >= goals.Length) return;
                    for (int i = j; i < goals.Length; i++)
                    {
                        var k = i + 1;
                        // Cache continuation so that goals calling PushChoice know where to continue from.
                        var @continue = vm.@continue;
                        vm.@continue = k >= goals.Length
                        ? @continue
                        : @continue == NoOp
                            ? vm => ContinueFrom(vm, k)
                            : vm => { @continue(vm); ContinueFrom(vm, k); };
                        goals[i](vm);
                        // Restore previous continuation before potentially yielding control to another And.
                        vm.@continue = @continue;
                        switch (vm.State)
                        {
                            // Ready can be used as a non-failing way to halt with a solution.
                            case VMState.Ready:
                                {
                                    vm.State = VMState.Solution;
                                    return;
                                }
                            case VMState.Fail: return;
                            case VMState.Solution: vm.MergeEnvironment(); break;
                        }
                    }
                    vm.Solution();
                }
            };
        }
        public static Op Or(params Op[] branches)
        {
            if (branches.Length == 0)
                return Fail;
            if (branches.Length == 1)
                return branches[0];
            return vm =>
            {
                for (int i = branches.Length - 1; i >= 1; i--)
                {
                    vm.PushChoice(Branch(branches[i]));
                }
                Branch(branches[0])(vm);

                Op Branch(Op branch) => vm =>
                {
                    branch(vm);
                    vm.SuccessToSolution();
                };
            };
        }
        public static Op IfThenElse(Op condition, Op consequence, Op alternative) => vm =>
        {
            var backupEnvironment = vm.CloneEnvironment();
            var numCp = vm.NumChoicePoints;
            condition(vm);
            if (vm.State != VMState.Fail)
            {
                if (vm.State == VMState.Solution)
                    vm.MergeEnvironment();
                // Discard choice points created by the condition. Similar to a cut but more specific.
                vm.DiscardChoices(vm.NumChoicePoints - numCp);
                consequence(vm);
            }
            else
            {
                vm.State = VMState.Success;
                vm.Environment = backupEnvironment;
                alternative(vm);
                vm.SuccessToSolution();
            }
        };
        public static Op IfThen(Op condition, Op consequence) => IfThenElse(condition, consequence, NoOp);
        /// <summary>
        /// Adds the current set of substitutions to te VM's environment, and then releases it back into the substitution map pool.
        /// </summary>
        public static Op UpdateEnvironment(SubstitutionMap subsToAdd) => vm =>
            {
                vm.Environment.AddRange(subsToAdd);
                SubstitutionMap.Pool.Release(subsToAdd);
            };
        public static Op SetEnvironment(SubstitutionMap newEnv) => vm =>
        {
            SubstitutionMap.Pool.Release(vm.Environment);
            vm.Environment = newEnv;
        };
        /// <summary>
        /// Converts a query into the corresponding Op.
        /// </summary>
        public static Op Goals(NTuple goals)
        {
            if (goals.Contents.Length == 0)
                return NoOp;
            if (goals.Contents.Length == 1)
                return Goal(goals.Contents[0]);
            return And(goals.Contents.Select(x => Goal(x, dynamic: false)).ToArray());
        }
        /// <summary>
        /// Calls an individual goal.
        /// </summary>
        public static Op Goal(ITerm goal, bool dynamic = false)
        {
            const string cutValue = "!";
            return goal switch
            {
                NTuple tup => Goals(tup),
                Atom { Value: true } => NoOp,
                Atom { Value: false } => Fail,
                Atom { Value: cutValue, IsQuoted: false } => Cut,
                _ => Resolve
            };

            void Resolve(ErgoVM vm)
            {
                var newGoal = goal.Substitute(vm.Environment);
                var matchEnum = GetEnumerator(vm, newGoal);
                NextMatch(vm);
                void NextMatch(ErgoVM vm)
                {
                    var anyMatch = false;
                TCO:
                    // In the non-tail recursive case, you can imagine this 'while' as if it were an 'if'.
                    while (matchEnum.MoveNext())
                    {
                        if (dynamic && !matchEnum.Current.Predicate.IsDynamic)
                            continue;
                        anyMatch = true;
                        // Push a choice point for this match. If it fails, it will be retried until there are no more matches.
                        vm.PushChoice(NextMatch);
                        // Update the environment by adding the current match's substitutions.
                        if (matchEnum.Current.Substitutions != null)
                            vm.Environment.AddRange(matchEnum.Current.Substitutions);
                        // Decide how to execute this goal depending on whether:
                        Op runGoal = NoOp;
                        var pred = matchEnum.Current.Predicate;
                        // - It's a builtin (we can run it directly with low overhead)
                        if (pred.BuiltIn.TryGetValue(out var builtIn))
                        {
                            matchEnum.Current.Goal.GetQualification(out var inst);
                            var args = inst.GetArguments();
                            vm.Arity = args.Length;
                            for (int i = 0; i < args.Length; i++)
                                vm.SetArg(i, args[i]);
                            runGoal = builtIn.Compile();
                        }
                        // - It has an execution graph (we can run it directly with low overhead if there's a cached compiled version)
                        else if (pred.ExecutionGraph.TryGetValue(out var graph))
                            runGoal = graph.Compile();
                        // - It has to be interpreted (we have to run it traditionally)
                        else if (!pred.IsFactual) // probably a dynamic goal with no associated graph
                            runGoal = Goals(pred.Body);
                        // Track the number of choice points before executing the goal (up to the one we just pushed)
                        var numCp = vm.NumChoicePoints;
                        // Actually execute the goal. This may produce success, a solution, or set the VM in a failure state.
                        runGoal(vm);
                        // If the VM is in success state, promote that success to a solution by pushing the current environment.
                        vm.SuccessToSolution();
                        // If this is a tail call of pred, then we can recycle the current stack frame (hence the top-level 'while').
                        if (pred.IsTailRecursive && Predicate.IsTailCall(newGoal, pred.Body)
                            /*&& vm.Flag(VMFlags.ContinuationIsDet)*/)
                        {
                            // Pop all choice points that were created by this predicate.
                            // TODO: figure out if this is actually the correct thing to do.
                            while (vm.NumChoicePoints > numCp)
                            {
                                var cp = vm.PopChoice().GetOr(default);
                                // Set the environment to that of the oldest popped choice point.
                                SubstitutionMap.Pool.Release(vm.Environment);
                                vm.Environment = cp.Environment;
                                // If runGoal failed, set the vm back to success as we're retrying now.
                                if (vm.State == VMState.Fail)
                                    vm.State = VMState.Success;
                            }
                            // If the above loop didn't run and runGoal failed, then we can't retry so we exit the outer loop.
                            if (vm.State == VMState.Fail)
                                break;
                            // Keep the list of substitutions that contributed to this iteration.
                            var bodyVars = pred.Body.Variables.ToHashSet();
                            var tcoSubs = vm.Environment
                                .Where(s => bodyVars.Contains((Variable)s.Lhs));
                            // Substitute the tail call with this list, creating the new head, and qualify it with the current module.
                            newGoal = pred.Body.Contents.Last().Substitute(tcoSubs)
                                .Qualified(pred.DeclaringModule);
                            // Remove all substitutions that are no longer relevant, including those we just used.
                            vm.Environment.RemoveRange(tcoSubs.Concat(matchEnum.Current.Substitutions));
                            vm.DiscardChoices(1); // We don't need the NextMatch choice point anymore.
                            matchEnum = GetEnumerator(vm, newGoal);
                            goto TCO;
                        }
                        // Non-tail recursive predicates don't benefit from the while loop and must backtrack as normal.
                        else break;
                    }
                    // If the 'while' above were an 'if', this would be the 'else' branch.
                    if (!anyMatch)
                    {
                        // Essentially, when we exhaust the list of matches for 'goal', we set the VM in a failure state to signal backtracking.
                        vm.Fail();
                    }
                }
            }
            IEnumerator<KBMatch> GetEnumerator(ErgoVM vm, ITerm newGoal)
            {
                if (!vm.KB.GetMatches(vm.InstantiationContext, newGoal, false).TryGetValue(out var matches))
                {
                    return Enumerable.Empty<KBMatch>().GetEnumerator();
                }
                return matches.GetEnumerator();
            }
        }
    }
}
