using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Maxfire.Prelude.ComponentModel;
using Maxfire.Prelude.Linq;

namespace Maxfire.Prelude
{
    /// <summary>
    /// The Date type is an immutable struct representing a date within the ISO calendar,
    /// with no reference to a particular time zone or time of day (i.e. not a system-specific
    /// "local" date).
    ///
    /// A Date value does not represent an instant on the global time line, because
    /// it has no associated time zone: In the real world "December 24th 2015" occurred at different
    /// instants for different people around the world, but Date does not keep track on such things.
    ///
    /// The Date value is just a (day, month, year)-tuple. This is to keep the Date type as
    /// simple as possible. The types main purpose is for day count calculations etc.
    ///
    /// If the context of 'whose date this is' (Europe/Copenhagen, UTC/GMT etc.) is important,
    /// the application layer must somehow keep this context (offset and/or time zone) connected
    /// to any Date value, or somehow convert between Date values and DateTimeOffset values
    /// (or UTC DateTime values).
    ///
    /// In most countries (including Denmark) the DST transformation in spring and fall will
    /// not affect the date part --- That is midnight will always exist regardless of DST
    /// transformation, and the transformation will not affect the year/month/day values when performed.
    /// Therefore Date math will work all year around without any DST transformation problems.
    ///
    /// The tricky (read: error-prone) part of ignoring Local vs Universal time is when constructing
    /// dates from either strings (Date.Parse) or from DateTime values (Date.FromDateTime). In any
    /// of these cases the code does not perform any time zone conversions. This way Date will completely
    /// ignore any system-specific timezone settings or any <see cref="DateTime.Kind"/> value.
    ///
    /// The Date type is implemented as a simple wrapper around the <see cref="System.DateTime"/> struct.
    /// In the .NET Framework, the DateTime struct is represented by a long value called Ticks.
    /// One tick is equal to 100 ns, and ticks are counted starting from 12:00 AM January 1, year 1 A.D.
    /// See also https://aakinshin.net/posts/datetime/ for detailed implementation details.
    /// </summary>
    [Serializable]
    [TypeConverter(typeof(DateTypeConverter))]
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    public struct Date : IComparable, IFormattable, IComparable<Date>, IEquatable<Date>
    {
        class DateTypeConverter : AbstractTypeConverter<Date>
        {
            public DateTypeConverter() : base(supportNullToEmptyString: false)
            {
            }

            protected override Date Parse(string s, CultureInfo? culture)
            {
                Date? result = Date.TryParse(s, DateFormat.Default) ??
                               Date.TryParse(s, DateFormat.Iso);

                return result ?? throw new FormatException(GetParseErrorMessage(s));
            }

            protected override string Stringify(Date value, CultureInfo? culture)
            {
                return value.ToDefaultString();
            }
        }

        private string DebuggerDisplay => $"{ToDefaultString()}";

        // In the .NET Common Type System, the System.DateTime struct is represented by a long value called Ticks.
        // One tick is equal to 100 ns. Ticks are counted starting from 12:00 AM January 1, year 1 A.D. (Gregorian Calendar).
        private readonly DateTime _unspecifiedDate;

        // This is the time zone object for 'Europe/Copenhagen' in spite its name 'standard time'. That is
        // this time zone object is used both for copenhagen standard time and copenhagen daylight/summer time.
        private static readonly Lazy<TimeZoneInfo> s_danishTimeZoneInfo =
            new(GetDanishTimeZoneInfo, isThreadSafe: true);

        // Unfortunately Windows, Linux and Mac have different timezone system
        private static TimeZoneInfo GetDanishTimeZoneInfo()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use proprietary windows time zone
                return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Use tz database time zones (IANA time zones)
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
            }

            throw new InvalidOperationException("I don't know how to get the danish time zone for your OS.");
        }

        /// <summary>
        /// Today in the 'Europe/Copenhagen' time zone (either standard time,
        /// during winter, or daylight saving time, during summer, aka summer time).
        /// </summary>
        public static Date DanishToday
        {
            get
            {
                DateTime danishNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, s_danishTimeZoneInfo.Value);
                return new Date(danishNow);
            }
        }

        /// <summary>
        /// Today in UTC time.
        /// </summary>
        public static Date UtcToday => new Date(DateTime.UtcNow);

        /// <summary>
        /// Today in system-specific local time.
        /// </summary>
        public static Date LocalToday => new Date(DateTime.Now);


        /// <summary>
        /// Converts a <see cref="DateTime" /> of any kind to a Date value, ignoring the time of day.
        /// This does not perform any time zone conversions, so a DateTime with a <see cref="DateTime.Kind"/>
        /// of <see cref="DateTimeKind.Utc"/> will still represent the same year/month/day - it won't
        /// be converted into the local system time.
        /// </summary>
        /// <param name="dateTime">A DateTime value to convert into a Date value.</param>
        /// <returns>A new <see cref="Date"/> value with the same date-part values as the specified <c>DateTime</c> value.</returns>
        public static Date FromDateTime(DateTime dateTime)
        {
            return new Date(dateTime);
        }

        /// <summary>
        /// Constructs a <see cref="DateTime"/> from this value which has a <see cref="DateTime.Kind" />
        /// of <see cref="DateTimeKind.Unspecified"/>. The result is midnight on the day represented
        /// by this value.
        /// </summary>
        /// <remarks>
        /// <see cref="DateTimeKind.Unspecified"/> is slightly odd - it can be treated as UTC if you
        /// use <see cref="DateTime.ToLocalTime"/> or as system local time if you use
        /// <see cref="DateTime.ToUniversalTime"/>, but it's the only kind which allows
        /// you to construct a <see cref="DateTimeOffset"/> with an arbitrary offset.
        /// </remarks>
        /// <returns>A <see cref="DateTime"/> value at midnight for the same date as this value.</returns>
        [Pure]
        public DateTime ToDateTimeUnspecified()
        {
            return _unspecifiedDate;
        }

        public static Date Parse(string s)
        {
            return Parse(s, DateFormat.Default);
        }

        public static Date Parse(string s, DateFormat dateFormat)
        {
            DateFormat dateFormatToUse = ResolveDateFormatForParsing(dateFormat);
            // This can be either Unspecified, Local or Utc (depending on time zone indicator)
            //
            DateTime dateTime =
                DateTime.ParseExact(s, dateFormatToUse.Format, dateFormatToUse.Culture, dateFormatToUse.Style);
            //if (unspecified.TimeOfDay != TimeSpan.Zero)
            //{
            //    throw new FormatException($"Unable to parse the string '{s}' to a {typeof(Date).FullName} value. -- The time of day is not midnight.");
            //}
            return new Date(dateTime);
        }

        public static Date? TryParse(string? s)
        {
            return TryParse(s, DateFormat.Default);
        }

        public static Date? TryParse(string? s, DateFormat dateFormat)
        {
            DateFormat dateFormatToUse = ResolveDateFormatForParsing(dateFormat);

            if (DateTime.TryParseExact(s, dateFormatToUse.Format, dateFormatToUse.Culture, dateFormatToUse.Style,
                out var unspecified))
            {
                if (unspecified.TimeOfDay != TimeSpan.Zero)
                {
                    return null;
                }

                return new Date(unspecified);
            }

            return null; // parse failure
        }

        private static DateFormat ResolveDateFormatForParsing(DateFormat? dateFormat)
        {
            DateFormat dateFormatToUse = dateFormat ?? DateFormat.Default;
            if (dateFormatToUse.IsParseable == false)
            {
                throw new InvalidOperationException(
                    $"{nameof(DateFormat)}.{dateFormatToUse.Name} is not a parseable format.");
            }

            return dateFormatToUse;
        }

        public static readonly Date MaxValue = FromDateTime(DateTime.MaxValue);
        public static readonly Date MinValue = FromDateTime(DateTime.MinValue);

        public static Date Max(Date d1, Date d2)
        {
            return d1 > d2 ? d1 : d2;
        }

        public static Date Max(Date d1, Date d2, Date d3)
        {
            return Max(Max(d1, d2), d3);
        }

        public static Date Min(Date d1, Date d2)
        {
            return d1 > d2 ? d2 : d1;
        }

        public static Date Min(Date d1, Date d2, Date d3)
        {
            return Min(Min(d1, d2), d3);
        }

        public static int DaysInMonth(int year, int month)
        {
            return DateTime.DaysInMonth(year, month);
        }

        public static bool IsEndOfMonth(Date d)
        {
            return DaysInMonth(d.Year, d.Month) == d.Day;
        }

        public static int DaysInYear(int year)
        {
            return 365 + LeapYear(year);
        }

        public static int LeapYear(int year)
        {
            return IsLeapYear(year) ? 1 : 0;
        }

        public static bool IsLeapYear(int year)
        {
            return DateTime.IsLeapYear(year);
        }

        private static bool IsDayAndMonthLessThanOrEqual(Date d1, Date d2)
        {
            if (d1.Month < d2.Month)
            {
                return true;
            }

            if (d1.Month > d2.Month)
            {
                return false;
            }

            if (d1.Day < d2.Day)
            {
                return true;
            }

            if (d1.Day > d2.Day)
            {
                return false;
            }

            return true;
        }

        public static int HoleYearDifference(Date d1, Date d2)
        {
            int sign = 1;
            if (d1 > d2)
            {
                Date tmp = d1;
                d1 = d2;
                d2 = tmp;
                sign = -1;
            }

            int diff = IsDayAndMonthLessThanOrEqual(d1, d2) ? d2.Year - d1.Year : d2.Year - d1.Year - 1;
            return sign * diff;
        }

        private Date(DateTime dateTime)
        {
            _unspecifiedDate = DateTime.SpecifyKind(dateTime.Date, DateTimeKind.Unspecified);
        }

        public Date(int year, int month, int day)
        {
            _unspecifiedDate = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
        }

        public override string ToString()
        {
            return ToDefaultString();
        }

        [Pure]
        public string ToDefaultString()
        {
            return ToString(DateFormat.Default);
        }

        [Pure]
        public string ToShortDateString()
        {
            return ToString(DateFormat.Short);
        }

        [Pure]
        public string ToLongDateString()
        {
            return ToString(DateFormat.Long);
        }

        [Pure]
        public string ToIsoDateString()
        {
            return ToString(DateFormat.Iso);
        }

        [Pure]
        public string ToYearMonthDayDateString()
        {
            return ToString(DateFormat.YearMonthDay);
        }

        [Pure]
        public string ToString(DateFormat dateFormat)
        {
            return _unspecifiedDate.ToString(dateFormat.Format, dateFormat.Culture);
        }

        string IFormattable.ToString(string? format, IFormatProvider? formatProvider)
        {
            if (format is null || format == "G")
                return ToString();

            if (Enumeration.GetAll<DateFormat>().Map(x => x.Format).Any(f => f == format))
            {
                return formatProvider is null
                    ? _unspecifiedDate.ToString(format)
                    : _unspecifiedDate.ToString(format, formatProvider);
            }

            throw new FormatException("Unknown format: " + format);
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (obj.GetType() != typeof(Date))
            {
                return false;
            }

            return Equals((Date) obj);
        }

        public bool Equals(Date date)
        {
            return _unspecifiedDate.Equals(date._unspecifiedDate);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int y = _unspecifiedDate.Year;
                int m = _unspecifiedDate.Month;
                int d = _unspecifiedDate.Day;
                int hc = 37 + y << 16;
                hc = 37 * hc + m << 8;
                hc = 37 * hc + d << 4;
                return hc;
            }
        }

        public DayOfWeek DayOfWeek => _unspecifiedDate.DayOfWeek;

        /// <summary>
        /// The returned value is an integer between 1 and 366.
        /// </summary>
        public int DayOfYear => _unspecifiedDate.DayOfYear;

        public int Year => _unspecifiedDate.Year;

        public int Month => _unspecifiedDate.Month;

        public int Day => _unspecifiedDate.Day;

        [Pure]
        public Date AddDays(int value)
        {
            return FromDateTime(_unspecifiedDate.AddDays(value));
        }

        [Pure]
        public Date AddMonths(int value)
        {
            return FromDateTime(_unspecifiedDate.AddMonths(value));
        }

        [Pure]
        public Date AddYears(int value)
        {
            return FromDateTime(_unspecifiedDate.AddYears(value));
        }

        [Pure]
        public int Subtract(Date date)
        {
            return _unspecifiedDate.Subtract(date._unspecifiedDate).Days;
        }

        public static bool operator ==(Date lhs, Date rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(Date lhs, Date rhs)
        {
            return lhs.Equals(rhs) == false;
        }

        public static bool operator >=(Date lhs, Date rhs)
        {
            return lhs._unspecifiedDate >= rhs._unspecifiedDate;
        }

        public static bool operator <=(Date lhs, Date rhs)
        {
            return lhs._unspecifiedDate <= rhs._unspecifiedDate;
        }

        public static bool operator >(Date lhs, Date rhs)
        {
            return lhs._unspecifiedDate > rhs._unspecifiedDate;
        }

        public static bool operator <(Date lhs, Date rhs)
        {
            return lhs._unspecifiedDate < rhs._unspecifiedDate;
        }

        public static Date operator +(Date lhs, int rhs)
        {
            return lhs.AddDays(rhs);
        }

        public static int operator -(Date lhs, Date rhs)
        {
            return lhs.Subtract(rhs);
        }

        public static Date operator -(Date lhs, int rhs)
        {
            return lhs.AddDays(-rhs);
        }

        [SuppressMessage("ReSharper", "ArrangeRedundantParentheses")]
        public static Date CalcEasterSunday(int year)
        {
            int gn = (year % 19) + 1;

            int ed, e;
            if (year > 1582)
            {
                int century = year / 100 + 1;
                int gc = ((3 * century) / 4) - 12;
                int cc = (century - 16 - (century - 18) / 25) / 3;
                ed = ((5 * year) / 4) - gc - 10;
                e = ((11 * gn + 19 + cc - gc) % 30) + 1;
                if ((e == 25 && gn > 11) || (e == 24))
                {
                    e = e + 1;
                }
            }
            else
            {
                ed = (5 * year) / 4;
                e = ((11 * gn - 4) % 30) + 1;
            }

            int day = 44 - e;
            if (day < 21)
            {
                day = day + 30;
            }

            day = day + 7 - ((ed + day) % 7);

            int month;
            if (day > 31)
            {
                month = 4;
                day = day - 31;
            }
            else
            {
                month = 3;
            }

            return new Date(year, month, day);
        }

        public int CompareTo(Date date)
        {
            return _unspecifiedDate.CompareTo(date._unspecifiedDate);
        }

        int IComparable.CompareTo(object? obj)
        {
            if (obj is null)
            {
                return 1;
            }

            if (!(obj is Date))
            {
                throw new ArgumentException($"The compared object instance is not of type {typeof(Date).FullName}.");
            }

            return CompareTo((Date) obj);
        }
    }
}
