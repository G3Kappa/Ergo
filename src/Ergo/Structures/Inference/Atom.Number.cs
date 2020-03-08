using Ergo.Structures.Monads;

namespace Ergo.Structures.Inference
{
    public abstract partial class Atom
    {
        public partial class Number : Atom
        {
            internal Number(double val) : base(val) { }
            public new double Value => (double)base.Value;
            public override Maybe<Atom> UnifyWith(Atom other)
            {
                return other switch
                {
                    Integer i when i.Value == (int)Value => Maybe.Some(other),
                    Number n when n.Value == Value => Maybe.Some(other),
                    _ => Maybe.None
                };
            }
            public override string Canonical()
            {
                return Value.ToString("#.###");
            }
        }
    }
}
