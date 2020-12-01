using System.Diagnostics.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Maxfire.Prelude.Tests
{
    public class NullableLearningTests
    {
        [Fact]
        [SuppressMessage("ReSharper", "ExpressionIsAlwaysNull")]
        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
        [SuppressMessage("ReSharper", "IsExpressionAlwaysTrue")]
        [SuppressMessage("ReSharper", "RedundantNullableTypeMark")]
        public void IsNullPattern()
        {
            int? x = null;
            x.ShouldBeNull();
            (x is null).ShouldBeTrue();
            (x is int?).ShouldBeFalse();
            (x is int).ShouldBeFalse();

            int? y = 6;
            y.ShouldNotBeNull();
            (y is null).ShouldBeFalse();
            (y is int?).ShouldBeTrue();
            (y is int).ShouldBeTrue();
        }
    }
}
