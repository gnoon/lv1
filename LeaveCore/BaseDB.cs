using System.Configuration;
using System.Web.Configuration;
using System.Data.SqlClient;
using System.Reflection;
using System;
using System.IO;
using System.Threading;
using System.Globalization;
using System.Security.Principal;

namespace LeaveCore
{
    public class BaseDB : IdentifiableObject
    {
		public static readonly string DefaultUILocale = "en";
		public static readonly string DefaultUILanguage = "th";
		protected IPrincipal User;

        //public BaseDB() : this(null)
        public BaseDB(IPrincipal User)
            : this("~")
        {
            this.User = User;
			InitialLocalization();
        }

        public BaseDB(string configPath)
        {
			Configuration c = null;
			string cstring = null;
			try
			{
				c = WebConfigurationManager.OpenWebConfiguration(configPath);
				cstring = this.GetConnectionString(c);
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
                this.ConnectionString = this.GetConnectionString(c);
            }
            else
            {
                this.ConnectionString = cstring;
            }
            this.CommandTimeout = this.GetCommandTimeout(c);
        }

        public BaseDB(string strConn, int timeout)
        {
            this.ConnectionString = strConn;
            this.CommandTimeout = timeout;
        }

        protected string GetConnectionString(Configuration c)
        {
            ConnectionStringSettings e = c.ConnectionStrings.ConnectionStrings["LEAVE"];
            if (e == null || string.IsNullOrEmpty(e.ConnectionString))
                return "";
            return e.ConnectionString;
        }

        protected int GetCommandTimeout(Configuration c)
        {
            KeyValueConfigurationElement e = c.AppSettings.Settings["dbCommandTimeout"];
            if (e == null || string.IsNullOrEmpty(e.Value))
                return 30;
            try
            {
                return int.Parse(e.Value);
            }
            catch
            {
                return 30;
            }
        }

        public string ConnectionString
        {
            get;
            set;
        }

        public int CommandTimeout
        {
            get;
            set;
        }

        public SqlConnection GetConnection()
        {
            SqlConnection objConn = new SqlConnection(this.ConnectionString);
            return objConn;
        }

		protected string thLocale
		{
			get
			{
				Configuration c = null;
				KeyValueConfigurationElement e = null;
				try
				{
					c = WebConfigurationManager.OpenWebConfiguration("~");
					e = c.AppSettings.Settings["thLocale"];
				}
				catch
				{
				}
				if (e == null || string.IsNullOrEmpty(e.Value))
					return "th-TH";
				return e.Value;
			}
		}

		protected string enLocale
		{
			get
			{
				Configuration c = null;
				KeyValueConfigurationElement e = null;
				try
				{
					c = WebConfigurationManager.OpenWebConfiguration("~");
					e = c.AppSettings.Settings["enLocale"];
				}
				catch
				{
				}
				if (e == null || string.IsNullOrEmpty(e.Value))
					return "en-US";
				return e.Value;
			}
		}

		protected string DefaultLocale
		{
			get
			{
				if (this.enLocale.Contains(DefaultUILocale))
					return this.enLocale;
				return this.thLocale;
			}
		}

		private void InitialLocalization()
		{
			CultureInfo c = new CultureInfo(DefaultLocale);
			Thread.CurrentThread.CurrentCulture = c;
			Thread.CurrentThread.CurrentUICulture = c;
		}

        /// <summary>
        /// Execute Query
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Query"></param>
        /// <param name="Conn">null หากต้องการเปิด Connection ใหม่</param>
        /// <returns></returns>
        public T ExecuteScalar<T>(string Query, SqlConnection Conn)
        {
            bool InternalClose = Conn == null || Conn.State != System.Data.ConnectionState.Open;
            if (InternalClose)
            {
                Conn = GetConnection();
                Conn.Open();
            }
            try
            {
                using (SqlCommand cmd = Conn.CreateCommand())
                {
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandText = Query;
                    object value = cmd.ExecuteScalar();

                    Type t = typeof(T);
                    t = Nullable.GetUnderlyingType(t) ?? t;

                    return (value == null || DBNull.Value.Equals(value)) ?
                        default(T) : (T)Convert.ChangeType(value, t);
                }
            }
            finally
            {
                if (InternalClose)
                    Conn.Close();
            }
        }
    }
}
