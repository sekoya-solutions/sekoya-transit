#nullable enable
using System;
using System.Globalization;

namespace SekoyaTransit.Core.Simulation
{
    /// <summary>
    /// A date in the simulation's fixed 365-day calendar, which has no leap days.
    /// </summary>
    public readonly struct GameDate : IEquatable<GameDate>, IComparable<GameDate>
    {
        public const int FirstYear = 1900;
        public const int DaysPerYear = 365;

        private static readonly int[] _daysPerMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        /// <summary>
        /// Initializes a date in the simulation's fixed calendar.
        /// </summary>
        /// <param name="year">The calendar year, starting at 1900.</param>
        /// <param name="month">The one-based month.</param>
        /// <param name="day">The one-based day within the month.</param>
        public GameDate(int year, int month, int day)
        {
            if (year < FirstYear)
            {
                throw new ArgumentOutOfRangeException(nameof(year), year, "Calendar years start at 1900.");
            }

            if (month < 1 || month > _daysPerMonth.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be between 1 and 12.");
            }

            if (day < 1 || day > _daysPerMonth[month - 1])
            {
                throw new ArgumentOutOfRangeException(nameof(day), day, "Day is outside the selected month.");
            }

            Year = year;
            Month = month;
            Day = day;
        }

        /// <summary>
        /// Gets the year, beginning at 1900.
        /// </summary>
        public int Year { get; }

        /// <summary>
        /// Gets the one-based month in the year.
        /// </summary>
        public int Month { get; }

        /// <summary>
        /// Gets the one-based day in the month.
        /// </summary>
        public int Day { get; }

        /// <summary>
        /// Gets the number of days in this date's month.
        /// </summary>
        public int DaysInMonth
        {
            get
            {
                return _daysPerMonth[Month - 1];
            }
        }

        /// <summary>
        /// Returns the date <paramref name="months"/> calendar months later, keeping the day of month
        /// or clamping it to the last day of the target month.
        /// </summary>
        public GameDate AddMonths(int months)
        {
            if (months < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(months), months, "Months to add cannot be negative.");
            }

            long monthIndex = ((long)Year * _daysPerMonth.Length) + (Month - 1) + months;
            int year = checked((int)(monthIndex / _daysPerMonth.Length));
            int month = (int)(monthIndex % _daysPerMonth.Length) + 1;
            int day = Math.Min(Day, _daysPerMonth[month - 1]);
            return new GameDate(year, month, day);
        }

        internal static GameDate FromElapsedDays(long elapsedDays)
        {
            if (elapsedDays < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedDays), elapsedDays, "Elapsed calendar days cannot be negative.");
            }

            long elapsedYears = elapsedDays / DaysPerYear;
            int year = checked(FirstYear + (int)elapsedYears);
            int dayOfYear = (int)(elapsedDays % DaysPerYear);

            for (int monthIndex = 0; monthIndex < _daysPerMonth.Length; monthIndex++)
            {
                if (dayOfYear < _daysPerMonth[monthIndex])
                {
                    return new GameDate(year, monthIndex + 1, dayOfYear + 1);
                }

                dayOfYear -= _daysPerMonth[monthIndex];
            }

            throw new InvalidOperationException("The fixed calendar produced an invalid day of year.");
        }

        /// <inheritdoc />
        public int CompareTo(GameDate other)
        {
            int yearComparison = Year.CompareTo(other.Year);
            if (yearComparison != 0)
            {
                return yearComparison;
            }

            int monthComparison = Month.CompareTo(other.Month);
            return monthComparison != 0 ? monthComparison : Day.CompareTo(other.Day);
        }

        /// <inheritdoc />
        public bool Equals(GameDate other)
        {
            return Year == other.Year && Month == other.Month && Day == other.Day;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is GameDate other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Year * 397) ^ Month) * 397 ^ Day;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Year.ToString("D4", CultureInfo.InvariantCulture)
                + "-"
                + Month.ToString("D2", CultureInfo.InvariantCulture)
                + "-"
                + Day.ToString("D2", CultureInfo.InvariantCulture);
        }

        public static bool operator ==(GameDate left, GameDate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameDate left, GameDate right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(GameDate left, GameDate right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(GameDate left, GameDate right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(GameDate left, GameDate right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(GameDate left, GameDate right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}
