using System;
using Shouldly;
using Xunit;

namespace Maxfire.Prelude.Tests
{
    public class DateTimeOffsetLearningTests
    {
        [Fact]
        public void IsAlwaysLocalTimeButZeroOffsetCorrespondsToInstantaniousUniversalTime()
        {
            new DateTimeOffset(1970, 6, 3, 0, 0, 0, TimeSpan.Zero).ToString("o")
                .ShouldBe("1970-06-03T00:00:00.0000000+00:00");
            new DateTimeOffset(1970, 6, 3, 0, 0, 0, TimeSpan.Zero).UtcDateTime.ToString("o")
                .ShouldBe("1970-06-03T00:00:00.0000000Z");

            // Some local time (time zone is unknown) is 1970-06-03T10:00:00. The offset is +01:00,
            // and UTC time is therefore 1970-06-03T09:00:00.
            var dateTimeOffset = new DateTimeOffset(1970, 6, 3, 10, 0, 0, TimeSpan.FromHours(1));
            // m_datetime is datetime - offset (utc part)
            dateTimeOffset.ToString("o").ShouldBe("1970-06-03T10:00:00.0000000+01:00");


            // Unspecified is the correct kind in Date.ToDateTimeUnspecified()
            var utc = dateTimeOffset.UtcDateTime; // DateTime.SpecifyKind(m_dateTime, DateTimeKind.Utc);
            utc.Kind.ShouldBe(DateTimeKind.Utc);
            utc.Hour.ShouldBe(9); // time is converted to utc (offset is ignored)

            // Unspecified is the correct kind in Date.ToDateTimeUnspecified()
            var local = dateTimeOffset.LocalDateTime; // UtcDateTime.ToLocalTime() THIS IS BAD!!!!!
            local.Kind.ShouldBe(DateTimeKind.Local);
            local.Hour.ShouldBe(9 + TimeZoneInfo.Local.GetUtcOffset(dateTimeOffset).Hours);
                // time is converted to utc + utcoffset

            // Unspecified is the correct kind in Date.ToDateTimeUnspecified()
            var unspecified = dateTimeOffset.DateTime;
                // new DateTime((m_dateTime + Offset).Ticks, DateTimeKind.Unspecified);
            unspecified.Kind.ShouldBe(DateTimeKind.Unspecified);
            unspecified.Hour.ShouldBe(10); // this is the dateTime passed in (ignoring the offset)

            // Unspecied DateTime has no offset
            new DateTimeOffset(1970, 6, 3, 0, 0, 0, TimeSpan.FromHours(1)).DateTime.ToString("o")
                .ShouldBe("1970-06-03T00:00:00.0000000");
        }
    }
}
