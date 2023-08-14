using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Maxfire.Prelude
{
    [Serializable]
    public abstract class Enumeration
    {
        protected Enumeration(int value, string name, string? text = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("The name cannot be null or empty.");
            }
            Value = value;
            Name = name;
            Text = text ?? name;
        }

        //
        // Enumeration value (so simple out 'of the box')
        //

        public int Value { get; }

        public string Name { get; }

        public string Text { get; }

        //
        // Enumeration 'Parse and Iterate' API (LINQ friendly API)
        //

        public static IEnumerable<TEnumeration> GetAll<TEnumeration>()
            where TEnumeration : Enumeration<TEnumeration>
        {
            return Enumeration<TEnumeration>.GetCachedFields();
        }

        public static IEnumerable<Enumeration> GetAll(Type enumerationType)
        {
            if (enumerationType is null)
            {
                throw new ArgumentNullException(nameof(enumerationType));
            }
            var func = ReadFunc(enumerationType) ?? WriteFunc(enumerationType);
            return func();
        }

        private static readonly ReaderWriterLockSlim s_lock = new ReaderWriterLockSlim();

        private static readonly IDictionary<Type, Func<IEnumerable<Enumeration>>> s_cachedFuncs =
            new Dictionary<Type, Func<IEnumerable<Enumeration>>>();

        private static Func<IEnumerable<Enumeration>>? ReadFunc(Type enumerationType)
        {
            s_lock.EnterReadLock();
            try
            {
                return s_cachedFuncs.TryGetValue(enumerationType, out var func) ? func : null;
            }
            finally
            {
                s_lock.ExitReadLock();
            }
        }

        private static Func<IEnumerable<Enumeration>> WriteFunc(Type enumerationType)
        {
            s_lock.EnterWriteLock();
            try
            {
                var func = GetFunc(enumerationType);
                s_cachedFuncs.Add(enumerationType, func);
                return func;
            }
            finally
            {
                s_lock.ExitWriteLock();
            }
        }

        static Func<IEnumerable<Enumeration>> GetFunc(Type enumerationType)
        {
            // Call Enumeration.GetAll<TEnumeration> via runtime type reference using Expression API as a mini-compiler
            MethodInfo? method = typeof(Enumeration).GetRuntimeMethod(nameof(GetAll), Type.EmptyTypes);
            Debug.Assert(!(method is null));
            MethodInfo genericMethod = method.MakeGenericMethod(enumerationType);
            MethodCallExpression body = Expression.Call(genericMethod, Enumerable.Empty<Expression>());
            Expression<Func<IEnumerable<Enumeration>>> lambda = Expression.Lambda<Func<IEnumerable<Enumeration>>>(body);
            Func<IEnumerable<Enumeration>> func = lambda.Compile(); // () => Expression<TRuntimeType>.GetAll()
            return func;
        }

        public static TEnumeration? FromValueOrDefault<TEnumeration>(int value)
            where TEnumeration : Enumeration<TEnumeration>
        {
            var matchingItem = GetAll<TEnumeration>().FirstOrDefault(item => item.Value == value);
            return matchingItem;
        }

        public static Enumeration? FromValueOrDefault(Type enumerationType, int value)
        {
            var matchingItem = GetAll(enumerationType).FirstOrDefault(item => item.Value == value);
            return matchingItem;
        }

        public static TEnumeration FromValue<TEnumeration>(int value)
            where TEnumeration : Enumeration<TEnumeration>
        {
            var matchingItem = FromValueOrDefault<TEnumeration>(value);
            if (matchingItem is null)
            {
                throw new ArgumentException($"'{value}' is not a valid value for '{typeof(TEnumeration)}'.");
            }
            return matchingItem;
        }

        public static Enumeration FromValue(Type enumerationType, int value)
        {
            var matchingItem = FromValueOrDefault(enumerationType, value);
            if (matchingItem is null)
            {
                throw new ArgumentException($"'{value}' is not a valid value for '{enumerationType}'.");
            }
            return matchingItem;
        }

        public static TEnumeration? FromNameOrDefault<TEnumeration>(string name)
            where TEnumeration : Enumeration<TEnumeration>
        {
            var matchingItem = GetAll<TEnumeration>()
                .FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return matchingItem;
        }

        public static Enumeration? FromNameOrDefault(Type enumerationType, string name)
        {
            var matchingItem = GetAll(enumerationType)
                .FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return matchingItem;
        }

        public static TEnumeration FromName<TEnumeration>(string name)
            where TEnumeration : Enumeration<TEnumeration>
        {
            var matchingItem = FromNameOrDefault<TEnumeration>(name);
            if (matchingItem is null)
            {
                throw new FormatException($"'{name}' is not a valid name for '{typeof(TEnumeration)}'.");
            }
            return matchingItem;
        }

        public static Enumeration FromName(Type enumerationType, string name)
        {
            var matchingItem = FromNameOrDefault(enumerationType, name);
            if (matchingItem is null)
            {
                throw new FormatException($"'{name}' is not a valid name for '{enumerationType}'.");
            }
            return matchingItem;
        }

#if NET
        public static TEnumeration? FromNameOrDefault<TEnumeration>(ReadOnlySpan<char> name)
            where TEnumeration : Enumeration<TEnumeration>
        {
            // Cannot use span (byref-like type) in lambda
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var item in GetAll<TEnumeration>())
            {
                if (name.Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                    return item;
            }

            return null;
        }

        public static Enumeration? FromNameOrDefault(Type enumerationType, ReadOnlySpan<char> name)
        {
            // Cannot use span (byref-like type) in lambda
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var item in GetAll(enumerationType))
            {
                if (name.Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                    return item;
            }

            return null;
        }

        public static TEnumeration FromName<TEnumeration>(ReadOnlySpan<char> name)
            where TEnumeration : Enumeration<TEnumeration>
        {
            var matchingItem = FromNameOrDefault<TEnumeration>(name);
            if (matchingItem is null)
            {
                throw new FormatException($"'{name}' is not a valid name for '{typeof(TEnumeration)}'.");
            }
            return matchingItem;
        }

        public static Enumeration FromName(Type enumerationType, ReadOnlySpan<char> name)
        {
            var matchingItem = FromNameOrDefault(enumerationType, name);
            if (matchingItem is null)
            {
                throw new FormatException($"'{name}' is not a valid name for '{enumerationType}'.");
            }
            return matchingItem;
        }
#endif
    }

    [Serializable]
    public abstract class Enumeration<TEnumeration> : Enumeration, IEquatable<TEnumeration>, IComparable, IComparable<TEnumeration>, IFormattable
        where TEnumeration : Enumeration<TEnumeration>
    {
        #region Cached Fields Per Type Infrastructure

        // Cached fields are lazy evaluated to give static fields a chance to initialize

        private static readonly Lazy<TEnumeration[]> s_cachedFields =
            new Lazy<TEnumeration[]>(GetFields, LazyThreadSafetyMode.ExecutionAndPublication);

        private static TEnumeration[] GetFields()
        {
            // get all the public, static, declared fields using the reflection api
            return typeof(TEnumeration)
                .GetTypeInfo()
                .DeclaredFields
                .Where(fieldInfo => fieldInfo.IsPublic && fieldInfo.IsStatic)
                .Select(field => field.GetValue(null))
                .OfType<TEnumeration>()
                .ToArray();
        }

        internal static TEnumeration[] GetCachedFields()
        {
            return s_cachedFields.Value;
        }

        #endregion

        protected Enumeration(int value, string name) : base(value, name)
        {
        }

        protected Enumeration(int value, string name, string? text) : base(value, name, text)
        {
        }

        public int CompareTo(object? obj)
        {
            if (obj is null)
            {
                return 1;
            }
            if (GetType() != obj.GetType())
            {
                throw new ArgumentException($"A value of type '{obj.GetType().FullName}' cannot be compared to this {GetType()}{Name} value, because the types are not the same.");
            }
            return Value.CompareTo(((TEnumeration)obj).Value);
        }

        public int CompareTo(TEnumeration? other)
        {
            if (other is null)
            {
                return 1;
            }
            // even here we test for type equality, because the derived classes can (in theory) be based on deep inheritance chains
            if (GetType() != other.GetType())
            {
                throw new ArgumentException(
                    $"The value {other.GetType().Name}.{other} cannot be compared to this {GetType()}{Name} value, because the enumeration types are not the same.");
            }
            return Value.CompareTo(other.Value);
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return !(obj is null) && GetType() == obj.GetType() && Value == ((TEnumeration) obj).Value;
        }

        public virtual bool Equals([NotNullWhen(true)] TEnumeration? other)
        {
            // even here we test for type equality, because the derived classes can (in theory) be based on deep inheritance chains
            return !(other is null) && GetType() == other.GetType() && Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Name;

        public string ToString(string? format) => ToStringHelper(format ?? "G");

        string IFormattable.ToString(string? format, IFormatProvider? formatProvider)
        {
            if (formatProvider?.GetFormat(GetType()) is ICustomFormatter fmt)
            {
                return fmt.Format(format, this, formatProvider);
            }

            var formatToUse = (format ?? "G").ToUpperInvariant();

            return ToStringHelper(formatToUse);
        }

        protected virtual string ToStringHelper(string format) =>
            format switch
            {
                "V" => Value.ToString(CultureInfo.InvariantCulture),
                "T" => Text,
                "N" => Name,
                "G" => ToString(),
                _ => throw new FormatException($"Unsupported format '{format}'")
            };
    }
}
