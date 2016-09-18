using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Security.Principal;
using System.Configuration;
using System.Web.Configuration;

namespace LeaveCore
{
    public class Unique
    {
		private static string path = "~";
		private static string ConnectionStringName = "LEAVE";
		public static readonly object Ticket = new object();

		public static Int64 GetLastID(string table, string field)
		{
			Int64? LastID = null;
			using (SqlConnection conn = new SqlConnection(Tool.GetConnectionString(path, ConnectionStringName)))
			{
				conn.Open();
				lock (Ticket)
				{
					using (SqlCommand cmd = conn.CreateCommand())
					{
						cmd.CommandText = "UPDATE [LV Unique] SET [Last ID] = [Last ID] + 1 WHERE [Table]=@Table AND [Field]=@Field;SELECT [Last ID] FROM [LV Unique] with(readuncommitted) WHERE [Table]=@Table AND [Field]=@Field";
						cmd.Parameters.Add("@Table", SqlDbType.VarChar).Value = table == null ? DBNull.Value : (object)table;
						cmd.Parameters.Add("@Field", SqlDbType.VarChar).Value = table == null ? DBNull.Value : (object)field;
						object val = cmd.ExecuteScalar();
						if (val is DBNull) val = null;
						if(val == null)
						{
							LastID = 1;
							SetLastID(table, field, (Int64)LastID);
						}
						else LastID = (Int64?)val;
					}
				}
			}
			return (Int64)LastID;
		}

		private static void SetLastID(string table, string field, Int64 LastID)
        {
			using (SqlConnection conn = new SqlConnection(Tool.GetConnectionString(path, ConnectionStringName)))
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText =
						  "INSERT INTO [LV Unique] ([Table], [Field], [Last ID], [Update Date]) "
						+ "VALUES(@Table, @Field, @LastID, @UpdateDate)";
					cmd.Parameters.Add("@Table", SqlDbType.VarChar).Value = table;
					cmd.Parameters.Add("@Field", SqlDbType.VarChar).Value = field;
					cmd.Parameters.Add("@LastID", SqlDbType.BigInt).Value = LastID;
					cmd.Parameters.Add("@UpdateDate", SqlDbType.DateTime).Value = DateTime.Now;
					cmd.ExecuteNonQuery();
				}
			}
		}
    }
}
