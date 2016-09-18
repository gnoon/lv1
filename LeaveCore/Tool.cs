using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Globalization;
using System.Xml;
using System.Runtime.Serialization;
using LeaveCore.Security;

namespace LeaveCore
{
    public class Tool
    {
        public static Dictionary<string, string> ReadColumns(DataTable Schema)
        {
            Dictionary<string, string> columns = new Dictionary<string, string>(Schema.Rows.Count);
            foreach (DataRow col in Schema.Rows)
            {
                string name = col.Field<string>("ColumnName");
                string key = name.Replace(" ", "").ToUpper();
                if (columns.ContainsKey(key)) continue;
                columns.Add(key, name);
            }
            return columns;
        }

        public static Dictionary<string, SqlDbType> ReadColumnsType(DataTable Schema)
        {
            Dictionary<string, SqlDbType> columns = new Dictionary<string, SqlDbType>(Schema.Rows.Count);
            foreach (DataRow col in Schema.Rows)
            {
                string name = col.Field<string>("ColumnName");
                string key = name.Replace(" ", "").ToUpper();
                columns.Add(key, (SqlDbType)col.Field<int>("ProviderType"));
            }
            return columns;
        }

		//This is just to get the property info using reflection.  In order to get the value
		//from a property dynamically, we need the property info from the class
		public static PropertyInfo[] GetInfo<K>(K item) where K : class
		{
			PropertyInfo[] propertyList;
			Type typeInfo;

			typeInfo = item.GetType();
			propertyList = typeInfo.GetProperties();

			return propertyList;
		}

		//This is the dynamic order by func that the OrderBy method needs to work
		public static IComparable OrderByProperty<T>(String propertyName, T item)
		  where T : class
		{
			PropertyInfo[] propertyList;

			propertyList = GetInfo(item);

			//Here we get the value of that property of the passed in item and make sure
			//to type the object (Which is what GetValue returns) into an IComparable
            PropertyInfo o = propertyList.DefaultIfEmpty(null).FirstOrDefault(currentProperty
			  => currentProperty.Name == propertyName);

            return o == null ? null : (IComparable)o.GetValue(item, null);
		}

		public static bool FindIndexProperty<T>(String propertyName, String propertyValue, T item)
			where T : class
		{
			PropertyInfo[] propertyList;

			propertyList = GetInfo(item);

			PropertyInfo o = propertyList.DefaultIfEmpty(null).FirstOrDefault(currentProperty
			  => currentProperty.Name == propertyName);

			if (o == null) return false;
			CultureInfo enLocale = new CultureInfo("en-US");
			if (o.PropertyType.FullName.Contains("DateTime"))
			{
				DateTime temp;
				if (DateTime.TryParseExact(propertyValue, "d/M/yyyy", enLocale, DateTimeStyles.None, out temp))
				{
					propertyValue = temp.ToString("d/M/yyyy", enLocale);
					return propertyValue == Convert.ToDateTime(o.GetValue(item, null)).ToString("d/M/yyyy", enLocale);
				}
			}
			else
			{
				return propertyValue == Convert.ToString(o.GetValue(item, null));
			}
			return false;
		}

		public static IEnumerable<DateTime> GetDateRange(DateTime startDate, DateTime endDate)
		{
			if (endDate < startDate)
				throw new ArgumentException("The end of 'date' and 'time' must be greater than or equal to start.");

			for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
				yield return day;
		}

        public static string GetAppSetting(Configuration c, string SettingName)
        {
            KeyValueConfigurationElement e = c.AppSettings.Settings[SettingName];
            if (e == null || string.IsNullOrEmpty(e.Value))
                return "";
            return e.Value;
        }

        public static string GetConnectionString(Configuration c, string ConnectionStringName)
        {
            ConnectionStringSettings e = c.ConnectionStrings.ConnectionStrings[ConnectionStringName];
            if (e == null || string.IsNullOrEmpty(e.ConnectionString))
                return "";
            return e.ConnectionString;
        }

		public static string GetConnectionString(string path, string ConnectionStringName)
		{
			Configuration c = null;
			string cstring = null;
			try
			{
				c = WebConfigurationManager.OpenWebConfiguration(path);
				cstring = GetConnectionString(c, ConnectionStringName);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(cstring))
			{
			  #if DEBUG
				string applicationName = Environment.GetCommandLineArgs()[0];
			  #else
                string applicationName = Environment.GetCommandLineArgs()[0]+ ".exe";
			  #endif

				c = ConfigurationManager.OpenExeConfiguration(applicationName);
				return GetConnectionString(c, ConnectionStringName);
			}
			return cstring;
		}

        /// <summary>
        /// ใช้ในการ Generate Person No จากชื่อ-สกุลภาษาไทยของพนักงาน
        /// </summary>
        /// <param name="DbServEncoding">ต้องเป็น Encoding ที่ตรงกับที่ระบุใน Regional Settings ของเครื่องที่เป็น Database Server โดยทั่วไปจะใช้ windows-874</param>
        /// <param name="THFirstName"></param>
        /// <param name="THLastName"></param>
        /// <returns></returns>
        public static string CreateHash(Encoding DbServEncoding, string THFirstName, string THLastName)
        {
            string Key = (THFirstName + "").Trim() + (THLastName + "").Trim();
            SHA256CryptoServiceProvider hash = new SHA256CryptoServiceProvider();
            string s = BitConverter.ToString(hash.ComputeHash(DbServEncoding.GetBytes(Key))).Replace("-", "");
            return s;
        }

        /// <summary>
        /// ใช้ในการ Validate URL Parameter ที่ส่งมาทาง HTTP GET
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
		public static string CreateHash(string plainText)
		{
			MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
			byte[] tmpSource;
			byte[] tmpHash;

			tmpSource = Encoding.UTF8.GetBytes(plainText); // Turn string into byte array
			tmpHash = md5.ComputeHash(tmpSource);

			StringBuilder sOutput = new StringBuilder(tmpHash.Length);
			// Loop through each byte of the hashed data  
			// and format each one as a hexadecimal string. 
			for (int i = 0; i < tmpHash.Length; i++)
			{
				sOutput.Append(tmpHash[i].ToString("X2"));  // X2 formats to hexadecimal
			}
			return sOutput.ToString();
		}

        public static string SerializeToXml(object value)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (StreamReader reader = new StreamReader(memoryStream))
                {
                    DataContractSerializer serializer = new DataContractSerializer(value.GetType());
                    serializer.WriteObject(memoryStream, value);
                    memoryStream.Position = 0;
                    return reader.ReadToEnd();
                }
            }
        }

        public static T DeserializeFromXml<T>(string xml)
        {
            Type t = typeof(T);
            t = Nullable.GetUnderlyingType(t) ?? t;

            using (Stream stream = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write(xml);
                    writer.Flush();
                    stream.Position = 0;
                    DataContractSerializer deserializer = new DataContractSerializer(t);
                    object value = deserializer.ReadObject(stream);
                    return (value == null) ? default(T) : (T)Convert.ChangeType(value, t);
                }
            }
        }

        public static bool TryGetPropertyValue<T>(object Instance, string PropertyName, out T Value)
        {
            Value = default(T);
            if (Instance == null) return false;

            Type t = typeof(T);
            t = Nullable.GetUnderlyingType(t) ?? t;

            object value = null;
            PropertyInfo p = Instance.GetType().GetProperty(PropertyName);
            if (p != null) value = p.GetValue(Instance, null);

            Value = (value == null) ? default(T) : (T)Convert.ChangeType(value, t);
            return (p != null);
        }

        public static string Encrypt(string str, string key, string vector)
        {
            //return str;
            RijndaelEnhanced aes = new RijndaelEnhanced(key, vector);
            return aes.Encrypt(str);
        }

        public static string Decrypt(string str, string key, string vector)
        {
            //return str;
            RijndaelEnhanced aes = new RijndaelEnhanced(key, vector);
            return aes.Decrypt(str);
        }

        public static string ConvertDaysToString(decimal days)
        {
            if (days == 0 || days == Const.QUOTA_UNLIMITED)
            {
                return "-";
            }
            decimal anhourMinutes = 60;
            decimal adayMinutes = anhourMinutes * Const.DEFAULT_WORKHOURS_OF_DAY;
            decimal totalMinutes = days * adayMinutes;
            int d = (int)Math.Floor(days);
            int h = (int)Math.Floor((totalMinutes % adayMinutes) / anhourMinutes);
            int m = (int)Math.Floor((totalMinutes % adayMinutes) % anhourMinutes);
            var s = string.Format("{0:0}:{1:00}:{2:00}", d, h, m);
            return s;
        }
        public static string ConvertHoursToString(decimal hours)
        {
            if (hours <= 0)
            {
                return "-";
            }
            decimal anhourMinutes = 60;
            decimal totalMinutes = hours * anhourMinutes;
            int h = (int)Math.Floor(totalMinutes / anhourMinutes);
            int m = (int)Math.Floor(totalMinutes % anhourMinutes);
            var s = string.Format("{0:0}:{1:00}", h, m);
            return s;
        }
    }
}
