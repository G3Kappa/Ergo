namespace Ergo.Lang.Ast;

internal sealed class InvalidTermNode : TermNode
{
    public InvalidTermNode() : base(null, 0) { }
    public override void Dispose() => throw new InvalidOperationException();
    public override ITerm ToTerm() => throw new InvalidOperationException();
}

