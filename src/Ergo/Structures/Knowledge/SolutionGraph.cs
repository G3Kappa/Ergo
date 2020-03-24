using Ergo.Abstractions.Inference;
using Ergo.Extensions.Inference;
using Ergo.Structures.Inference;
using System.Collections.Generic;
using System.Linq;

namespace Ergo.Structures.Knowledge
{
    public partial class SolutionGraph
    {
        public readonly Node Root;
        public SolutionGraph(Node root)
        {
            Root = root;
        }

        public IEnumerable<Solution> Solutions()
        {
            // Walk the graph and enumerate all solutions
            var rootVars = Root.Value.Variables()
                    .GroupBy(v => v.Name).Select(g => g.First())
                    .ToDictionary(v => v.Name);
            return SolutionsRec(Root, rootVars);

            IEnumerable<Solution> SolutionsRec(Node node, Dictionary<string, Variable> rootVars)
            {
                if (node == null) yield break;
                var vars = node.Value.Variables()
                    .Where(v => rootVars.ContainsKey(v.Name))
                    .GroupBy(v => v.Name).Select(g => g.First())
                    .ToDictionary(v => v.Name);
                rootVars = rootVars.ToDictionary(k => k.Key, k => k.Value);
                foreach (var v in vars) {
                    if (rootVars.ContainsKey(v.Key))
                        rootVars[v.Key] = v.Value;
                }
                if (rootVars.All(v => v.Value.IsGround())) {
                    yield return new Solution(rootVars.Select(v => new Solution.Binding(v.Key, v.Value.Value)).ToArray());
                }
                foreach (var child in node.Children) {
                    foreach (var s in SolutionsRec(child, rootVars)) {
                        yield return s;
                    }
                }
            }
        }
    }
}