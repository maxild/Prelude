using System;
using System.Collections.Generic;
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
        protected Enumeration(int value, string name) : this(value, name, name)
        {
        }

        protected Enumeration(int value, string name, string text)
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
            if (enumerationType == null)
            {
                throw new ArgumentNullException(nameof(enumerationType));
            }
            var func = ReadFunc(enumerationType) ?? WriteFunc(enumerationType);
            return func();
        }

        private static readonly ReaderWriterLockSlim LOCK = new ReaderWriterLockSlim();

        private static readonly IDictionary<Type, Func<IEnumerable<Enumeration>>> CACHED_FUNCS =
            new Dictionary<Type, Func<IEnumerable<Enumeration>>>();

        private static Func<IEnumerable<Enumeration>> ReadFunc(Type enumerationType)
        {
            LOCK.EnterReadLock();
            try
            {
                Func<IEnumerable<Enumeration>> func;
                if (CACHED_FUNCS.TryGetValue(enumerationType, out func))
                {
                    return func;
                }
                return null;
            }
            finally
            {
                LOCK.ExitReadLock();
            }
        }

        private static Func<IEnumerable<Enumeration>> WriteFunc(Type enumerationType)
        {
            LOCK.EnterWriteLock();
            try
            {
                var func = GetFunc(enumerationType);
                CACHED_FUNCS.Add(enumerationType, func);
                return func;
            }
            finally
            {
                LOCK.ExitWriteLock();
            }
        }

        static Func<IEnumerable<Enumeration>> GetFunc(Type enumerationType)
        {
            // Call Enumeration.GetAll<TEnumeration> via runtime type reference using Expession API as a mini-compiler
            MethodInfo method = typeof(Enumeration).GetRuntimeMethod("GetAll", Type.EmptyTypes);
            MethodInfo genericMethod = method.MakeGenericMethod(enumerationType);
            MethodCallExpression body = Expression.Call(genericMethod, Enumerable.Empty<Expression>());
            Expression<Func<IEnumerable<Enumeration>>> lambda = Expression.Lambda<Func<IEnumerable<Enumeration>>>(body);
            Func<IEnumerable<Enumeration>> func = lambda.Compile(); // () => Expression<TRuntimeType>.GetAll()
            return func;
        }

        public static TEnumeration FromValueOrDefault<TEnumeration>(int value)
            where TEnumeration : Enumeration<TEnumeration>
        {
            TEnumeration matchingItem = GetAll<TEnumeration>().FirstOrDefault(item => item.Value == value);
            return matchingItem;
        }

        public static Enumeration FromValueOrDefault(Type enumerationType, int value)
        {
            Enumeration matchingItem = GetAll(enumerationType).FirstOrDefault(item => item.Value == value);
            return matchingItem;
        }

        public static TEnumeration FromValue<TEnumeration>(int value)
            where TEnumeration : Enumeration<TEnumeration>
        {
            TEnumeration matchingItem = FromValueOrDefault<TEnumeration>(value);
            if (matchingItem == null)
            {
                throw new ArgumentException($"'{value}' is not a valid value for '{typeof(TEnumeration)}'.");
            }
            return matchingItem;
        }

        public static Enumeration FromValue(Type enumerationType, int value)
        {
            Enumeration matchingItem = FromValueOrDefault(enumerationType, value);
            if (matchingItem == null)
            {
                throw new ArgumentException($"'{value}' is not a valid value for '{enumerationType}'.");
            }
            return matchingItem;
        }

        public static TEnumeration FromNameOrDefault<TEnumeration>(string name)
            where TEnumeration : Enumeration<TEnumeration>
        {
            TEnumeration matchingItem = GetAll<TEnumeration>()
                .FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return matchingItem;
        }

        public static Enumeration FromNameOrDefault(Type enumerationType, string name)
        {
            Enumeration matchingItem = GetAll(enumerationType)
                .FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return matchingItem;
        }

        public static TEnumeration FromName<TEnumeration>(string name)
            where TEnumeration : Enumeration<TEnumeration>
        {
            TEnumeration matchingItem = FromNameOrDefault<TEnumeration>(name);
            if (matchingItem == null)
            {
                throw new ArgumentException($"'{name}' is not a valid name for '{typeof(TEnumeration)}'.");
            }
            return matchingItem;
        }

        public static Enumeration FromName(Type enumerationType, string name)
        {
            Enumeration matchingItem = FromNameOrDefault(enumerationType, name);
            if (matchingItem == null)
            {
                throw new ArgumentException($"'{name}' is not a valid name for '{enumerationType}'.");
            }
            return matchingItem;
        }
    }

    [Serializable]
    public abstract class Enumeration<TEnumeration> : Enumeration, IEquatable<TEnumeration>, IComparable, IComparable<TEnumeration>, IFormattable
        where TEnumeration : Enumeration<TEnumeration>
    {
        #region Cached Fields Per Type Infrastructure

        // Cached fields are lazy evaluated to give static fields a chance to initialize

        private static readonly Lazy<TEnumeration[]> CACHED_FIELDS =
            new Lazy<TEnumeration[]>(GetFields, LazyThreadSafetyMode.ExecutionAndPublication);

        static TEnumeration[] GetFields()
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
            return CACHED_FIELDS.Value;
        }

        #endregion

        protected Enumeration(int value, string name) : base(value, name)
        {
        }

        protected Enumeration(int value, string name, string text) : base(value, name, text)
		{
		}

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }
            if (GetType() != obj.GetType())
            {
                throw new ArgumentException($"A value of type '{obj.GetType().FullName}' cannot be compared to this {GetType()}{Name} value, because the types are not the same.");
            }
            return Value.CompareTo(((TEnumeration)obj).Value);
        }

        public int CompareTo(TEnumeration other)
        {
            if (other == null)
            {
                return 1;
            }
            // even here we test for type equality, because the derived classes can (in theory) be based on deep inheritance chains
            if (GetType() != other.GetType())
            {
                throw new ArgumentException($"The value {other.GetType().Name}.{other} cannot be compared to this {GetType()}{Name} value, because the enumeration types are not the same.");
            }
            return Value.CompareTo(other.Value);
        }

        public override bool Equals(object obj)
		{
		    return obj != null && GetType() == obj.GetType() && Value == ((TEnumeration) obj).Value;
		}

        public bool Equals(TEnumeration other)
        {
            // even here we test for type equality, because the derived classes can (in theory) be based on deep inheritance chains
            return other != null && GetType() == other.GetType() && Value == other.Value;
        }

		public override int GetHashCode()
		{
			return Value;
		}

		public override string ToString()
		{
			return Name;
		}

		public string ToString(string format, IFormatProvider formatProvider)
        {
            if (formatProvider != null)
            {
                var fmt = formatProvider.GetFormat(GetType()) as ICustomFormatter;
                if (fmt != null)
                {
                    return fmt.Format(format, this, formatProvider);
                }
            }

            string formatToUse = (format ?? "G").ToUpperInvariant();

            return ToStringHelper(formatToUse);
        }

        protected virtual string ToStringHelper(string format)
        {
            switch (format)
            {
                case "V":
                    return Value.ToString(CultureInfo.InvariantCulture);
                case "T":
                    return Text;
                case "G":
                    return ToString();
                default:
                    throw new FormatException($"Unsupported format '{format}'");
            }
        }
    }
}
