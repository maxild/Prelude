using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Maxfire.Prelude.ComponentModel
{
    public class EnumerationConverter<TEnumeration> : AbstractTypeConverter<TEnumeration>
        where TEnumeration : Enumeration<TEnumeration>
    {
        private readonly Func<TEnumeration, string>? _convertToString;

        public EnumerationConverter()
        {
        }

        protected EnumerationConverter(Func<TEnumeration, string> converter)
        {
            _convertToString = converter;
        }

        protected override TEnumeration Parse(string s, CultureInfo culture)
        {
            TEnumeration result = int.TryParse(s, out var val) ?
                Enumeration.FromValue<TEnumeration>(val) :
                Enumeration.FromName<TEnumeration>(s);
            return result;
        }

        protected override string Stringify([NotNull] TEnumeration value, CultureInfo culture)
        {
            return _convertToString is null ? value.Name : _convertToString(value);
        }
    }
}
