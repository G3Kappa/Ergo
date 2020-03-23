using Ergo.Abstractions.Inference;
using Ergo.Structures.Inference;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ergo.Structures.Monads
{
    [DebuggerDisplay("{Canonical()}")]
    public readonly struct Clause : ICanonicalRepresentation
    {
        public readonly Goal Head;
        public readonly Query Body;
        public readonly int Arity => (Head.Term is CompoundTerm c ? c.Arity : 0);
        public readonly bool Factual;


        public Clause(Goal head, Query goals)
        {
            Head = head;
            Body = goals;
            Factual = Body.Goals.Count == 0;
        }

        public string Canonical()
        {
            return Head.Term switch
            {
                CompoundTerm c => $"{c.Functor.Canonical()}/{Arity}",
                _ => $"{Head.Term.Canonical()}/{Arity}"
            };
        }

        public override string ToString()
        {
            var head = Head; var body = Body;
            return String.Join("", Tokenize());

            IEnumerable<string> Tokenize()
            {
                yield return head.Term.Canonical();
                if (body.Goals.Count == 0 || (body.Goals.Count == 1 && body.Goals.Single().Term == Fact.True.Term)) {
                    yield return ".";
                    yield break;
                }
                yield return " :-";
                for (int i = 0; i < body.Goals.Count - 1; i++) {
                    yield return "\n\t";
                    yield return body.Goals[i].Term.Canonical();
                    yield return ",";
                }
                yield return "\n\t";
                yield return body.Goals.Last().Term.Canonical();
                yield return ".";
            }
        }

        public static implicit operator Clause(Goal f)
        {
            return new Clause(f, Array.Empty<Goal>());
        }
    }
}
