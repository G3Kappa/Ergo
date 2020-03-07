using Ergo.Parser;
using Ergo.Structures.Monads;
using System;
using System.Text.RegularExpressions;

namespace Ergo.Structures.Inference
{
    public abstract partial class Atom
    {
        public partial class String
        {
            private readonly Regex Space = new Regex(@"\s", RegularExpressions.DefaultOptions);
            private readonly Regex LeftQuote = new Regex(@"^\s*['""]", RegularExpressions.DefaultOptions);
            private readonly Regex RightQuote = new Regex(@"['""]\s*$", RegularExpressions.DefaultOptions);

            public class Quoted : String
            {
                public Quoted(string val) : base(val) {  }
                public Maybe<String> TrySimplify()
                {
                    if (Space.IsMatch(Value))
                        return Maybe.Some(new String(RightQuote.Replace(LeftQuote.Replace(Value, "'"), "'")));
                    return Maybe.Some(new String(RightQuote.Replace(LeftQuote.Replace(Value, ""), "")));
                }
                public override Maybe<Atom> UnifiesWith(Atom other)
                {
                    return other switch {
                        String s => TrySimplify().Map(s => s.UnifiesWith(other)).ValueOr(Maybe.None),
                        _ => Maybe.None
                    };
                }
            }
        }
    }
}
