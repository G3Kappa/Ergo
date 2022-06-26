﻿using Ergo.Solver.DataBindings;
using Ergo.Solver.DataBindings.Interfaces;

namespace Ergo.Solver;

public sealed class DataSource<T> : IDataSource
    where T : new()
{
    public readonly Atom Functor;
    public readonly DataSource Source;
    public event Action<DataSource<T>, T, ITerm> ItemYielded;

    async IAsyncEnumerable<ITerm> FromEnumerable(Func<IEnumerable<T>> data)
    {
        foreach (var item in data())
        {
            await Task.CompletedTask;
            var term = TermMarshall.ToTerm(item);
            yield return term;
            ItemYielded?.Invoke(this, item, term);
        }
    }

    async IAsyncEnumerable<ITerm> FromAsyncEnumerable(Func<IAsyncEnumerable<T>> data)
    {
        await foreach (var item in data())
        {
            await Task.CompletedTask;
            var term = TermMarshall.ToTerm(item);
            yield return term;
            ItemYielded?.Invoke(this, item, term);
        }
    }

    public IAsyncEnumerator<ITerm> GetAsyncEnumerator(CancellationToken cancellationToken = default) => Source.GetAsyncEnumerator(cancellationToken);

    public DataSource(Func<IEnumerable<T>> source, Maybe<Atom> functor = default, RejectionData rejectSemantics = RejectionData.Discard, RejectionControl enumSemantics = RejectionControl.Continue)
    {
        var signature = ErgoSolver.GetDataSignature<T>(functor);
        Functor = signature.Tag.GetOr(signature.Functor);
        Source = new(() => FromEnumerable(source), rejectSemantics, enumSemantics);
    }

    public DataSource(Func<IAsyncEnumerable<T>> source, Maybe<Atom> functor = default, RejectionData dataSemantics = RejectionData.Discard, RejectionControl ctrlSemantics = RejectionControl.Continue)
    {
        var signature = ErgoSolver.GetDataSignature<T>(functor);
        Functor = signature.Tag.GetOr(signature.Functor);
        Source = new(() => FromAsyncEnumerable(source), dataSemantics, ctrlSemantics);
    }
}