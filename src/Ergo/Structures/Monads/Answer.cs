using Ergo.Structures.Inference;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Ergo.Structures.Monads
{
    public sealed class Answer : IEnumerable<Solution>
    {
        private IEnumerable<Solution> Solutions;
        private readonly LinkedList<Solution> Graph;
        private readonly Stack<LinkedListNode<Solution>> ChoicePoints;
        public readonly Query Query;
        public Maybe<bool> Result { get; private set; } = Maybe.None;

        public Answer(Query query, LinkedList<Solution> graph, Stack<LinkedListNode<Solution>> choicePoints, IEnumerable<Solution> solutions)
        {
            Query = query;
            Solutions = solutions
                .Where(s => s.Variables.Length > 0);
            Graph = graph;
            ChoicePoints = choicePoints;
        }

        public IEnumerator<Solution> GetEnumerator()
        {
            if(!Result.TryGetValue(out _)) {
                Solutions = Solutions.ToList();
                if(!Solutions.Any()) {
                    Result = Maybe.Some(Graph.Count > 0);
                }
                else {
                    Result = Maybe.Some(true);
                }
            }
            return Solutions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Solutions.GetEnumerator();
        }
    }
}
