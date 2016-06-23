using Xunit;
using Shouldly;

namespace Maxfire.Prelude.ComponentModel.Tests
{
    public class EnumerationToValueConverterTests
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
            var sut = new EnumerationToValueConverter<BooleanEnumeration>();
            sut.CanConvertFrom(typeof(string)).ShouldBeTrue();
        }

        [Fact]
        public void CanConvertTo()
        {
            var sut = new EnumerationToValueConverter<BooleanEnumeration>();
            sut.CanConvertTo(typeof(string)).ShouldBeTrue();
        }

        [Fact]
        public void ConvertFrom()
        {
            var sut = new EnumerationToValueConverter<BooleanEnumeration>();
            sut.ConvertFrom("1").ShouldBe(BooleanEnumeration.True);
        }

        [Fact]
        public void ConvertTo()
        {
            var sut = new EnumerationToValueConverter<BooleanEnumeration>();
            sut.ConvertToString(BooleanEnumeration.True).ShouldBe("1");
        }
    }
}
