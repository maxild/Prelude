using System;
using System.Globalization;

namespace Maxfire.Prelude.ComponentModel
{
    public class EnumerationTypeConverter<TEnumeration> : AbstractTypeConverter<TEnumeration>
        where TEnumeration : Enumeration<TEnumeration>
    {
        private readonly Func<TEnumeration, string> _convertToString;

        public EnumerationTypeConverter()
        {
        }

        protected EnumerationTypeConverter(Func<TEnumeration, string> converter)
        {
            _convertToString = converter;
        }

        protected override TEnumeration Parse(string s, CultureInfo culture)
        {
            int val;
            TEnumeration result = int.TryParse(s, out val) ?
                Enumeration.FromValue<TEnumeration>(val) :
                Enumeration.FromName<TEnumeration>(s);
            return result;
        }

        protected override string Stringify(TEnumeration value, CultureInfo culture)
        {
            return _convertToString != null ? _convertToString(value) : value.Name;
        }
    }
}
