﻿namespace Ergo.Lang.Compiler;

public class TrueNode : StaticNode
{
    public static readonly TrueNode Instance = new();
    public override ErgoVM.Op Compile() => ErgoVM.Ops.NoOp;
    public override string Explain(bool canonical = false) => $"⊤";
}
