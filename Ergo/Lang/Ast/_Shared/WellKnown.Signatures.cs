﻿namespace Ergo.Lang.Ast;

public static partial class WellKnown
{
    public static class Signatures
    {
        public static readonly Signature Unify = new("unify", 2, WellKnown.Modules.Prologue, default);
    }
}