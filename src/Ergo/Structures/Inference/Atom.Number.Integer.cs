using Ergo.Structures.Monads;

namespace Ergo.Structures.Inference
{
    public abstract partial class Atom
    {
        public partial class Number
        {
            public class Integer : Number
            {
                internal Integer(int val) : base(val) { }
                public new int Value => (int)base.Value;
                public override Maybe<Atom> UnifyWith(Atom other)
                {
                    return other switch
                    {
                        Integer i when i.Value == Value => Maybe.Some(other),
                        Number n when (int)n.Value == Value => Maybe.Some(other),
                        _ => Maybe.None
                    };
                }

                public override string Canonical()
                {
                    return Value.ToString();
                }
            }
        }
    }
}
