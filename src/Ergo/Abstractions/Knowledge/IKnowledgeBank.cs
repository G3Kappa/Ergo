using Ergo.Structures.Knowledge;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Ergo.Abstractions.Knowledge
{
    public interface IKnowledgeBank
    {
        ValueTask<bool> Store(Fact f); 
        ValueTask<Fact> Read(Fact f); 
    }
}
