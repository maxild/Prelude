using System;
using System.Globalization;

namespace Maxfire.Prelude
{
    /// <summary>
    /// Enumeration of named values for formatting ('stringifying') and parsing dates.
    /// No time zone information is present in any format, because <see cref="Date" />
    /// represents with no reference to any particular time zone (local or universal).
    /// </summary>
    public class DateFormat : Enumeration<DateFormat>
    {
        private static readonly Tuple<Func<CultureInfo>, DateTimeStyles> s_nonParseableData =
            new(() => CultureInfo.CurrentCulture, DateTimeStyles.None);

        private static readonly Tuple<Func<CultureInfo>, DateTimeStyles> s_parseableData =
            new(() => CultureInfo.InvariantCulture, DateTimeStyles.None);

        /// <summary>
        /// Culture-insensitive 'd/M-yyyy' format of variable length --- christmas in 2015
        /// is represented as '24/12-2015'.
        /// </summary>
        public static readonly DateFormat Default
            = new DateFormat(0, "Default", "Short human readable format (27/1-2003)",
                             @"d'\/'M'-'yyyy", s_parseableData);

        /// <summary>
        /// Culture-sensitive short date format --- christmas in 2015 is
        /// represented as '24-12-2015' using danish culture da-DK.
        /// </summary>
        public static readonly DateFormat Short
            = new DateFormat(1, "Short", "Short human readable format (27-01-2003)",
                             @"d", s_nonParseableData);

        /// <summary>
        /// Culture-sensitive long date format --- christmas in 2015 is
        /// represented as '24. december 2015' using danish culture da-DK.
        /// </summary>
        public static readonly DateFormat Long
            = new DateFormat(2, "Long", "Long human readable format (27. januar 2003)",
                             @"D", s_nonParseableData);

        /// <summary>
        /// Culture-insensitive 'ddMMyyyy' format of fixed length 8 --- christmas
        /// in 2015 is represented as '24122015'.
        /// </summary>
        public static readonly DateFormat DayMonthYear
            = new DateFormat(3, "DayMonthYear", "Machine readable format with fixed length of 8 (27012003)",
                             @"ddMMyyyy", s_parseableData);

        /// <summary>
        /// Culture-insensitive 'yyyyMMdd' format of fixed length 8 --- christmas
        /// in 2015 is represented as '20151224'.
        /// </summary>
        public static readonly DateFormat YearMonthDay
            = new DateFormat(4, "YearMonthDay", "Machine readable format with fixed length of 8 (20030127)",
                             @"yyyyMMdd", s_parseableData);

        /// <summary>
        /// Culture-insensitive 'dd-MM-yyyy' format of fixed length 10 --- christmas in 2015
        /// is represented as '24-12-2015'.
        /// </summary>
        public static readonly DateFormat ReverseIso
            = new DateFormat(5, "ReverseIso", "Year-last non-sortable format with fixed length of 10.",
                             @"dd'-'MM'-'yyyy", s_parseableData);

        /// <summary>
        /// Culture-insensitive 'yyyy-MM-dd' profile/format (with no time zone indicator) of
        /// fixed length 10 that will generate sortable text representations of
        /// dates  --- christmas in 2015 is represented as '2015-12-24'.
        /// </summary>
        public static readonly DateFormat Iso
            = new DateFormat(6, "Iso", "Year-first sortable ISO 8601 format with fixed length of 10.",
                             @"yyyy'-'MM'-'dd", s_parseableData);

        // Important to be able resolve Thread.CurrentThread.CurrentCulture at each invocation => lambda/func
        private readonly Tuple<Func<CultureInfo>, DateTimeStyles> _data;

        private DateFormat(
            int value,
            string name,
            string text,
            string format,
            Tuple<Func<CultureInfo>, DateTimeStyles> data) : base(value, name, text)
        {
            Format = format;
            _data = data;
        }

        /// <summary>
        /// Format string array to use with ParseExact and ToString.
        /// </summary>
        public string Format { get; }

        /// <summary>
        /// Culture to use with ParseExact and ToString
        /// </summary>
        public CultureInfo Culture => _data.Item1();

        /// <summary>
        /// Style to use with ParseExact.
        /// </summary>
        public DateTimeStyles Style => _data.Item2;

        /// <summary>
        /// If true, this value can be passed to Date.Parse. Otherwise Date.Parse will throw.
        /// </summary>
        public bool IsParseable => ReferenceEquals(_data, s_parseableData);
    }
}
