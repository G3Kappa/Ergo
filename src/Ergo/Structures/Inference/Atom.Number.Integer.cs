using Ergo.Structures.Monads;

namespace Ergo.Structures.Inference
{
    public abstract partial class Atom
    {
        public partial class Number
        {
            public class Integer : Number
            {
                public Integer(int val) : base(val) { }
                public new int Value => (int)base.Value;
                public override Maybe<Atom> UnifiesWith(Atom other)
                {
                    return other switch
                    {
                        Integer i when i.Value == Value => Maybe.Some(other),
                        Number n when (int)n.Value == Value => Maybe.Some(other),
                        _ => Maybe.None
                    };
                }
            }
        }
    }
}
