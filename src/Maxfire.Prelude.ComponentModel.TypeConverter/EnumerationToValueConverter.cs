using System.Globalization;

namespace Maxfire.Prelude.ComponentModel
{
    public class EnumerationToValueConverter<TEnumeration> : EnumerationConverter<TEnumeration>
        where TEnumeration : Enumeration<TEnumeration>
    {
        public EnumerationToValueConverter()
            : base(e => e.Value.ToString(CultureInfo.InvariantCulture))
        {
        }
    }
}
