﻿using System.Collections.Concurrent;
using System.Reflection;

namespace Ergo.Lang;

public sealed class TermMarshall
{

    internal static readonly ConcurrentDictionary<Type, ITypeResolver> PositionalResolvers = new();
    internal static readonly ConcurrentDictionary<Type, ITypeResolver> NamedResolvers = new();

    internal static ITypeResolver EnsurePositionalResolver(Type t)
    {
        if (!PositionalResolvers.TryGetValue(t, out var resolver))
        {
            resolver = (ITypeResolver)Activator.CreateInstance(typeof(PositionalPropertyTypeResolver<>).MakeGenericType(t));
            PositionalResolvers.AddOrUpdate(t, resolver, (t, r) => r);
        }

        return resolver;
    }

    internal static ITypeResolver EnsureNamedResolver(Type t)
    {
        if (!NamedResolvers.TryGetValue(t, out var resolver))
        {
            resolver = (ITypeResolver)Activator.CreateInstance(typeof(NamedPropertyTypeResolver<>).MakeGenericType(t));
            NamedResolvers.AddOrUpdate(t, resolver, (t, r) => r);
        }

        return resolver;
    }

    private static TermMarshalling GetMode(Type type, Maybe<TermMarshalling> mode = default) => mode
        .GetOr(type.GetCustomAttribute<TermAttribute>()?.Marshalling ?? TermMarshalling.Positional);


    public static ITerm ToTerm<T>(T value, Maybe<Atom> functor = default, Maybe<TermMarshalling> mode = default, TermMarshallingContext ctx = null)
    {
        ctx ??= new();
        return GetMode(typeof(T), mode) switch
        {
            var m when ctx.TryGetCached(m, value, typeof(T), functor, out var cached) => cached,
            TermMarshalling.Positional => ctx.Cache(TermMarshalling.Positional, value, typeof(T), EnsurePositionalResolver(typeof(T)).ToTerm(value, functor, default, ctx)),
            TermMarshalling.Named => ctx.Cache(TermMarshalling.Named, value, typeof(T), EnsureNamedResolver(typeof(T)).ToTerm(value, functor, default, ctx)),
            _ => throw new NotImplementedException()
        };
    }
    public static ITerm ToTerm(object value, Type type, Maybe<Atom> functor = default, Maybe<TermMarshalling> mode = default, TermMarshallingContext ctx = null)
    {
        ctx ??= new();
        return GetMode(type, mode) switch
        {
            var m when ctx.TryGetCached(m, value, type, functor, out var cached) => cached,
            TermMarshalling.Positional => ctx.Cache(TermMarshalling.Positional, value, type, EnsurePositionalResolver(type).ToTerm(value, functor, default, ctx)),
            TermMarshalling.Named => ctx.Cache(TermMarshalling.Named, value, type, EnsureNamedResolver(type).ToTerm(value, functor, default, ctx)),
            _ => throw new NotImplementedException()
        };
    }
    public static T FromTerm<T>(ITerm value, T _ = default, Maybe<TermMarshalling> mode = default) =>
        GetMode(typeof(T), mode) switch
        {
            TermMarshalling.Positional => (T)Convert.ChangeType(EnsurePositionalResolver(typeof(T)).FromTerm(value), typeof(T)),
            TermMarshalling.Named => (T)Convert.ChangeType(EnsureNamedResolver(typeof(T)).FromTerm(value), typeof(T)),
            _ => throw new NotImplementedException()
        };
    public static object FromTerm(ITerm value, Type type, Maybe<TermMarshalling> mode = default) =>
        GetMode(type, mode) switch
        {
            TermMarshalling.Positional => EnsurePositionalResolver(type).FromTerm(value),
            TermMarshalling.Named => EnsureNamedResolver(type).FromTerm(value),
            _ => throw new NotImplementedException()
        };
}
