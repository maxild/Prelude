using System;
using System.Globalization;
using System.Threading;
using Shouldly;
using Xunit;

namespace Maxfire.Prelude.Tests
{
    public class DateFormattingSpecs
    {
        class CurrentCultureScope : IDisposable
        {
            private readonly CultureInfo _culture;
            private readonly CultureInfo _uiCulture;

            public CurrentCultureScope(string name)
            {
                _culture = Thread.CurrentThread.CurrentCulture;
                _uiCulture = Thread.CurrentThread.CurrentUICulture;
                Thread.CurrentThread.CurrentCulture = new CultureInfo(name);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(name);
            }

            public void Dispose()
            {
                Thread.CurrentThread.CurrentCulture = _culture;
                Thread.CurrentThread.CurrentUICulture = _uiCulture;
            }
        }

        [Fact]
        public void ToDefaultString()
        {
            using (new CurrentCultureScope(""))
            {
                new Date(2015, 12, 24).ToString().ShouldBe("24/12-2015");
                new Date(2015, 12, 24).ToDefaultString().ShouldBe("24/12-2015");
            }

            using (new CurrentCultureScope("da-DK"))
            {
                new Date(2015, 12, 24).ToString().ShouldBe("24/12-2015");
                new Date(2015, 12, 24).ToDefaultString().ShouldBe("24/12-2015");
            }

            using (new CurrentCultureScope("en-US"))
            {
                new Date(2015, 12, 24).ToString().ShouldBe("24/12-2015");
                new Date(2015, 12, 24).ToDefaultString().ShouldBe("24/12-2015");
            }
        }

        [Fact]
        public void ToIsoDateString()
        {
            using (new CurrentCultureScope(""))
            {
                new Date(2015, 12, 24).ToIsoDateString().ShouldBe("2015-12-24");
                new Date(2015, 12, 24).ToString(DateFormat.Iso).ShouldBe("2015-12-24");
            }

            using (new CurrentCultureScope("da-DK"))
            {
                new Date(2015, 12, 24).ToIsoDateString().ShouldBe("2015-12-24");
                new Date(2015, 12, 24).ToString(DateFormat.Iso).ShouldBe("2015-12-24");
            }

            using (new CurrentCultureScope("en-US"))
            {
                new Date(2015, 12, 24).ToIsoDateString().ShouldBe("2015-12-24");
                new Date(2015, 12, 24).ToString(DateFormat.Iso).ShouldBe("2015-12-24");
            }
        }

        [Fact]
        public void ToReverseIsoDateString()
        {
            using (new CurrentCultureScope(""))
            {
                new Date(2015, 12, 24).ToString(DateFormat.ReverseIso).ShouldBe("24-12-2015");
            }

            using (new CurrentCultureScope("da-DK"))
            {
                new Date(2015, 12, 24).ToString(DateFormat.ReverseIso).ShouldBe("24-12-2015");
            }

            using (new CurrentCultureScope("en-US"))
            {
                new Date(2015, 12, 24).ToString(DateFormat.ReverseIso).ShouldBe("24-12-2015");
            }
        }

        [Fact]
        public void ToYearMonthDayDateString()
        {
            using (new CurrentCultureScope(""))
            {
                new Date(2015, 12, 24).ToYearMonthDayDateString().ShouldBe("20151224");
                new Date(2015, 12, 24).ToString(DateFormat.YearMonthDay).ShouldBe("20151224");
            }

            using (new CurrentCultureScope("da-DK"))
            {
                new Date(2015, 12, 24).ToYearMonthDayDateString().ShouldBe("20151224");
                new Date(2015, 12, 24).ToString(DateFormat.YearMonthDay).ShouldBe("20151224");
            }

            using (new CurrentCultureScope("en-US"))
            {
                new Date(2015, 12, 24).ToYearMonthDayDateString().ShouldBe("20151224");
                new Date(2015, 12, 24).ToString(DateFormat.YearMonthDay).ShouldBe("20151224");
            }
        }

        [Fact]
        public void ToDayMonthYearDateString()
        {
            using (new CurrentCultureScope(""))
            {
                new Date(2015, 12, 24).ToString(DateFormat.DayMonthYear).ShouldBe("24122015");
            }

            using (new CurrentCultureScope("da-DK"))
            {
                new Date(2015, 12, 24).ToString(DateFormat.DayMonthYear).ShouldBe("24122015");
            }

            using (new CurrentCultureScope("en-US"))
            {
                new Date(2015, 12, 24).ToString(DateFormat.DayMonthYear).ShouldBe("24122015");
            }
        }
    }
}
