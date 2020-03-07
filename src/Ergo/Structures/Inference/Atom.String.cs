using Ergo.Structures.Monads;
using System;

namespace Ergo.Structures.Inference
{
    public abstract partial class Atom
    {
        public partial class String : Atom
        {
            public String(string val) : base(val) { }
            public new string Value => (string)base.Value;

            public override Maybe<Atom> UnifiesWith(Atom other)
            {
                return other switch {
                    Quoted q => q.TrySimplify().Map(s => s.UnifiesWith(this)).ValueOrThrow("Unreachable"),
                    String s when System.String.Equals(Value, s.Value, StringComparison.Ordinal) => Maybe.Some(other),
                    _ => Maybe.None
                };
            }
        }
    }
}
