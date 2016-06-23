﻿using System;
using System.Globalization;
using System.ComponentModel;

namespace Maxfire.Prelude.ComponentModel
{
    /// <summary>
    /// Conversion to and from string.
    /// </summary>
    public abstract class AbstractTypeConverter<T> : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return (sourceType == typeof(string) || base.CanConvertFrom(context, sourceType));
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return (destinationType == typeof(string) || base.CanConvertTo(context, destinationType));
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var s = value as string;
            if (s != null)
            {
                return Parse(s, culture);
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            if (destinationType == typeof(string))
            {
                if (!(value is T))
                {
                    throw new ArgumentException($"The value to convert to a string is not of type '{typeof(T).Name}'.", nameof(value));
                }
                return Stringify((T)value, culture);
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        protected abstract T Parse(string s, CultureInfo culture);

        protected abstract string Stringify(T value, CultureInfo culture);
    }
}