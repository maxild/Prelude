using System;
using System.Globalization;
using Shouldly;
using Xunit;

namespace Maxfire.Prelude.Tests
{
    // The advantages of using the UTC perspective (kind) are
    //   - dates are always unambigious (no ambigious/invalid dates)
    //   - math with dates just work (no DST problem, or problems with different time zones)
    // The disadvantage is that end users (humans) don't understand UTC. Therefore the "store
    // as UTC, display as local time" rule. However Lofus does not need a ZonedDate as found
    // in Nodatime. Users are suspected to understand and live by local times defined by the
    // "Romance Standard Time" time zone.
    // We could off course use DateTimeOffset internally. This would have the advantage of
    // preserving the context at that precise point (date) in time. This way the date would
    // also be local and could be presented to the user as is. But still we need math to work,
    // and sizeof(DateTimeOffset) - sizeof(Date) == sizeof(Timespan) means dates would grow in
    // (byte-) size.
    //
    // 1) Midnight doesn't always exist (Brazil), but in Denmark midnght does always exist. Therefore
    //    it is safe to use the Date portion of an internal DateTime.
    // 2) Once you ignore the TimeOfDay portion of a DateTime, you should always ignore the time.
    //    That is once you have a date, do not think of it as a DateTime at midnight (or without the time portion)


    public class DateTimeLearningTests
    {
        public const string IsoShortFormat = @"yyyy'-'MM'-'dd";
        public const string IsoShortFormatWithTz = @"yyyy'-'MM'-'ddK";
        public const string IsoLongFormat = @"yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFF";
        public const string IsoLongFormatWithTz = @"yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK";

       [Fact]
        public void DateTimeSortable()
        {
            new DateTime(2015, 12, 24).Kind.ShouldBe(DateTimeKind.Unspecified);

            // sortable => ISO 8601 (without time zone information, aka calendar)
            new DateTime(2015, 12, 24).ToString("s").ShouldBe("2015-12-24T00:00:00");
            new DateTime(2015, 12, 24, 0, 0, 0, DateTimeKind.Unspecified).ToString("s")
                .ShouldBe("2015-12-24T00:00:00");
            new DateTime(2015, 12, 24, 0, 0, 0, DateTimeKind.Local).ToString("s").ShouldBe("2015-12-24T00:00:00");
            new DateTime(2015, 12, 24, 0, 0, 0, DateTimeKind.Utc).ToString("s").ShouldBe("2015-12-24T00:00:00");
        }

        [Fact]
        public void DateTimeRoundtripable()
        {
            new DateTime(2015, 12, 24).Kind.ShouldBe(DateTimeKind.Unspecified);

            // local times assume time zone culture, and are dependent
            // on the machine/OS, via TimeZoneInfo.Local
            var hours = TimeZoneInfo.Local.BaseUtcOffset.Hours;
            string localTimeZoneOffset = $"{(hours >= 0 ? "+" : "")}{hours.ToString("00")}:00";

            // ISO 8601 ("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK" custom format string)
            new DateTime(2015, 12, 24).ToString("o")
                .ShouldBe("2015-12-24T00:00:00.0000000");
            new DateTime(2015, 12, 24, 0, 0, 0, DateTimeKind.Unspecified).ToString("o")
                .ShouldBe("2015-12-24T00:00:00.0000000");
            new DateTime(2015, 12, 24, 0, 0, 0, DateTimeKind.Utc).ToString("o")
                .ShouldBe("2015-12-24T00:00:00.0000000Z");
            new DateTime(2015, 12, 24, 0, 0, 0, DateTimeKind.Local).ToString("o")
                .ShouldBe($"2015-12-24T00:00:00.0000000{localTimeZoneOffset}");
        }

        [Fact]
        public void NowIsLocal()
        {
            // Local is bad!!!
            // In .NET BCL the Local DateTime (implicit) offset is coming from your machine clock setting => BAD!!!
            // The right solution is to use DateTimeOffset data type in place of DateTime. It's available in
            // sql Server starting from the 2008 version and in the .Net framework starting from the 3.5 version
            // It is also correct to use DateTime of all values are converted to Universal (instantanious) time.
            DateTime.Now.Kind.ShouldBe(DateTimeKind.Local);
        }

        [Fact]
        public void NewDateTimeIsUnspecified()
        {
            new DateTime(1970, 6, 3).Kind.ShouldBe(DateTimeKind.Unspecified);
        }

        [Fact]
        public void UtcNowIsUtc()
        {
            DateTime.UtcNow.Kind.ShouldBe(DateTimeKind.Utc);
        }

        [Fact]
        public void UtcNowAndNowAreDifferent()
        {
            // Even though the ticks are always different between Now and UtcNow the hour is also always different
            if (TimeZoneInfo.Local.BaseUtcOffset >= TimeSpan.FromHours(1) ||
                TimeZoneInfo.Local.BaseUtcOffset <= TimeSpan.FromHours(-1))
            {
                Assert.NotEqual(DateTime.Now.Hour, DateTime.UtcNow.Hour);
            }
        }

        // A DateTime value stores its Kind in its most significant two bits: Unspecified (00), Utc (01),
        // Local (10), and LocalAmbiguousDst (11). However, LocalAmbiguousDst is exposed publicly as Local.

        [Fact]
        public void ParseExact()
        {
            // When "Z" (Zulu) is tacked on the end of a time, it indicates that that time is UTC,
            // so really the literal Z is part of the time.
            DateTime.ParseExact("2012-09-30T23:00:00.0000000Z",
                IsoLongFormatWithTz,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal).Kind.ShouldBe(DateTimeKind.Utc);

            // A literal "Z" in the string is directly equivalent to a "+00:00" offset as far as ISO8601 is concerned.
            DateTime.ParseExact("2012-09-30T23:00:00.0000000+00:00",
                IsoLongFormatWithTz,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal).Kind.ShouldBe(DateTimeKind.Utc);
            // but RoundtripKind will use Local perspective/kind
            DateTime.ParseExact("2012-09-30T23:00:00.0000000+00:00",
                IsoLongFormatWithTz,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind).Kind.ShouldBe(DateTimeKind.Local);


            // If you want to convert a DateTime to a string and back to the same DateTime
            // (including preserving the DateTime.Kind setting), use the DateTimeStyles.RoundtripKind flag.
            DateTime.ParseExact("2012-09-30T23:00:00.0000000Z",
                IsoLongFormatWithTz,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind).Kind.ShouldBe(DateTimeKind.Utc);
        }

        [Fact]
        public void ParsedDateTimeIsUnspecified()
        {
            DateTime.ParseExact("2015-12-14", IsoShortFormat, CultureInfo.InvariantCulture, DateTimeStyles.None)
                .Kind.ShouldBe(DateTimeKind.Unspecified);
            // Timezone indicator is ignored (it is optional in the string)
            DateTime.ParseExact("2015-12-14", IsoShortFormatWithTz, CultureInfo.InvariantCulture, DateTimeStyles.None)
                .Kind.ShouldBe(DateTimeKind.Unspecified);

            DateTime.ParseExact("2015-12-14Z", IsoShortFormatWithTz, CultureInfo.InvariantCulture, DateTimeStyles.None)
                .Kind.ShouldBe(DateTimeKind.Local);

            DateTime.ParseExact("2015-12-14", IsoShortFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                .Kind.ShouldBe(DateTimeKind.Unspecified);
            DateTime.ParseExact("2015-12-14", IsoShortFormatWithTz, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind)
                .Kind.ShouldBe(DateTimeKind.Unspecified);
        }

        [Fact]
        public void ParseShortDateWithDateTimeStylesNone()
        {
            DateTime.ParseExact("2015-12-14", IsoShortFormat, CultureInfo.InvariantCulture, DateTimeStyles.None)
                .Kind.ShouldBe(DateTimeKind.Unspecified);

            DateTime.ParseExact("2015-12-14", IsoShortFormatWithTz, CultureInfo.InvariantCulture, DateTimeStyles.None)
                .Kind.ShouldBe(DateTimeKind.Unspecified);
            DateTime.ParseExact("2015-12-14+01:00", IsoShortFormatWithTz, CultureInfo.InvariantCulture,
                DateTimeStyles.None)
                .Kind.ShouldBe(DateTimeKind.Local);
            DateTime.ParseExact("2015-12-14Z", IsoShortFormatWithTz, CultureInfo.InvariantCulture, DateTimeStyles.None)
                .Kind.ShouldBe(DateTimeKind.Local);
        }

        [Fact]
        public void ParseShortDateWithDateTimeStylesRoundtripKind()
        {
            DateTime.ParseExact("2015-12-14", IsoShortFormatWithTz, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind)
                .Kind.ShouldBe(DateTimeKind.Unspecified);
            DateTime.ParseExact("2015-12-14+01:00", IsoShortFormatWithTz, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind)
                .Kind.ShouldBe(DateTimeKind.Local);
            DateTime.ParseExact("2015-12-14Z", IsoShortFormatWithTz, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind)
                .Kind.ShouldBe(DateTimeKind.Utc);
        }

        // DateTimeStyles.None implies that any offset (Time zone indicator, K-part)
        // will translate into Local time. And Local DateTime in .NET BCL is bad.

        [Fact]
        public void ParseWithDateTimeStylesNone()
        {
            DateTime.ParseExact("1970-06-03T23:45:01.000", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.None)
                .Kind.ShouldBe(DateTimeKind.Unspecified);

            DateTime.ParseExact("1970-06-03T23:45:01.000+02:00", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.None)
                .Kind.ShouldBe(DateTimeKind.Local);

            DateTime.ParseExact("1970-06-03T23:45:01.000Z", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.None)
                .Kind.ShouldBe(DateTimeKind.Local);


            DateTime.ParseExact("1970-06-03", IsoShortFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.None)
                .Kind.ShouldBe(DateTimeKind.Unspecified);

            DateTime.ParseExact("1970-06-03+02:00", IsoShortFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.None)
                .Kind.ShouldBe(DateTimeKind.Local);

            DateTime.ParseExact("1970-06-03Z", IsoShortFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.None)
                .Kind.ShouldBe(DateTimeKind.Local);
        }

        // By using DateTimeStyles.RoundtripKind a DateTime will roundtrip correctly w.r.t.
        // Offset and Kind (but offset will be forgotten after parse). Therefore Local
        // should throw an error. We don't want offsets, but all of '1970-06-03', '1970-06-03Z' and
        // '1970-06-03+00:00' and '1970-06-03-00:00' should be okay.

        [Fact]
        public void ParseWithRoundtripKind()
        {
            DateTime.ParseExact("1970-06-03T23:45:01.000", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                .Kind.ShouldBe(DateTimeKind.Unspecified);

            DateTime.ParseExact("1970-06-03T23:45:01.000+02:00", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                .Kind.ShouldBe(DateTimeKind.Local);

            DateTime.ParseExact("1970-06-03T23:45:01.000Z", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                .Kind.ShouldBe(DateTimeKind.Utc);


            DateTime.ParseExact("1970-06-03", IsoShortFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                .Kind.ShouldBe(DateTimeKind.Unspecified);

            DateTime.ParseExact("1970-06-03+02:00", IsoShortFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                .Kind.ShouldBe(DateTimeKind.Local);

            DateTime.ParseExact("1970-06-03Z", IsoShortFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                .Kind.ShouldBe(DateTimeKind.Utc);
        }

        // AssumeLocal and/or AssumeUniversal is about the input string. The output will be local
        // unless you also provide the AdjustToUniversal flag.

        [Fact]
        public void ParseWithAssumeLocal()
        {
            DateTime.ParseExact("1970-06-03T23:45:01.000", IsoLongFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal)
                .Kind.ShouldBe(DateTimeKind.Local);

            DateTime.ParseExact("1970-06-03T23:45:01.000+02:00", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal)
                .Kind.ShouldBe(DateTimeKind.Local);

            DateTime.ParseExact("1970-06-03T23:45:01.000Z", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal)
                .Kind.ShouldBe(DateTimeKind.Local);
        }

        [Fact]
        public void ParseWithAssumeUniversal()
        {
            DateTime.ParseExact("1970-06-03T23:45:01.000", IsoLongFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                .Kind.ShouldBe(DateTimeKind.Local);

            DateTime.ParseExact("1970-06-03T23:45:01.000+02:00", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                .Kind.ShouldBe(DateTimeKind.Local);

            DateTime.ParseExact("1970-06-03T23:45:01.000Z", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                .Kind.ShouldBe(DateTimeKind.Local);
        }

        [Fact]
        public void ParseWithAssumeLocalAndAdjustToUniversal()
        {
            DateTime.ParseExact("1970-06-03T23:45:01.000", IsoLongFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal)
                .Kind.ShouldBe(DateTimeKind.Utc);

            DateTime.ParseExact("1970-06-03T23:45:01.000+02:00", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal)
                .Kind.ShouldBe(DateTimeKind.Utc);

            DateTime.ParseExact("1970-06-03T23:45:01.000Z", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal)
                .Kind.ShouldBe(DateTimeKind.Utc);
        }

        [Fact]
        public void ParseWithAssumeUniversalAndAdjustToUniversal()
        {
            DateTime.ParseExact("1970-06-03T23:45:01.000", IsoLongFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
                .Kind.ShouldBe(DateTimeKind.Utc);

            DateTime.ParseExact("1970-06-03T23:45:01.000+02:00", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
                .Kind.ShouldBe(DateTimeKind.Utc);

            DateTime.ParseExact("1970-06-03T23:45:01.000Z", IsoLongFormatWithTz,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
                .Kind.ShouldBe(DateTimeKind.Utc);
        }
    }
}
