using Ergo.Abstractions.Inference;
using Ergo.Parser;
using Ergo.Structures.Monads;

namespace Ergo.Structures.Inference
{
    public abstract partial class Atom : IUnifiable<Atom>
    {
        public virtual object Value { get; protected set; }
        internal Atom(object val)
        {
            Value = val;
        }
        public static implicit operator Atom(string s) =>
            RegularExpressions.QuotedString.IsMatch(s)
                ? new String.Quoted(s)
                : new String(s);
        public static implicit operator Atom(double n) => new Number(n);
        public static implicit operator Atom(float n) => new Number(n);
        public static implicit operator Atom(int n) => new Number.Integer(n);
        public static implicit operator Atom(short n) => new Number.Integer(n);
        public static implicit operator Atom(byte n) => new Number.Integer(n);
        public abstract Maybe<Atom> UnifiesWith(Atom other);
    }
}
