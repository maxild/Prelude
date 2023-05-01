using System;
using System.ComponentModel;
using System.Globalization;
using Shouldly;
using Xunit;

namespace Maxfire.Prelude.Tests
{
    public class DateTests
    {
        [Fact]
        public void MinValueIsDefault()
        {
            Date.MinValue.ShouldBe(default);
        }

        [Fact]
        public void HoleYearDifference()
        {
            Date.HoleYearDifference(new Date(1970, 6, 3), new Date(1970, 6, 3)).ShouldBe(0);
            Date.HoleYearDifference(new Date(1970, 6, 3), new Date(1970, 6, 4)).ShouldBe(0);
            Date.HoleYearDifference(new Date(1970, 6, 3), new Date(1971, 6, 2)).ShouldBe(0);
            Date.HoleYearDifference(new Date(1970, 6, 3), new Date(1971, 6, 3)).ShouldBe(1);
            Date.HoleYearDifference(new Date(1970, 6, 3), new Date(1971, 6, 4)).ShouldBe(1);

            Date.HoleYearDifference(new Date(2000, 2, 29), new Date(1999, 2, 28)).ShouldBe(-1);
            Date.HoleYearDifference(new Date(2000, 2, 29), new Date(1999, 3, 1)).ShouldBe(0);
            Date.HoleYearDifference(new Date(2000, 2, 29), new Date(2001, 2, 28)).ShouldBe(0);
            Date.HoleYearDifference(new Date(2000, 2, 29), new Date(2001, 3, 1)).ShouldBe(1);
        }

        [Fact]
        public void TryParse()
        {
            Date.TryParse(null).ShouldBeNull();
            Date.TryParse("bogus").ShouldBeNull();
        }

        [Fact]
        public void DateTypeConverter_CanConvertFromAndToString()
        {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(Date));
            // NOTE: Any source type that is not a string in ConvertFrom throws NotSupported
            converter.CanConvertFrom(typeof(string)).ShouldBeTrue();
            // NOTE: Any destination type that is not string throws NotSupported (also any
            // source type (value.GetType()) that is not a Date throws NotSupported
            converter.CanConvertTo(typeof(string)).ShouldBeTrue();
        }

        [Fact]
        public void DateTypeConverter_ConvertTo_WrongDestinationType_ThrowsNotSupported()
        {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(Date));
            Assert.Throws<NotSupportedException>(() =>
                    converter.ConvertTo(null, CultureInfo.InvariantCulture, value: "3/6-1970", typeof(DateTime)))
                .Message.ShouldBe("DateTypeConverter is unable to convert System.String to System.DateTime.");
        }

        [Fact]
        public void DateTypeConverter_ConvertTo_WrongSourceType_ThrowsFor2()
        {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(Date));
            Assert.Throws<NotSupportedException>(() =>
                    converter.ConvertTo(null, CultureInfo.InvariantCulture, value: 8, typeof(string)))
                .Message.ShouldBe("DateTypeConverter is unable to convert System.Int32 to System.String.");
        }

        [Fact]
        public void DateTypeConverter_ConvertToString_ThrowsIfValueIsNotDate()
        {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(Date));
            Assert.Throws<NotSupportedException>(() => converter.ConvertToString(value: ""))
                .Message.ShouldBe("DateTypeConverter is unable to convert System.String to System.String.");
        }

        [Fact]
        public void DateTypeConverter_ConvertToString_ThrowsIfValueIsNull()
        {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(Date));

            Assert.Throws<NotSupportedException>(() => converter.ConvertToString(value: null!))
                .Message.ShouldBe("DateTypeConverter is unable to convert (null) to System.String.");
        }

        [Fact]
        public void DateTypeConverter_ConvertToString()
        {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(Date));
            converter.ConvertToString(new Date(1970, 6, 3)).ShouldBe("3/6-1970");
        }

        [Fact]
        public void DateTypeConverter_ConvertFrom_ThrowsFormatExceptionIfValueIsNull()
        {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(Date));
            Assert.Throws<NotSupportedException>(() => converter.ConvertFrom(value: null!))
                .Message.ShouldBe("DateTypeConverter cannot convert from (null).");
        }

        [Fact]
        public void DateTypeConverter_ConvertFrom_ThrowsFormatExceptionIfValueIsBogus()
        {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(Date));
            Assert.Throws<FormatException>(() => converter.ConvertFrom(value: "bogus"))
                .Message.ShouldBe("DateTypeConverter cannot convert from 'bogus'.");
        }

        [Fact]
        public void DateTypeConverter_ConvertFrom()
        {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(Date));
            Assert.Throws<NotSupportedException>(() => converter.ConvertFrom(value: 8))
                .Message.ShouldBe("DateTypeConverter cannot convert from System.Int32.");
        }
    }
}
