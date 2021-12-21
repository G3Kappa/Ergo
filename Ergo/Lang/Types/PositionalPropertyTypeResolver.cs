﻿using System;
using System.Linq;
using System.Reflection;

namespace Ergo.Lang
{
    internal class PositionalPropertyTypeResolver<T> : ITypeResolver
    {
        public Type Type => typeof(T);

        protected readonly PropertyInfo[] Properties;
        protected readonly Func<object, Atom> GetFunctor;
        protected readonly bool IsAtomic;


        public PositionalPropertyTypeResolver()
        {
            Properties = Type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (Type == typeof(char) || Type == typeof(string))
            {
                IsAtomic = true;
                GetFunctor = o => new Atom(o.ToString());
            }
            else if (Type == typeof(bool))
            {
                IsAtomic = true;
                GetFunctor = o => new Atom(o);
            }
            else if (Type.IsPrimitive)
            {
                IsAtomic = true;
                GetFunctor = o => new Atom((double)o);
            }
            else
            {
                IsAtomic = false;
                GetFunctor = o => new Atom(Type.Name.ToLower());
            }
        }

        public object FromITerm(ITerm t)
        {
            if (IsAtomic)
            {
                return ((Atom)t).Value;
            }
            if(Type.Namespace == null)
            {
                // anonymous type
                var constructor = Type.GetConstructors().Single();
                var constructorParameters = constructor.GetParameters();
                var args = ((Complex)t).Arguments.Select((a, i) =>
                {
                    var value = ITermMarshall.FromITerm(a, Properties[i].PropertyType, ITermMarshall.MarshallingMode.Positional);
                    value = Convert.ChangeType(value, constructorParameters[i].ParameterType);
                    return value;
                })
                    .ToArray();
                var instance = constructor.Invoke(args);
                return instance;
            }
            else
            {
                var instance = Activator.CreateInstance(Type);
                var args = ((Complex)t).Arguments;
                for (int i = 0; i < Properties.Length; i++)
                {
                    var value = ITermMarshall.FromITerm(args[i], Properties[i].PropertyType, ITermMarshall.MarshallingMode.Positional);
                    value = Convert.ChangeType(value, Properties[i].PropertyType);
                    Properties[i].SetValue(instance, value);
                }
                return instance;
            }
        }

        public ITerm ToITerm(object o)
        {
            if (o.GetType() != Type) throw new ArgumentException(null, nameof(o));

            var functor = GetFunctor(o);
            if (IsAtomic) return functor;

            return new Complex(functor, Properties.Select(
                v => ITermMarshall.ToITerm(v.GetValue(o), ITermMarshall.MarshallingMode.Positional)
            ).ToArray());
        }
    }
}
