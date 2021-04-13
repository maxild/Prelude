using System;
using System.Runtime.InteropServices;
using Shouldly;
using Xunit;

namespace Maxfire.Prelude.Tests
{
    // Avoid the following types in .NET BCL (your code should not depend on the computers global time zone settings)
    //    DateTimeKind.Local
    //    DateTime.Now/Today
    //    DateTime.ToUniversalTime() (assumes UTC when unspecified)
    //    DateTime.ToLocalTime (assumes universal...when unspecified)
    //    TimeZoneInfo.ConvertTimeToUtc(DateTime) // no source time zone
    //    TimeZoneInfo.ConvertTime(DateTime, TimeZoneInfo) // source time zone
    //    TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime, String) // source time zone

    public class TimeZoneLearningTests
    {
        enum Zone
        {
            Copenhagen,
            London,
            NewYork
        }

        private static string GetZoneId(Zone zone)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return zone switch
                {
                    Zone.Copenhagen => "Romance Standard Time",
                    Zone.London => "GMT Standard Time",
                    Zone.NewYork => "Eastern Standard Time",
                    _ => throw new InvalidOperationException()
                };
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return zone switch
                {
                    Zone.Copenhagen => "Europe/Copenhagen",
                    Zone.London => "Europe/London",
                    Zone.NewYork => "US/Eastern",
                    _ => throw new InvalidOperationException()
                };
            }

            throw new InvalidOperationException("I don't know how to get the time zone for your OS.");
        }

        [Fact]
        public void ConvertTime()
        {
            var utcNow = new DateTime(2015, 12, 24, 21, 45, 30, DateTimeKind.Utc);
            //var localNow = new DateTime(2015, 12, 24, 21, 45, 30, DateTimeKind.Local);
            var unspecifiedNow = new DateTime(2015, 12, 24, 21, 45, 30, DateTimeKind.Unspecified);

            // all values (except the offset) are equal
            utcNow.ToString("o")
                .ShouldBe("2015-12-24T21:45:30.0000000Z"); // UTC => Zero = Z
            unspecifiedNow.ToString("o")
                .ShouldBe("2015-12-24T21:45:30.0000000"); // Unspecified => unknown offset (unknown time zone)

            var newYork = TimeZoneInfo.FindSystemTimeZoneById(GetZoneId(Zone.NewYork));
            var london = TimeZoneInfo.FindSystemTimeZoneById(GetZoneId(Zone.London));
            var copenhagen = TimeZoneInfo.FindSystemTimeZoneById(GetZoneId(Zone.Copenhagen));

            var newYorkNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, newYork);
            var londonNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, london);
            var copenhagenNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, copenhagen);

            // Time zone based conversions => Unspecified
            newYorkNow.Kind.ShouldBe(DateTimeKind.Unspecified);

            // all values are different after conversion (and time zone is also unspecified)
            // WHY didn't we remember the time zones used at ConvertTimeFromUtc time
            newYorkNow.ToString("o")
                .ShouldBe("2015-12-24T16:45:30.0000000"); // -7 hours offset
            londonNow.ToString("o")
                .ShouldBe("2015-12-24T21:45:30.0000000"); // no offset (london is UTC)
            copenhagenNow.ToString("o")
                .ShouldBe("2015-12-24T22:45:30.0000000"); // 1 hour offset
        }
    }
}
