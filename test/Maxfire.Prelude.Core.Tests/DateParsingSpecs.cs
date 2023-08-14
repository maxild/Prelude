using System;
using System.Globalization;
using Shouldly;
using Xunit;

namespace Maxfire.Prelude.Tests
{
    public class DateParsingSpecs
    {
        [Fact]
        public void TroubleMakers_ThisIsWhyDateDoesNotHaveTimeZoneIndicatorInAnyValidFormat()
        {
            // Transformed local date-time is created, because of RoundtripKind...BAD!!!
            var dateTime = DateTime.ParseExact("1970-12-24+02:00", @"yyyy'-'MM'-'ddK",
                DateFormat.Iso.Culture, DateTimeStyles.RoundtripKind);

            // BAD
            dateTime.Kind.ShouldBe(DateTimeKind.Local);
            dateTime.Day.ShouldBe(23);
            dateTime.Hour.ShouldBe(22 + TimeZoneInfo.Local.GetUtcOffset(dateTime).Hours);

            // This is "1970-12-24+02:00" as a DateTimeOffset value
            var utc = new DateTimeOffset(1970, 12, 24, 0, 0, 0, TimeSpan.FromHours(2)).UtcDateTime;
            utc.Day.ShouldBe(23);
            utc.Hour.ShouldBe(22);
            var local = utc.ToLocalTime(); // BAD
            local.Day.ShouldBe(23);
            local.Hour.ShouldBe(22 + TimeZoneInfo.Local.GetUtcOffset(dateTime).Hours);

            Assert.Equal(dateTime, local);
        }

        [Fact]
        public void ParseDefaultDateString()
        {
            Date.Parse("24/12-2015").ShouldBe(new Date(2015, 12, 24));
            Date.Parse("24/12-2015", DateFormat.Default).ShouldBe(new Date(2015, 12, 24));

            Date.Parse("3/6-1970").ShouldBe(new Date(1970, 6, 3));
            Date.Parse("3/6-1970", DateFormat.Default).ShouldBe(new Date(1970, 6, 3));

            Date.Parse("03/6-1970").ShouldBe(new Date(1970, 6, 3));
            Date.Parse("03/6-1970", DateFormat.Default).ShouldBe(new Date(1970, 6, 3));

            Date.Parse("3/06-1970").ShouldBe(new Date(1970, 6, 3));
            Date.Parse("3/06-1970", DateFormat.Default).ShouldBe(new Date(1970, 6, 3));

            Date.Parse("03/06-1970").ShouldBe(new Date(1970, 6, 3));
            Date.Parse("03/06-1970", DateFormat.Default).ShouldBe(new Date(1970, 6, 3));

            // too many digits
            Assert.Throws<FormatException>(() => Date.Parse("003/6-1970"));
            Assert.Throws<FormatException>(() => Date.Parse("003/6-1970", DateFormat.Default));

            // leading white space
            Assert.Throws<FormatException>(() => Date.Parse(" 3/6-1970"));
            Assert.Throws<FormatException>(() => Date.Parse(" 3/6-1970", DateFormat.Default));

            // trailing white space
            Assert.Throws<FormatException>(() => Date.Parse("3/6-1970 "));
            Assert.Throws<FormatException>(() => Date.Parse("3/6-1970 ", DateFormat.Default));

            // inner white space
            Assert.Throws<FormatException>(() => Date.Parse("3 / 6-1970 "));
            Assert.Throws<FormatException>(() => Date.Parse("3 / 6-1970 ", DateFormat.Default));
        }

        [Fact]
        public void ParseShortDateStringThrows()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Date.Parse("", DateFormat.Short));
            ex.Message.ShouldBe("DateFormat.Short is not a parseable format.");
        }

        [Fact]
        public void ParseLongDateStringThrows()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Date.Parse("", DateFormat.Long));
            ex.Message.ShouldBe("DateFormat.Long is not a parseable format.");
        }

        [Fact]
        public void ParseDayMonthYearDateString()
        {
            Date.Parse("24122015", DateFormat.DayMonthYear).ShouldBe(new Date(2015, 12, 24));
            Date.Parse("03061970", DateFormat.DayMonthYear).ShouldBe(new Date(1970, 6, 3));

            // wrong length
            Assert.Throws<FormatException>(() => Date.Parse("3061970", DateFormat.DayMonthYear));

            // leading white space
            Assert.Throws<FormatException>(() => Date.Parse(" 03061970", DateFormat.DayMonthYear));

            // trailing white space
            Assert.Throws<FormatException>(() => Date.Parse("03061970 ", DateFormat.DayMonthYear));

            // inner white space
            Assert.Throws<FormatException>(() => Date.Parse("03 061970", DateFormat.DayMonthYear));
        }

        [Fact]
        public void ParseYearMonthDayDateString()
        {
            Date.Parse("20151224", DateFormat.YearMonthDay).ShouldBe(new Date(2015, 12, 24));
            Date.Parse("19700603", DateFormat.YearMonthDay).ShouldBe(new Date(1970, 6, 3));

            // wrong length
            Assert.Throws<FormatException>(() => Date.Parse("1970063", DateFormat.YearMonthDay));

            // leading white space
            Assert.Throws<FormatException>(() => Date.Parse(" 19700603", DateFormat.YearMonthDay));

            // trailing white space
            Assert.Throws<FormatException>(() => Date.Parse("19700603 ", DateFormat.YearMonthDay));
        }

        [Fact]
        public void ParseIsoDateString()
        {
            Date.Parse("2015-12-24", DateFormat.Iso).ShouldBe(new Date(2015, 12, 24));
            Date.Parse("1970-06-03", DateFormat.Iso).ShouldBe(new Date(1970, 6, 3));

            // wrong length
            Assert.Throws<FormatException>(() => Date.Parse("1970-06-3", DateFormat.Iso));

            // leading white space
            Assert.Throws<FormatException>(() => Date.Parse(" 1970-06-03", DateFormat.Iso));

            // trailing white space
            Assert.Throws<FormatException>(() => Date.Parse("1970-06-03 ", DateFormat.Iso));
        }
    }
}
