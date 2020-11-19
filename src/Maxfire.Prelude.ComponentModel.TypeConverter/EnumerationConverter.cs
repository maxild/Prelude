using System;
using System.Globalization;

namespace Maxfire.Prelude.ComponentModel
{
    public class EnumerationConverter<TEnumeration> : AbstractTypeConverter<TEnumeration>
        where TEnumeration : Enumeration<TEnumeration>
    {
        private readonly Func<TEnumeration, string>? _convertToString;

        public EnumerationConverter() : base(supportNullToEmptyString: false)
        {
        }

        protected EnumerationConverter(Func<TEnumeration, string> converter) : base(supportNullToEmptyString: false)
        {
            _convertToString = converter;
        }

        protected override TEnumeration Parse(string s, CultureInfo culture)
        {
            TEnumeration? result = int.TryParse(s, out var val) ?
                Enumeration.FromValueOrDefault<TEnumeration>(val) :
                Enumeration.FromNameOrDefault<TEnumeration>(s);

            return result ?? throw new FormatException(GetParseErrorMessage(s));
        }

        protected override string Stringify(TEnumeration value, CultureInfo culture)
        {
            return _convertToString is null ? value.Name : _convertToString(value);
        }
    }
}
