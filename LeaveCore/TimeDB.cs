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
    public class TimeDB : BaseDB
    {
        public TimeDB(IPrincipal User)
            : base(User)
        {
			this.SetConnection();
        }

		public void SetConnection()
		{
			Configuration c = WebConfigurationManager.OpenWebConfiguration("~");
			ConnectionString = this.GetConnectionString(c);
			CommandTimeout = this.GetCommandTimeout(c);
		}

		protected string GetConnectionString(Configuration c)
		{
		    ConnectionStringSettings e = c.ConnectionStrings.ConnectionStrings["TIMEATT"];
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
    }
}
