using System;
using Xunit;
using Shouldly;

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
            Enumeration.GetAll<FooBarEnumeration>().ShouldBe(new [] { FooBarEnumeration.Foo, FooBarEnumeration.Bar });
            Enumeration.GetAll(typeof(FooBarEnumeration)).ShouldBe(new[] { FooBarEnumeration.Foo, FooBarEnumeration.Bar });
        }

        [Fact]
        public void FromValue()
        {
            Enumeration.FromValue<FooBarEnumeration>(1).ShouldBe(FooBarEnumeration.Foo);
            Enumeration.FromValue(typeof(FooBarEnumeration), 2).ShouldBe(FooBarEnumeration.Bar);
        }

        [Fact]
        public void FromValueOrDefault()
        {
            Enumeration.FromValueOrDefault<FooBarEnumeration>(1).ShouldBe(FooBarEnumeration.Foo);
            Enumeration.FromValueOrDefault(typeof(FooBarEnumeration), 2).ShouldBe(FooBarEnumeration.Bar);
        }

        [Fact]
        public void FromValue_ThrowsIfNotFound()
        {
            Assert.Throws<ArgumentException>(() => Enumeration.FromValue<FooBarEnumeration>(0))
                .Message.ShouldBe("'0' is not a valid value for 'Maxfire.Prelude.Tests.EnumerationTests+FooBarEnumeration'.");
            Assert.Throws<ArgumentException>(() => Enumeration.FromValue(typeof(FooBarEnumeration), 0))
                .Message.ShouldBe("'0' is not a valid value for 'Maxfire.Prelude.Tests.EnumerationTests+FooBarEnumeration'.");
        }

        [Fact]
        public void FromValueOrDefault_ReturnsNullIfNotFound()
        {
            Enumeration.FromValueOrDefault<FooBarEnumeration>(0).ShouldBeNull();
            Enumeration.FromValueOrDefault(typeof(FooBarEnumeration), 0).ShouldBeNull();
        }

        [Fact]
        public void FromName()
        {
            Enumeration.FromName<FooBarEnumeration>("Foo").ShouldBe(FooBarEnumeration.Foo);
            Enumeration.FromName(typeof(FooBarEnumeration), "Bar").ShouldBe(FooBarEnumeration.Bar);
        }

        [Fact]
        public void FromNameOrDefault()
        {
            Enumeration.FromNameOrDefault<FooBarEnumeration>("foo").ShouldBe(FooBarEnumeration.Foo);
            Enumeration.FromNameOrDefault(typeof(FooBarEnumeration), "bar").ShouldBe(FooBarEnumeration.Bar);
        }

        [Fact]
        public void FromNameThrowsIfNotFound()
        {
            Assert.Throws<FormatException>(() => Enumeration.FromName<FooBarEnumeration>("Rubbish"))
                .Message.ShouldBe("'Rubbish' is not a valid name for 'Maxfire.Prelude.Tests.EnumerationTests+FooBarEnumeration'.");
            Assert.Throws<FormatException>(() => Enumeration.FromName(typeof(FooBarEnumeration), "Rubbish"))
                .Message.ShouldBe("'Rubbish' is not a valid name for 'Maxfire.Prelude.Tests.EnumerationTests+FooBarEnumeration'.");
        }

        [Fact]
        public void FromNameOrDefault_ReturnsNullIfNotFound()
        {
            Enumeration.FromNameOrDefault<FooBarEnumeration>("Rubbish").ShouldBeNull();
            Enumeration.FromNameOrDefault(typeof(FooBarEnumeration), "Rubbish").ShouldBeNull();
        }

        [Fact]
        public void Equality()
        {
            FooBarEnumeration.Foo.ShouldBe(FooBarEnumeration.Foo);
            FooBarEnumeration.Foo.ShouldNotBe(FooBarEnumeration.Bar);

            // equality by type
            // ReSharper disable once SuspiciousTypeConversion.Global
            FooBarEnumeration.Foo.Equals(AnotherFooBarEnumeration.Foo).ShouldBeFalse();
            FooBarEnumeration.Foo.Equals(new object()).ShouldBeFalse();

            // equality by value
            AnotherFooBarEnumeration.Foo.ShouldBe(new AnotherFooBarEnumeration(1, "Rubbish"));
            AnotherFooBarEnumeration.Foo.ShouldNotBe(new AnotherFooBarEnumeration(10, "Foo"));
        }

        [Fact]
        public void EqualToNullIsFalse()
        {
            FooBarEnumeration.Foo.Equals(null).ShouldBeFalse();
        }

        [Fact]
        public void EqualToNullIsFalse2()
        {
            FooBarEnumeration.Foo.Equals((object?) null).ShouldBeFalse();
        }

        [Fact]
        public void Comparison()
        {
            FooBarEnumeration.Foo.CompareTo(null).ShouldBeGreaterThan(0); // null compares less anything (by convention)
            FooBarEnumeration.Foo.ShouldBeLessThan(FooBarEnumeration.Bar);
            FooBarEnumeration.Foo.ShouldBeLessThanOrEqualTo(FooBarEnumeration.Bar);
            FooBarEnumeration.Foo.ShouldBeLessThanOrEqualTo(FooBarEnumeration.Foo);
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
