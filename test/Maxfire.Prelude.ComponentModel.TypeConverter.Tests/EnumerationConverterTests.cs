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

        [Fact]
        public void CanConvertFrom()
        {
            var sut = new EnumerationConverter<BooleanEnumeration>();
            sut.CanConvertFrom(typeof (string)).ShouldBeTrue();
        }

        [Fact]
        public void CanConvertTo()
        {
            var sut = new EnumerationConverter<BooleanEnumeration>();
            sut.CanConvertTo(typeof(string)).ShouldBeTrue();
        }

        [Fact]
        public void ConvertFrom()
        {
            var sut = new EnumerationConverter<BooleanEnumeration>();
            sut.ConvertFrom("True").ShouldBe(BooleanEnumeration.True);
        }

        [Fact]
        public void ConvertTo()
        {
            var sut = new EnumerationConverter<BooleanEnumeration>();
            sut.ConvertToString(BooleanEnumeration.True).ShouldBe("True");
        }
    }
}
