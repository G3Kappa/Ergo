﻿using Ergo.Lang.Ast.Terms.Interfaces;

namespace Ergo.Lang.Compiler;

public sealed class TermMemory(int cS = 1024, int vS = 1024, int sS = 1024, int aS = 1024)
{
    public readonly record struct State(
        uint CP,
        ITermAddress[] Variables,
        ITermAddress[][] Structures,
        AbstractCell[] Abstracts
    );
    public readonly record struct AbstractCell(IAbstractTermCompiler Compiler, ITermAddress Address, Type Type);
    private readonly Atom[] Atoms = new Atom[cS];
    private readonly ITermAddress[] Variables = new ITermAddress[vS];
    private readonly ITermAddress[][] Structures = new ITermAddress[sS][];
    private readonly AbstractCell[] Abstracts = new AbstractCell[aS];
    public uint CP = 0, VP = 0, SP = 0, AP = 0;
    public uint NumAtoms => (uint)Atoms.Length;

    internal Dictionary<string, VariableAddress> VariableLookup = new();
    internal Dictionary<VariableAddress, string> InverseVariableLookup = new();
    internal Dictionary<int, ITermAddress> TermLookup = new();

    public void Clear()
    {
        CP = 0;
        VP = 0;
        SP = 0;
        AP = 0;
        Array.Clear(Atoms);
        Array.Clear(Variables);
        Array.Clear(Structures);
        Array.Clear(Abstracts);
        TermLookup.Clear();
        VariableLookup.Clear();
        InverseVariableLookup.Clear();
    }

    public TermMemory Clone()
    {
        var state = SaveState();
        var mem = new TermMemory(cS, vS, sS, aS)
        {
            TermLookup = TermLookup,
            VariableLookup = VariableLookup,
            InverseVariableLookup = InverseVariableLookup
        };
        mem.LoadState(state);
        return mem;
    }

    public State SaveState()
    {
        var variables = new ITermAddress[VP];
        var structures = new ITermAddress[SP][];
        var abstracts = new AbstractCell[AP];
        Array.Copy(Variables, variables, VP);
        Array.Copy(Abstracts, abstracts, AP);
        for (int i = 0; i < SP; i++)
        {
            structures[i] = new ITermAddress[Structures[i].Length];
            Array.Copy(Structures[i], structures[i], Structures[i].Length);
        }
        return new State(CP, variables, structures, abstracts);
    }

    public void LoadState(State state)
    {
        (CP, VP, SP, AP) = (
            state.CP,
            (uint)state.Variables.Length,
            (uint)state.Structures.Length,
            (uint)state.Abstracts.Length
        );
        Array.Copy(state.Variables, Variables, VP);
        Array.Copy(state.Structures, Structures, SP);
        Array.Copy(state.Abstracts, Abstracts, AP);
    }

    public ConstAddress StoreAtom(Atom value)
    {
        var hash = value.GetHashCode();
        if (TermLookup.TryGetValue(hash, out var c))
            return (ConstAddress)c;
        var addr = new ConstAddress(CP++);
        this[addr] = value;
        TermLookup[hash] = addr;
        return addr;
    }
    public VariableAddress StoreVariable(string name = null)
    {
        if (name != null && VariableLookup.TryGetValue(name, out var v))
            return v;
        var addr = new VariableAddress(VP++);
        this[addr] = VariableLookup[name] = addr;
        InverseVariableLookup[addr] = name;
        return addr;
    }
    public StructureAddress StoreStructure(params ITermAddress[] args)
    {
        var addr = new StructureAddress(SP++);
        this[addr] = args;
        return addr;
    }
    public AbstractAddress StoreAbstract(AbstractTerm term)
    {
        var addr = new AbstractAddress(AP++);
        this[addr] = new(term.Compiler, term.Compiler.Store(this, term), term.GetType());
        return addr;
    }
    public Atom this[ConstAddress c]
    {
        get => Atoms[c.Index];
        internal set => Atoms[c.Index] = value;
    }
    public ITermAddress this[VariableAddress c]
    {
        get => Variables[c.Index];
        internal set => Variables[c.Index] = value;
    }
    public ITermAddress[] this[StructureAddress c]
    {
        get => Structures[c.Index];
        internal set => Structures[c.Index] = value;
    }
    public AbstractCell this[AbstractAddress c]
    {
        get => Abstracts[c.Index];
        internal set => Abstracts[c.Index] = value;
    }
}
