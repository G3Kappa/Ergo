﻿namespace Ergo.Shell.Commands;

public sealed class PrintPredicates : PredicatesShellCommand
{
    public PrintPredicates() : base(new[] { "::", "desc" }, "Displays the head of all matching predicates.", false) { }
}
