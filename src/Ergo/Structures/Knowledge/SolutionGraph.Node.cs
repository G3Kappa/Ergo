using Ergo.Structures.Monads;
using System.Collections.Generic;

namespace Ergo.Structures.Knowledge
{
    public partial class SolutionGraph
    {
        public class Node
        {
            public readonly Node Parent;
            public readonly Query Value;
            public readonly List<Node> Children;

            public Node(Query query, Node parent = null)
            {
                Parent = parent;
                Value = query;
                Children = new List<Node>();
            }
            public override string ToString()
            {
                return Value.ToString();
            }
        }
    }
}