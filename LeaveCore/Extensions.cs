using System;
using System.Data.SqlClient;
using System.Globalization;
using System.Data;

namespace LeaveCore
{
    public static class Extensions
    {
        public static T GetValue<T>(this IDataReader reader, string name)
        {
            object value = null;
            if (!reader.IsDBNull(reader.GetOrdinal(name)))
                value = reader[name];

            Type t = typeof(T);
            t = Nullable.GetUnderlyingType(t) ?? t;

            return (value == null || DBNull.Value.Equals(value)) ?
                default(T) : (T)Convert.ChangeType(value, t);
        }

		public static int GetWeekOfMonth(this DateTime date)
		{
			DateTime first = new DateTime(date.Year, date.Month, 1);
			return date.GetWeekOfYear() - first.GetWeekOfYear() + 1;
		}

		static int GetWeekOfYear(this DateTime date)
		{
			GregorianCalendar gc = new GregorianCalendar();
			return gc.GetWeekOfYear(date, CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
		}

		public static DateTime GetDateOfWeek(this int DayOfWeek)
		{
			if (DayOfWeek < 0 || DayOfWeek > 6) throw new ArgumentOutOfRangeException("Day is invalid.");
			
			// 4 January 2009 was a Sunday
			return new DateTime(2009,1,4).AddDays(DayOfWeek);
		}
    }
}
