using Ergo.Exceptions;
using Ergo.Parser;
using Ergo.Structures.Monads;
using System;
using System.Text.RegularExpressions;

namespace Ergo.Structures.Inference
{
    public abstract partial class Atom
    {
        public partial class String : Atom
        {
            private static readonly Regex Space = new Regex(@"\s", RegularExpressions.DefaultOptions);
            private static readonly Regex LeftQuote = new Regex(@"^\s*['""]", RegularExpressions.DefaultOptions);
            private static readonly Regex RightQuote = new Regex(@"['""]\s*$", RegularExpressions.DefaultOptions);

            public static String Make(string val)
            {
                if (Space.IsMatch(val)) {
                    if (!LeftQuote.IsMatch(val) || !RightQuote.IsMatch(val))
                        throw new ArgumentException("Missing or mismatched quotes for atomic string containing spaces.");
                    return new String(RightQuote.Replace(LeftQuote.Replace(val, "'"), "'"));
                }
                else {
                    return new String(RightQuote.Replace(LeftQuote.Replace(val, ""), ""));
                }
            }

            internal String(string val) : base(val) { }
            public new string Value => (string)base.Value;

            public override Maybe<Atom> UnifyWith(Atom other)
            {
                return other switch {
                    String s when System.String.Equals(Value, s.Value, StringComparison.Ordinal) => Maybe.Some(other),
                    _ => Maybe.None
                };
            }

            public override string CanonicalRepresentation()
            {
                return Value;
            }
        }
    }
}
