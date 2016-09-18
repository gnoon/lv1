using System;
using System.Globalization;
using System.Data;

namespace FingerScan
{
    public static class Extensions
    {
        public static T GetValue<T>(this IDataReader reader, string name)
        {
            object value = reader[name];

            Type t = typeof(T);
            t = Nullable.GetUnderlyingType(t) ?? t;

            return (value == null || DBNull.Value.Equals(value)) ?
                default(T) : (T)Convert.ChangeType(value, t);
        }
    }
}
