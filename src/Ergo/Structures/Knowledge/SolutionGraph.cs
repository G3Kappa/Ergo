using Ergo.Abstractions.Inference;
using Ergo.Extensions.Inference;
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
            return SolutionsRec(Root);

            IEnumerable<Solution> SolutionsRec(Node node, Dictionary<string, ITerm> variables = null)
            {
                if (node == null) yield break;
                if(variables is null) {
                    variables = node.Value.Variables()
                        .Where(v => !v.Name.StartsWith("_"))
                        .GroupBy(v => v.Name).Select(g => g.First())
                        .ToDictionary(v => v.Name, v => (ITerm)v);
                }
                else if(variables.All(t => t.Value.IsGround())) {
                    yield return new Solution(variables.Select(v => new Solution.Binding(v.Key, v.Value)).ToArray());
                }

                foreach (var c in node.Children) {
                    var cVars = c.Value.Variables()
                        .GroupBy(v => v.Name).Select(g => g.First())
                        .ToDictionary(v => v.Name);
                    var newVars = variables
                        .ToDictionary(v => v.Key, v => cVars.TryGetValue(v.Key, out var cV) ? cV.Value : v.Value);
                    foreach (var s in SolutionsRec(c, newVars))
                        yield return s;
                }

            }
        }
    }
}