using System;
using System.Globalization;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Maxfire.Prelude.ComponentModel
{
    /// <summary>
    /// Conversion to and from string.
    /// </summary>
    public abstract class AbstractTypeConverter<T> : TypeConverter
    {
        private readonly bool _supportNullToEmptyString; // allow null value in ConvertTo

        protected AbstractTypeConverter(bool supportNullToEmptyString)
        {
            _supportNullToEmptyString = supportNullToEmptyString;
        }

        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext? context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo culture,
            object? value)
        {
            return value switch
            {
                string s => Parse(s, culture),
                // This discard arm will handle both 'null' and 'invalid type' (both result in NotSupported)
                _ => ConvertFromException(value)
            };
        }

        [DoesNotReturn]
        private object ConvertFromException(object? value) =>
            throw new NotSupportedException(GetConvertFromErrorMessage(value?.GetType().FullName));

        public override object ConvertTo(ITypeDescriptorContext? context, CultureInfo culture, object? value,
            Type destinationType)
        {
            // We keep this check, because then we do not depend
            // on ConvertToException to always delegate to base.ConvertTo
            if (destinationType == null)
            {
                throw new ArgumentNullException(nameof(destinationType));
            }

            if (destinationType == typeof(string))
            {
                return value switch
                {
                    null => ConvertNullToException(destinationType), // invalid (null) value
                    T typedValue => Stringify(typedValue, culture),
                    // We could delegate to base.ConvertTo, BUT...
                    // TypeConverter will always succeed converting to a string (it uses
                    // IFormattable.ToString or Object.ToString). This way DateTimeTypeConverter
                    // will be able to convert System.Int32 to a System.String.
                    // We don't want that!
                    _ => ConvertToException(value, destinationType) // invalid (non-null) value
                };
            }

            return ConvertToException(value, destinationType); // invalid destination type
        }

        protected string GetParseErrorMessage(string? value)
        {
            string valueAsString = value switch
            {
                null => "(null)",
                {Length : 0} => "String.Empty",
                _ => $"'{value}'"
            };
            return $"{GetFullName(GetType())} cannot convert from {valueAsString}.";
        }

        private string GetConvertFromErrorMessage(string? typeName) =>
            $"{GetFullName(GetType())} cannot convert from {typeName ?? "(null)"}.";

        private object ConvertNullToException(Type destinationType) =>
            _supportNullToEmptyString
                ? string.Empty
                : ConvertToException(null, destinationType);

        // Helper that encapsulate throw exception
        private object ConvertToException(object? value, Type destinationType) =>
            throw new NotSupportedException(
                $"{GetFullName(GetType())} is unable to convert {value?.GetType().FullName ?? "(null)"} to {destinationType.FullName}.");

        // TODO: Make ReflectionUtils with this recursive function
        static string GetFullName(Type t)
        {
            if (!t.IsGenericType)
                return t.Name;

            var sb = new StringBuilder();

            sb.Append(t.Name.Substring(0, t.Name.LastIndexOf("`", StringComparison.Ordinal)));
            sb.Append(t.GetGenericArguments().Aggregate("<",
                (aggregate, type) => aggregate + (aggregate == "<" ? "" : ",") + GetFullName(type)
            ));
            sb.Append(">");

            return sb.ToString();
        }

        /// <summary>
        /// Convert a string to a <typeparamref name="T"/> value.
        /// NOTE: The derived implementation of this method should only ever throw <see cref="FormatException"/>.
        /// </summary>
        /// <param name="s">The string to parse and convert to a <typeparamref name="T"/> value.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>A <typeparamref name="T"/> value.</returns>
        /// <exception cref="FormatException">In case of any parse error(s).</exception>
        [return: NotNull]
        protected abstract T Parse(string s, CultureInfo culture);

        protected abstract string Stringify([DisallowNull] T value, CultureInfo culture);
    }
}
