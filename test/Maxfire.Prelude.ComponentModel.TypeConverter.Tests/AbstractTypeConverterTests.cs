using System;
using System.Globalization;
using Shouldly;
using Xunit;

namespace Maxfire.Prelude.ComponentModel.Tests
{
    public class AbstractTypeConverterTests
    {
        /// <summary>
        /// This is a skeleton for DateTypeConverter in Brf.Lofus.Common
        /// </summary>
        class DateTimeTypeConverter : AbstractTypeConverter<DateTime>
        {
            public DateTimeTypeConverter(bool supportNullToEmptyString) : base(supportNullToEmptyString)
            {
            }

            protected override DateTime Parse(string s, CultureInfo? culture)
            {
                return !DateTime.TryParse(s, out DateTime result)
                    ? throw new FormatException(GetParseErrorMessage(s))
                    : result;
            }

            protected override string Stringify(DateTime value, CultureInfo? culture)
            {
                return value.ToString("yyyy-MM-dd");
            }
        }

        public AbstractTypeConverterTests()
        {
            Sut = new DateTimeTypeConverter(supportNullToEmptyString: false);
        }

        private DateTimeTypeConverter Sut { get; }

        [Fact]
        public void CanConvertFromAndToString()
        {
            Sut.CanConvertFrom(typeof(string)).ShouldBeTrue();
            Sut.CanConvertTo(typeof(string)).ShouldBeTrue();
        }

        [Fact]
        public void ConvertToNullDestinationType_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                Sut.ConvertTo(null, CultureInfo.InvariantCulture, new DateTime(1970, 6, 3), null!))
                    .ParamName.ShouldBe("destinationType");
        }

        [Fact]
        public void ConvertTo_WrongDestinationType_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() =>
                    Sut.ConvertTo(null, CultureInfo.InvariantCulture, value: new DateTime(1970, 6, 3), typeof(DateTimeOffset)))
                .Message.ShouldBe("DateTimeTypeConverter is unable to convert System.DateTime to System.DateTimeOffset."); // TODO: add wrong destination type
        }

        [Fact]
        public void ConvertToString_WrongSourceTypeOfValue_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => Sut.ConvertToString(value: 8))
                .Message.ShouldBe("DateTimeTypeConverter is unable to convert System.Int32 to System.String."); // TODO: add wrong source type of value
        }

        [Fact]
        public void ConvertToString_WrongSourceTypeOfValue_ThrowsNotSupported_2()
        {
            Assert.Throws<NotSupportedException>(() => Sut.ConvertToString(value: "1970-06-03"))
                .Message.ShouldBe("DateTimeTypeConverter is unable to convert System.String to System.String.");
        }

        [Fact]
        public void ConvertToString_ValueIsNull_ReturnsEmptyString()
        {
            new DateTimeTypeConverter(supportNullToEmptyString: true).ConvertToString(value: null!)
                .ShouldBe(string.Empty);
        }

        [Fact]
        public void ConvertToString_ValueIsNull_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => Sut.ConvertToString(null!))
                .Message.ShouldBe("DateTimeTypeConverter is unable to convert (null) to System.String.");
        }

        [Fact]
        public void ConvertToString()
        {
            Sut.ConvertToString(new DateTime(1970, 6, 3)).ShouldBe("1970-06-03");
        }

        [Fact]
        public void ConvertFromInvalidType_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => Sut.ConvertFrom(value: 8))
                .Message.ShouldBe("DateTimeTypeConverter cannot convert from System.Int32.");
        }

        [Fact]
        public void ConvertFromNull_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => Sut.ConvertFrom(value: null))
                .Message.ShouldBe("DateTimeTypeConverter cannot convert from (null).");
        }

        [Fact]
        public void ConvertFromEmptyString_ThrowsFormat()
        {
            // This exception is thrown by Parse
            Assert.Throws<FormatException>(() => Sut.ConvertFrom(value: ""))
                .Message.ShouldBe("DateTimeTypeConverter cannot convert from String.Empty.");
        }

        [Fact]
        public void ConvertFromBogusString_ThrowsFormat()
        {
            // This exception is thrown by Parse
            Assert.Throws<FormatException>(() => Sut.ConvertFrom(value: "bogus"))
                .Message.ShouldBe("DateTimeTypeConverter cannot convert from 'bogus'.");
        }
    }
}
