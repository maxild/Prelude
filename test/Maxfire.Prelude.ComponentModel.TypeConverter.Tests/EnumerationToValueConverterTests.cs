using Xunit;

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
            Assert.True(sut.CanConvertFrom(typeof(string)));
        }

        [Fact]
        public void CanConvertTo()
        {
            var sut = new EnumerationToValueConverter<BooleanEnumeration>();
            Assert.True(sut.CanConvertTo(typeof(string)));
        }

        [Fact]
        public void ConvertFrom()
        {
            var sut = new EnumerationToValueConverter<BooleanEnumeration>();
            Assert.Equal(BooleanEnumeration.True, sut.ConvertFrom("1"));
        }

        [Fact]
        public void ConvertTo()
        {
            var sut = new EnumerationToValueConverter<BooleanEnumeration>();
            Assert.Equal("1", sut.ConvertToString(BooleanEnumeration.True));
        }
    }
}
