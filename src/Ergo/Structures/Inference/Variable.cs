using Ergo.Abstractions.Inference;
using Ergo.Exceptions;
using Ergo.Structures.Monads;
using System;
using System.Diagnostics;

namespace Ergo.Structures.Inference
{
    [DebuggerDisplay("{DebugView()}")]
    public class Variable : ITerm
    {
        private static int _GID = -1;
        public static int GlobalId() => _GID = ++_GID % 100;

        public string Name { get; }
        public Maybe<ITerm> Reference { get; set; }
        public bool Instantiated => Reference.TryGetValue(out _);
        public ITerm Value {
            get {
                if (!Reference.TryGetValue(out var @ref))
                    return this;
                if (@ref is Variable v)
                    return v.Value;
                return @ref;
            }
        }

        public Variable(string name, Maybe<ITerm> instance = default)
        {
            // _    = anonymous discard
            // _Var = ignored singleton
            if (name == "_") {
                Name = $"_G{GlobalId():00}";
            }
            else {
                Name = name;
            }
            Reference = instance;
        }

        public Maybe<ITerm> UnifyWith(ITerm other)
        {
            if (other == this)
                return this;

            return other switch {
                Variable v when v.Instantiated => Update(this, v.Value),
                _ => Update(this, other) 
            };

            static Maybe<ITerm> Update(Variable a, ITerm bi)
            {
                if(a.Reference.TryGetValue(out var ai)) {
                    if (ai.UnifyWith(bi).TryGetValue(out var u)) {
                        a.Reference = Maybe.Some(u);
                        return a;
                    }
                    else return Maybe.None;
                }
                a.Reference = new Variable("_", Maybe.Some(bi));
                return a;
            }
        }
        public string Canonical()
        {
            if(Reference.TryGetValue(out _)) {
                return $"{Name} = {Value.Canonical()}";
            }
            return Name;
        }
        public bool IsGround() => Reference.TryGetValue(out var t) && t.IsGround();
        internal string DebugView()
        {
            return $"{Canonical()} ({GetHashCode()})";
        }
    }
}
