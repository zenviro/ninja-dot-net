using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using NodaTime;

namespace Zenviro.Ninja
{
    static class Schedule
    {
        /// <summary>
        /// Gets the duration of the sleep.
        /// </summary>
        /// <value>
        /// The duration of the sleep.
        /// </value>
        /// <remarks>
        /// If it's waking hours, use the WakeFrequency value.
        /// If it's sleeping hours, and we are configured to work slowly, use SleepFrequency.
        /// If it's sleeping hours, and we are configured to pause work, use DefaultSleepFrequency.
        /// </remarks>
        public static int PauseBetweenCycles
        {
            get
            {
                return IsWakeTime
                    ? WakeFrequency
                    : ((SleepFrequency > 0)
                        ? SleepFrequency
                        : DefaultSleepFrequency);
            }
        }

        /// <summary>
        /// Gets a value indicating whether [should work].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [should work]; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>
        /// If it's waking hours, we should work.
        /// If it's sleeping hours, check if we are configured to pause work.
        /// </remarks>
        public static bool ShouldWork
        {
            get { return (IsWakeTime) || SleepFrequency > 0; }
        }

        private static bool IsWakeTime
        {
            get
            {
                var now = SystemClock.Instance.Now.InUtc();
                var wakeTime = new LocalDateTime(now.Year, now.Month, now.Day, WakeTime.Hour, WakeTime.Minute).InUtc();
                var sleepTime = new LocalDateTime(now.Year, now.Month, now.Day, SleepTime.Hour, SleepTime.Minute).InUtc();
                return now >= wakeTime && now < sleepTime && WakeDays.Contains(now.IsoDayOfWeek);
            }
        }

        private const int DefaultWakeFrequency = 5;
        private static int WakeFrequency
        {
            get
            {
                int value;
                if (int.TryParse(ConfigurationManager.AppSettings.Get("WakeFrequency"), out value))
                    return value * 60 * 1000;
                return DefaultWakeFrequency * 60 * 1000;
            }
        }

        private const int DefaultSleepFrequency = 60;
        private static int SleepFrequency
        {
            get
            {
                int value;
                if (int.TryParse(ConfigurationManager.AppSettings.Get("SleepFrequency"), out value))
                    return value * 60 * 1000;
                return DefaultSleepFrequency * 60 * 1000;
            }
        }

        private static readonly LocalTime DefaultWakeTime = new LocalTime(8, 0);
        private static LocalTime WakeTime
        {
            get
            {
                return LazyTimeParse(ConfigurationManager.AppSettings.Get("WakeTime"), DefaultWakeTime);
            }
        }

        private static readonly LocalTime DefaultSleepTime = new LocalTime(19, 0);
        private static LocalTime SleepTime
        {
            get
            {
                return LazyTimeParse(ConfigurationManager.AppSettings.Get("SleepTime"), DefaultSleepTime);
            }
        }

        private static readonly IEnumerable<IsoDayOfWeek> DefaultWakeDays = new[] { IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Thursday, IsoDayOfWeek.Friday };
        private static IEnumerable<IsoDayOfWeek> WakeDays
        {
            get
            {
                try
                {
                    return ConfigurationManager.AppSettings.Get("WakeDays").Split(new[] { ',', ';', ' ', ':' }, StringSplitOptions.RemoveEmptyEntries).Select(LazyDayOfWeekParse);
                }
                catch
                {
                    return DefaultWakeDays;
                }
            }
        }

        private static LocalTime LazyTimeParse(string time, LocalTime fallback = new LocalTime())
        {
            if (string.IsNullOrEmpty(time))
                return fallback;
            if (time.Contains(":"))
                try
                {
                    var t = time.Trim().Split(':');
                    return new LocalTime(int.Parse(t[0]), int.Parse(t[1]));
                }
                catch
                {
                    return fallback;
                }
            if (time.Trim().Length >= 4)
                try
                {
                    var t = time.Trim();
                    return new LocalTime(int.Parse(t.Substring(0, 2)), int.Parse(t.Substring(2, 2)));
                }
                catch
                {
                    return fallback;
                }
            return fallback;
        }

        private static IsoDayOfWeek LazyDayOfWeekParse(string dow)
        {
            var map = new Dictionary<int, IsoDayOfWeek>
            {
                { (int)IsoDayOfWeek.Monday, IsoDayOfWeek.Monday },
                { (int)IsoDayOfWeek.Tuesday, IsoDayOfWeek.Tuesday },
                { (int)IsoDayOfWeek.Wednesday, IsoDayOfWeek.Wednesday },
                { (int)IsoDayOfWeek.Thursday, IsoDayOfWeek.Thursday },
                { (int)IsoDayOfWeek.Friday, IsoDayOfWeek.Friday },
                { (int)IsoDayOfWeek.Saturday, IsoDayOfWeek.Saturday },
                { (int)IsoDayOfWeek.Sunday, IsoDayOfWeek.Sunday }
            };
            int value;
            return int.TryParse(dow, out value)
                ? map[value]
                : IsoDayOfWeek.None;
        }
    }
}
