using System;
using System.Globalization;
using Xunit;
using Shouldly;

namespace Maxfire.Prelude.ComponentModel.Tests
{
    public class EnumerationConverterTests
    {
        public class BooleanEnumeration : Enumeration<BooleanEnumeration>
        {
            public static readonly BooleanEnumeration False = new BooleanEnumeration(0, "False");
            public static readonly BooleanEnumeration True = new BooleanEnumeration(1, "True");

            private BooleanEnumeration(int value, string name)
                : base(value, name)
            {
            }
        }

        public EnumerationConverterTests()
        {
            Sut = new EnumerationConverter<BooleanEnumeration>();
        }

        private EnumerationConverter<BooleanEnumeration> Sut { get; }

        [Fact]
        public void CanConvertFrom()
        {
            Sut.CanConvertFrom(typeof (string)).ShouldBeTrue();
        }

        [Fact]
        public void CanConvertTo()
        {
            Sut.CanConvertTo(typeof(string)).ShouldBeTrue();
        }

        [Fact]
        public void ConvertFrom()
        {
            Sut.ConvertFrom("True").ShouldBe(BooleanEnumeration.True);
        }

        [Fact]
        public void ConvertTo()
        {
            Sut.ConvertToString(BooleanEnumeration.True).ShouldBe("True");
        }

        [Fact]
        public void ConvertTo_NullDestinationType_ThrowsNullArgument()
        {
            Assert.Throws<ArgumentNullException>(() =>
                    Sut.ConvertTo(null, CultureInfo.InvariantCulture, BooleanEnumeration.False, destinationType: null!))
                .ParamName.ShouldBe("destinationType");
        }

        [Fact]
        public void ConvertTo_WrongDestinationType_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() =>
                    Sut.ConvertTo(null, CultureInfo.InvariantCulture, value: BooleanEnumeration.True, typeof(DateTime)))
                .Message.ShouldBe(
                    "EnumerationConverter<BooleanEnumeration> is unable to convert Maxfire.Prelude.ComponentModel.Tests.EnumerationConverterTests+BooleanEnumeration to System.DateTime.");
        }

        [Fact]
        public void ConvertToString_WrongSourceTypeOfValue_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => Sut.ConvertToString(value: 8))
                .Message.ShouldBe("EnumerationConverter<BooleanEnumeration> is unable to convert System.Int32 to System.String.");
        }

        [Fact]
        public void ConvertToString_WrongSourceTypeOfValue_ThrowsNotSupported_2()
        {
            Assert.Throws<NotSupportedException>(() => Sut.ConvertToString(value: "True"))
                .Message.ShouldBe("EnumerationConverter<BooleanEnumeration> is unable to convert System.String to System.String.");
        }

        [Fact]
        public void ConvertToString_ValueIsNull_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => Sut.ConvertToString(null!))
                .Message.ShouldBe("EnumerationConverter<BooleanEnumeration> is unable to convert (null) to System.String.");
        }

        [Fact]
        public void ConvertToString()
        {
            Sut.ConvertToString(BooleanEnumeration.False).ShouldBe("False");
            Sut.ConvertToString(BooleanEnumeration.True).ShouldBe("True");
        }

        [Fact]
        public void ConvertFromInvalidType_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => Sut.ConvertFrom(value: 8))
                .Message.ShouldBe("EnumerationConverter<BooleanEnumeration> cannot convert from System.Int32.");
        }

        [Fact]
        public void ConvertFromNull_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => Sut.ConvertFrom(value: null))
                .Message.ShouldBe("EnumerationConverter<BooleanEnumeration> cannot convert from (null).");
        }

        [Fact]
        public void ConvertFromEmptyString_ThrowsFormat()
        {
            // This exception is thrown by Parse
            Assert.Throws<FormatException>(() => Sut.ConvertFrom(value: ""))
                .Message.ShouldBe("EnumerationConverter<BooleanEnumeration> cannot convert from String.Empty.");
        }

        [Fact]
        public void ConvertFromBogusString_ThrowsFormat()
        {
            // This exception is thrown by Parse
            Assert.Throws<FormatException>(() => Sut.ConvertFrom(value: "bogus"))
                .Message.ShouldBe("EnumerationConverter<BooleanEnumeration> cannot convert from 'bogus'.");
        }
    }
}
