﻿namespace Ergo.Lang.Ast
{
    public static class Literals
    {
        public static readonly ITerm Discard = new Variable("_");
        public static readonly ITerm True = new Atom(true);
        public static readonly ITerm False = new Atom(false);
        public static readonly ITerm Cut = new Atom("!");
        public static readonly ITerm EmptyList = List.EmptyLiteral;
        public static readonly ITerm EmptyCommaExpression = CommaSequence.EmptyLiteral;

        public static readonly ITerm[] DefinedLiterals = new[]
        {
            Discard, True, False, Cut, EmptyList, EmptyCommaExpression
        };
    }
}