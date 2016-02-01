using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Maxfire.Prelude
{
#if !DNXCORE50
    [Serializable]
#endif
    public abstract class Enumeration
    {
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

#if !DNXCORE50
    [Serializable]
#endif
    public abstract class Enumeration<TEnumeration> : Enumeration, IEquatable<TEnumeration>, IComparable, IComparable<TEnumeration>, IFormattable, IConvertible
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

        protected Enumeration(int value, string name) : base(value, name, name)
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
		    var fmt = formatProvider?.GetFormat(GetType()) as ICustomFormatter;
		    if (fmt != null)
		    {
		        return fmt.Format(format, this, formatProvider);
		    }

		    format = format ?? "G";

			switch (format.ToUpperInvariant())
			{
				case "V":
					return Value.ToString();
				case "T":
					return Text;
				case "G":
					return ToString();
				default:
					throw new FormatException($"Unsupported format '{format}'");
			}
		}

        TypeCode IConvertible.GetTypeCode()
        {
            return TypeCode.Int32;
        }

        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(Value, provider);
        }

        char IConvertible.ToChar(IFormatProvider provider)
        {
            return Convert.ToChar(Value, provider);
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return Convert.ToSByte(Value, provider);
        }

        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return Convert.ToByte(Value, provider);
        }

        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return Convert.ToInt16(Value, provider);
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return Convert.ToUInt16(Value, provider);
        }

        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(Value, provider);
        }

        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return Convert.ToUInt32(Value, provider);
        }

        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return Convert.ToInt64(Value, provider);
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return Convert.ToUInt64(Value, provider);
        }

        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return Convert.ToSingle(Value, provider);
        }

        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return Convert.ToDouble(Value, provider);
        }

        decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return Convert.ToDecimal(Value, provider);
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            throw new InvalidCastException(GetInvalidCastMessage(typeof(DateTime)));
        }

        string IConvertible.ToString(IFormatProvider provider)
        {
            return ToString();
        }

        object IConvertible.ToType(Type targetType, IFormatProvider provider)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));
            if (GetType() == targetType || targetType == typeof(object))
                return this;

            IConvertible convertible = this;

            if (targetType == typeof(bool))
            {
                return convertible.ToBoolean(provider);
            }
            if (targetType == typeof(char))
            {
                return convertible.ToChar(provider);
            }
            if (targetType == typeof(sbyte))
            {
                return convertible.ToSByte(provider);
            }
            if (targetType == typeof(byte))
            {
                return convertible.ToByte(provider);
            }
            if (targetType == typeof(short))
            {
                return convertible.ToInt16(provider);
            }
            if (targetType == typeof(ushort))
            {
                return convertible.ToUInt16(provider);
            }
            if (targetType == typeof(int))
            {
                return convertible.ToInt32(provider);
            }
            if (targetType == typeof(uint))
            {
                return convertible.ToUInt32(provider);
            }
            if (targetType == typeof(long))
            {
                return convertible.ToInt64(provider);
            }
            if (targetType == typeof(ulong))
            {
                return convertible.ToUInt64(provider);
            }
            if (targetType == typeof(float))
            {
                return convertible.ToSingle(provider);
            }
            if (targetType == typeof(double))
            {
                return convertible.ToDouble(provider);
            }
            if (targetType == typeof(decimal))
            {
                return convertible.ToDecimal(provider);
            }
            if (targetType == typeof(DateTime))
            {
                return convertible.ToDateTime(provider);
            }
            if (targetType == typeof(string))
            {
                return convertible.ToString(provider);
            }
            throw new InvalidCastException(GetInvalidCastMessage(targetType));
        }

        private string GetInvalidCastMessage(Type targetType)
        {
            return $"A value of type '{GetType().FullName}' cannot be converted to a value of type '{targetType.FullName}'.";
        }
    }
}
