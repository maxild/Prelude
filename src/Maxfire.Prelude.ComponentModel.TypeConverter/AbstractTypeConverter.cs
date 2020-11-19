using System;
using System.Globalization;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Maxfire.Prelude.ComponentModel
{
    /// <summary>
    /// Conversion to and from string.
    /// </summary>
    public abstract class AbstractTypeConverter<T> : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext? context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture,
            object? value)
        {
            if (value is string s)
            {
                return Parse(s, culture);
            }

            return base.ConvertFrom(context!, culture!, value);
        }

        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value,
            Type destinationType)
        {
            if (destinationType == null)
            {
                throw new ArgumentNullException(nameof(destinationType));
            }

            if (destinationType == typeof(string))
            {
                if (value is null)
                {
                    // nullable value type or (nullable) reference type are converted to empty string
                    return string.Empty;
                }

                if (value is T typedValue)
                {
                    // value cannot be null here, and therefore derived
                    // implementations of Stringify should not handle null
                    return Stringify(typedValue, culture);
                }

                throw new ArgumentException($"The value to convert to a string is not of type '{typeof(T).Name}'.",
                    nameof(value));
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        [return: NotNull]
        protected abstract T Parse(string s, CultureInfo? culture);

        protected abstract string Stringify([DisallowNull] T value, CultureInfo? culture);
    }
}
