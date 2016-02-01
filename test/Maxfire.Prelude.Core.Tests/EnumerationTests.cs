using System;
using Xunit;
using FluentAssertions;

namespace Maxfire.Prelude.Tests
{
	public class EnumerationTests
	{
        public class FooBarEnumeration : Enumeration<FooBarEnumeration>
        {
            public static readonly FooBarEnumeration Foo = new FooBarEnumeration(1, "Foo", "Foo");
            public static readonly FooBarEnumeration Bar = new FooBarEnumeration(2, "Bar", "Bar");

            private FooBarEnumeration(int value, string name, string text)
                : base(value, name, text)
            {
            }
        }

        [Fact]
		public void GetAll()
		{
            Enumeration.GetAll<FooBarEnumeration>().Should().Equal(FooBarEnumeration.Foo, FooBarEnumeration.Bar);
            Enumeration.GetAll(typeof(FooBarEnumeration)).Should().Equal(FooBarEnumeration.Foo, FooBarEnumeration.Bar);
        }

        [Fact]
        public void FromValue()
        {
            Enumeration.FromValue<FooBarEnumeration>(1).Should().Be(FooBarEnumeration.Foo);
            Enumeration.FromValue(typeof(FooBarEnumeration), 2).Should().Be(FooBarEnumeration.Bar);
        }

        [Fact]
        public void FromValueOrDefault()
        {
            Enumeration.FromValueOrDefault<FooBarEnumeration>(1).Should().Be(FooBarEnumeration.Foo);
            Enumeration.FromValueOrDefault(typeof(FooBarEnumeration), 2).Should().Be(FooBarEnumeration.Bar);
        }

        [Fact]
        public void FromValue_ThrowsIfNotFound()
        {
            Assert.Throws<ArgumentException>(() => Enumeration.FromValue<FooBarEnumeration>(0))
                .Message.Should().Be("'0' is not a valid value for 'Maxfire.Prelude.Tests.EnumerationTests+FooBarEnumeration'.");
            Assert.Throws<ArgumentException>(() => Enumeration.FromValue(typeof(FooBarEnumeration), 0))
                .Message.Should().Be("'0' is not a valid value for 'Maxfire.Prelude.Tests.EnumerationTests+FooBarEnumeration'.");
        }

        [Fact]
        public void FromValueOrDefault_ReturnsNullIfNotFound()
        {
            Enumeration.FromValueOrDefault<FooBarEnumeration>(0).Should().BeNull();
            Enumeration.FromValueOrDefault(typeof(FooBarEnumeration), 0).Should().BeNull();
        }

        [Fact]
        public void FromName()
        {
            Enumeration.FromName<FooBarEnumeration>("Foo").Should().Be(FooBarEnumeration.Foo);
            Enumeration.FromName(typeof(FooBarEnumeration), "Bar").Should().Be(FooBarEnumeration.Bar);
        }

        [Fact]
        public void FromNameOrDefault()
        {
            Enumeration.FromNameOrDefault<FooBarEnumeration>("foo").Should().Be(FooBarEnumeration.Foo);
            Enumeration.FromNameOrDefault(typeof(FooBarEnumeration), "bar").Should().Be(FooBarEnumeration.Bar);
        }

        [Fact]
        public void FromNameThrowsIfNotFound()
        {
            Assert.Throws<ArgumentException>(() => Enumeration.FromName<FooBarEnumeration>("Rubbish"))
                .Message.Should().Be("'Rubbish' is not a valid name for 'Maxfire.Prelude.Tests.EnumerationTests+FooBarEnumeration'.");
            Assert.Throws<ArgumentException>(() => Enumeration.FromName(typeof(FooBarEnumeration), "Rubbish"))
                .Message.Should().Be("'Rubbish' is not a valid name for 'Maxfire.Prelude.Tests.EnumerationTests+FooBarEnumeration'.");
        }

        [Fact]
        public void FromNameOrDefault_ReturnsNullIfNotFound()
        {
            Enumeration.FromNameOrDefault<FooBarEnumeration>("Rubbish").Should().BeNull();
            Enumeration.FromNameOrDefault(typeof(FooBarEnumeration), "Rubbish").Should().BeNull();
        }

        [Fact]
        public void Equality()
        {
            FooBarEnumeration.Foo.Should().Be(FooBarEnumeration.Foo);
            FooBarEnumeration.Foo.Should().NotBe(FooBarEnumeration.Bar);
            // equality by null
            FooBarEnumeration.Foo.Equals(null).Should().BeFalse();
            FooBarEnumeration.Foo.Equals((object)null).Should().BeFalse();
            // equality by type
// ReSharper disable once SuspiciousTypeConversion.Global
            FooBarEnumeration.Foo.Equals(AnotherFooBarEnumeration.Foo).Should().BeFalse();
            FooBarEnumeration.Foo.Equals(new object()).Should().BeFalse();
            // equality by value
            AnotherFooBarEnumeration.Foo.Should().Be(new AnotherFooBarEnumeration(1, "Rubbish"));
            AnotherFooBarEnumeration.Foo.Should().NotBe(new AnotherFooBarEnumeration(10, "Foo"));
        }

        [Fact]
        public void Comparison()
        {
            FooBarEnumeration.Foo.Should().BeGreaterThan(null);
            FooBarEnumeration.Foo.Should().BeLessThan(FooBarEnumeration.Bar);
            FooBarEnumeration.Foo.Should().BeLessOrEqualTo(FooBarEnumeration.Bar);
            FooBarEnumeration.Foo.Should().BeLessOrEqualTo(FooBarEnumeration.Foo);
        }

        public class AnotherFooBarEnumeration : Enumeration<AnotherFooBarEnumeration>
        {
            public static readonly AnotherFooBarEnumeration Foo = new AnotherFooBarEnumeration(1, "Foo");
            public static readonly AnotherFooBarEnumeration Bar = new AnotherFooBarEnumeration(2, "Bar");

            // Normally ctor would be private in production code
            public AnotherFooBarEnumeration(int value, string name)
                : base(value, name)
            {
            }
        }
    }
}
