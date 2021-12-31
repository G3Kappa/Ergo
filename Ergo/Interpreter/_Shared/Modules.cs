﻿using Ergo.Lang.Ast;

namespace Ergo.Interpreter
{
    public static class Modules
    {
        public static readonly Atom Prologue = new("prologue");
        public static readonly Atom List = new("list");
        public static readonly Atom Math = new("math");
        public static readonly Atom Meta = new("meta");


        public static readonly Atom __BuiltIn = new("__builtin");
        public static readonly Atom Stdlib = new("stdlib");
        public static readonly Atom User = new("user");
    }
}
