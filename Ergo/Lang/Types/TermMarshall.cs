﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Ergo.Lang
{
    public partial class TermMarshall
    {
        internal static readonly ConcurrentDictionary<Type, ITypeResolver> PositionalResolvers = new();
        internal static readonly ConcurrentDictionary<Type, ITypeResolver> NamedResolvers = new();

        internal static ITypeResolver EnsurePositionalResolver(Type t)
        {
            if (!PositionalResolvers.TryGetValue(t, out var resolver))
            {
                resolver = (ITypeResolver)Activator.CreateInstance(typeof(PositionalPropertyTypeResolver<>).MakeGenericType(t));
                NamedResolvers.AddOrUpdate(t, resolver, (t, r) => r);
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

        public static ITerm ToTerm<T>(T value, MarshallingMode mode = MarshallingMode.Positional) =>
            mode switch
            {
                MarshallingMode.Positional => EnsurePositionalResolver(typeof(T)).ToTerm(value),
                MarshallingMode.Named => EnsureNamedResolver(typeof(T)).ToTerm(value),
                _ => throw new NotImplementedException()
            };
        public static T FromTerm<T>(ITerm value, T _ = default, MarshallingMode mode = MarshallingMode.Positional) =>
            mode switch
            {
                MarshallingMode.Positional => (T)EnsurePositionalResolver(typeof(T)).FromTerm(value),
                MarshallingMode.Named => (T)EnsureNamedResolver(typeof(T)).FromTerm(value),
                _ => throw new NotImplementedException()
            };
        public static object FromTerm(ITerm value, Type type, MarshallingMode mode = MarshallingMode.Positional) =>
            mode switch
            {
                MarshallingMode.Positional => EnsurePositionalResolver(type).FromTerm(value),
                MarshallingMode.Named => EnsureNamedResolver(type).FromTerm(value),
                _ => throw new NotImplementedException()
            };
    }
}
