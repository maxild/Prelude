using System.Globalization;

namespace Maxfire.Prelude.ComponentModel
{
    public class EnumerationToValueTypeConverter<TEnumeration> : EnumerationTypeConverter<TEnumeration>
        where TEnumeration : Enumeration<TEnumeration>
    {
        public EnumerationToValueTypeConverter()
            : base(e => e.Value.ToString(CultureInfo.InvariantCulture))
        {
        }
    }
}
