using System;
using log4net;

namespace Zenviro.Ninja
{
    public static class Extensions
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Extensions));

        public static bool Contains(this string source, string value, StringComparison comparisonType)
        {
            return source.IndexOf(value, comparisonType) >= 0;
        }
    }
}