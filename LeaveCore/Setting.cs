using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.Data.SqlClient;
using System.Security.Principal;
using System.Globalization;

namespace LeaveCore
{
	public class Setting : BaseDB
	{
		public Setting(IPrincipal User)
			: base(User)
		{
		}

		#region Holiday {Header}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <returns></returns>
		public static List<HolidaysTemplate> ListHolidays(IPrincipal User)
		{
			Setting obj = new Setting(User);
			List<HolidaysTemplate> list = new List<HolidaysTemplate>();
			using (SqlConnection conn = obj.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					#region Query
					cmd.CommandText =
						  "SELECT   [Holidays ID],[Company Code],[Description],[Active],[Modify Date],[Modify Person] "
						+ "FROM     [LV Holidays] WITH(READUNCOMMITTED) "
						//+ "WHERE    YEAR([Holiday Date]) BETWEEN YEAR(GETDATE()) AND YEAR(GETDATE()) + 1 "
						+ "ORDER BY [Description]";
					using (SqlDataReader rs = cmd.ExecuteReader())
					{
						int i;
						HolidaysTemplate rec;
						while (rs.Read())
						{
							rec = new HolidaysTemplate();
							i = rs.GetOrdinal("Holidays ID");
							rec.HolidaysID = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
							i = rs.GetOrdinal("Company Code");
							rec.CompanyCode = rs.IsDBNull(i) ? null : rs.GetString(i);
							i = rs.GetOrdinal("Description");
							rec.Description = rs.IsDBNull(i) ? null : rs.GetString(i);
							i = rs.GetOrdinal("Active");
							rec.Active = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
							i = rs.GetOrdinal("Modify Date");
							rec.ModifyDate = rs.IsDBNull(i) ? null : (DateTime?)rs.GetDateTime(i);
							i = rs.GetOrdinal("Modify Person");
							rec.ModifyPerson = rs.IsDBNull(i) ? null : rs.GetString(i);

							list.Add(rec);
						}
					}
					#endregion
				}
			}

			return list;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="Description"></param>
		/// <returns></returns>
		public static int AddHolidays(IPrincipal User, string Description)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText = "INSERT INTO [LV Holidays] "
						+ "			([Holidays ID], [Company Code], "
						+ "          [Description], [Active], [Modify Date], [Modify Person]) "
						+ "VALUES	((SELECT MAX([Holidays ID]) + 1 FROM [LV Holidays]), "
						+ "          @CompanyCode, @Description, 1, @ModifyDate, @ModifyPerson)";
					cmd.Parameters.Clear();
                    cmd.Parameters.Add("@CompanyCode", SqlDbType.VarChar).Value = string.Empty;
					cmd.Parameters.Add("@Description", SqlDbType.VarChar).Value = Description;
                    cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
					result = cmd.ExecuteNonQuery();
				}
			}
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="HolidaysID"></param>
		/// <param name="Description"></param>
		/// <returns></returns>
		public static int EditHolidays(IPrincipal User, int HolidaysID, string Description)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText = "UPDATE [LV Holidays] SET "
						+ " [Description]=@Description "
						+ ",[Modify Date]=@ModifyDate "
						+ ",[Modify Person]=@ModifyPerson "
						+ "WHERE [Holidays ID]=@HolidaysID";
					cmd.Parameters.Clear();
					cmd.Parameters.Add("@HolidaysID", SqlDbType.Int).Value = HolidaysID;
					cmd.Parameters.Add("@Description", SqlDbType.VarChar).Value = Description;
                    cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
					result = cmd.ExecuteNonQuery();
				}
			}
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="HolidaysID"></param>
		/// <returns></returns>
		public static int DeleteHolidays(IPrincipal User, int HolidaysID)
		{
			int result;
			BaseDB db = new BaseDB(User);
			DeleteHoliday(User, HolidaysID, 0);
			using (SqlConnection conn = db.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText = "DELETE FROM [LV Holidays] WHERE [Holidays ID]=@HolidaysID";
					cmd.Parameters.Clear();
					cmd.Parameters.Add("@HolidaysID", SqlDbType.Int).Value = HolidaysID;
					result = cmd.ExecuteNonQuery();
				}
			}
			return result;
		}
		#endregion

		#region Holiday {Lines}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="HolidaysID"></param>
		/// <returns></returns>
		public static List<HolidayTemplate> ListHoliday(IPrincipal User, int? Year, int HolidaysID)
		{
			Setting obj = new Setting(User);
			List<HolidayTemplate> list = new List<HolidayTemplate>();
			using (SqlConnection conn = obj.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					#region Query
					string WhereBy = "";
					List<string> filters = new List<string>();
					if(Year.HasValue) filters.Add("YEAR(line.[Holiday Date])="+Year);
					if(HolidaysID != 0) filters.Add("head.[Holidays ID]="+HolidaysID);
					if (filters != null && filters.Count > 0)
						WhereBy = string.Join(" AND ", filters);
					else WhereBy = "1<>1";
					cmd.CommandText =
						  "SELECT   head.[Holidays ID], head.[Company Code], head.[Description], "
						+ "         line.[Holiday ID], line.[Holiday Date], line.[TH Name], line.[EN Name] "
						+ "FROM     [LV Holiday] line WITH(READUNCOMMITTED) "
						+ "         INNER JOIN [LV Holidays] head WITH(READUNCOMMITTED) ON line.[Holidays ID]=head.[Holidays ID] "
						+ "WHERE    " + WhereBy + " "
						+ "ORDER BY line.[Holiday Date]";
					using (SqlDataReader rs = cmd.ExecuteReader())
					{
						int i;
						HolidayTemplate rec;
						while (rs.Read())
						{
							rec = new HolidayTemplate();
							i = rs.GetOrdinal("Holiday ID");
							rec.HolidayID = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
							i = rs.GetOrdinal("Holiday Date");
							rec.Date = rs.IsDBNull(i) ? null : (DateTime?)rs.GetDateTime(i);
							i = rs.GetOrdinal("TH Name");
							rec.NameTH = rs.IsDBNull(i) ? null : rs.GetString(i);
							i = rs.GetOrdinal("EN Name");
							rec.NameEN = rs.IsDBNull(i) ? null : rs.GetString(i);

							rec.Of = new HolidaysTemplate();
							i = rs.GetOrdinal("Holidays ID");
							rec.Of.HolidaysID = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
							i = rs.GetOrdinal("Company Code");
							rec.Of.CompanyCode = rs.IsDBNull(i) ? null : rs.GetString(i);
							i = rs.GetOrdinal("Description");
							rec.Of.Description = rs.IsDBNull(i) ? null : rs.GetString(i);
							//CultureInfo thCulture = new CultureInfo("en-US");
							//rec.Date = rs.IsDBNull(i) ? null : (DateTime?)DateTime.Parse("2013-01-01 ", thCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal);

							list.Add(rec);
						}
					}
					#endregion
				}
			}

			return list;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="HolidaysID"></param>
		/// <param name="Date"></param>
		/// <param name="NameTH"></param>
		/// <param name="NameEN"></param>
		/// <returns></returns>
		public static int AddHoliday(IPrincipal User, int HolidaysID, DateTime Date, string NameTH, string NameEN)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "INSERT INTO [LV Holiday] "
			            + "			([Holiday ID], [Holidays ID], [Holiday Date], "
						+ "			 [TH Name], [EN Name], [Modify Date], [Modify Person]) "
			            + "VALUES	((SELECT MAX([Holiday ID]) + 1 FROM [LV Holiday]), "
						+ "			 @HolidaysID, @Date, @NameTH, @NameEN, @ModifyDate, @ModifyPerson)";
			        cmd.Parameters.Clear();
					cmd.Parameters.Add("@HolidaysID", SqlDbType.Int).Value = HolidaysID;
					cmd.Parameters.Add("@Date", SqlDbType.DateTime).Value = Date.Date;
					cmd.Parameters.Add("@NameTH", SqlDbType.VarChar).Value = NameTH;
					cmd.Parameters.Add("@NameEN", SqlDbType.VarChar).Value = NameEN ?? string.Empty;
                    cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="HolidayID"></param>
		/// <param name="Date"></param>
		/// <param name="NameTH"></param>
		/// <param name="NameEN"></param>
		/// <returns></returns>
		public static int EditHoliday(IPrincipal User, int HolidayID, DateTime Date, string NameTH, string NameEN)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "UPDATE [LV Holiday] SET "
			            + " [Holiday Date]=@Date "
						+ ",[TH Name]=@NameTH "
						+ ",[EN Name]=@NameEN "
						+ ",[Modify Date]=@ModifyDate "
						+ ",[Modify Person]=@ModifyPerson "
			            + "WHERE [Holiday ID]=@HolidayID";
					cmd.Parameters.Clear();
			        cmd.Parameters.Add("@HolidayID", SqlDbType.Int).Value = HolidayID;
					cmd.Parameters.Add("@Date", SqlDbType.DateTime).Value = Date.Date;
					cmd.Parameters.Add("@NameTH", SqlDbType.VarChar).Value = NameTH;
					cmd.Parameters.Add("@NameEN", SqlDbType.VarChar).Value = NameEN ?? string.Empty;
                    cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="HolidaysID"></param>
		/// <param name="HolidayID"></param>
		/// <returns></returns>
		public static int DeleteHoliday(IPrincipal User, int HolidaysID, int HolidayID)
		{
			int result;
			BaseDB db = new BaseDB(User);
			List<SqlParameter> Params = new List<SqlParameter>();
            Params.Add(new SqlParameter("@headid", SqlDbType.BigInt));
            Params[0].Value = HolidaysID;
            Params.Add(new SqlParameter("@lineid", SqlDbType.BigInt));
            Params[1].Value = HolidayID;
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        string WhereHeadID = "";
			        string WhereLineID = "";
					if (HolidayID != 0) WhereLineID = " AND [Holiday ID]=@lineid ";
					if (HolidaysID != 0) WhereHeadID = " AND [Holidays ID]=@headid ";
					cmd.CommandText = "DELETE FROM [LV Holiday] WHERE 1=1 " + WhereHeadID + WhereLineID;
			        cmd.Parameters.Clear();
                    if (Params != null && Params.Count > 0)
                        cmd.Parameters.AddRange(Params.ToArray());
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}
		#endregion

		#region Workshift {Header}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <returns></returns>
		public static List<WorkshiftsTemplate> ListWorkshifts(IPrincipal User)
		{
			Setting obj = new Setting(User);
			List<WorkshiftsTemplate> list = new List<WorkshiftsTemplate>();
			using (SqlConnection conn = obj.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					#region Query
					cmd.CommandText =
						  "SELECT   [Workshifts ID],[Office Staff],[Description],[Active],[Modify Date],[Modify Person] "
						+ "FROM     [LV Workshifts] WITH(READUNCOMMITTED) "
						+ "WHERE    [Active] = 1 "
						+ "ORDER BY [Office Staff] DESC, [Description]";
					using (SqlDataReader rs = cmd.ExecuteReader())
					{
						int i;
						WorkshiftsTemplate rec;
						while (rs.Read())
						{
							rec = new WorkshiftsTemplate();
							i = rs.GetOrdinal("Workshifts ID");
							rec.WorkshiftsID = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
							i = rs.GetOrdinal("Office Staff");
							rec.OfficeStaff = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
							i = rs.GetOrdinal("Description");
							rec.Description = rs.IsDBNull(i) ? null : rs.GetString(i);
							i = rs.GetOrdinal("Active");
							rec.Active = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
							i = rs.GetOrdinal("Modify Date");
							rec.ModifyDate = rs.IsDBNull(i) ? null : (DateTime?)rs.GetDateTime(i);
							i = rs.GetOrdinal("Modify Person");
							rec.ModifyPerson = rs.IsDBNull(i) ? null : rs.GetString(i);

							list.Add(rec);
						}
					}
					#endregion
				}
			}

			return list;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="OfficeStaff"></param>
		/// <param name="Description"></param>
		/// <returns></returns>
		public static int AddWorkshifts(IPrincipal User, int OfficeStaff, string Description)
		{
			int result;
			BaseDB db = new BaseDB(User);
			Int32 Id = db.ExecuteScalar<Int32>("select max([Workshifts ID]) + 1 from [LV Workshifts]", null);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "INSERT INTO	[LV Workshifts] "
			            + "			([Workshifts ID], [Office Staff], "
						+ "          [Description], [Active], [Modify Date], [Modify Person]) "
			            + "VALUES	(@WorkshiftsID, @OfficeStaff, @Description, 1, @ModifyDate, @ModifyPerson)";
			        cmd.Parameters.Clear();
					cmd.Parameters.Add("@WorkshiftsID", SqlDbType.Int).Value = Id;
					cmd.Parameters.Add("@OfficeStaff", SqlDbType.Int).Value = OfficeStaff;
					cmd.Parameters.Add("@Description", SqlDbType.VarChar).Value = Description;
                    cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			if (result > 0) AddWorkshift(User, Id);
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="WorkshiftsID"></param>
		/// <param name="OfficeStaff"></param>
		/// <param name="Description"></param>
		/// <returns></returns>
		public static int EditWorkshifts(IPrincipal User, int WorkshiftsID, int OfficeStaff, string Description)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "UPDATE [LV Workshifts] SET "
			            + " [Office Staff]=@OfficeStaff "
			            + ",[Description]=@Description "
						+ ",[Modify Date]=@ModifyDate "
						+ ",[Modify Person]=@ModifyPerson "
			            + "WHERE [Workshifts ID]=@WorkshiftsID";
					cmd.Parameters.Clear();
			        cmd.Parameters.Add("@WorkshiftsID", SqlDbType.Int).Value = WorkshiftsID;
					cmd.Parameters.Add("@OfficeStaff", SqlDbType.Int).Value = OfficeStaff;
					cmd.Parameters.Add("@Description", SqlDbType.VarChar).Value = Description;
                    cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="WorkshiftsID"></param>
		/// <returns></returns>
		public static int DeleteWorkshifts(IPrincipal User, int WorkshiftsID)
		{
			int result;
			BaseDB db = new BaseDB(User);
			DeleteWorkshift(User, WorkshiftsID, 0);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "DELETE FROM [LV Workshifts] WHERE [Workshifts ID]=@WorkshiftsID";
			        cmd.Parameters.Clear();
					cmd.Parameters.Add("@WorkshiftsID", SqlDbType.Int).Value = WorkshiftsID;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}
		#endregion

		#region Workshift {Lines}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="WorkshiftsID"></param>
		/// <returns></returns>
		public static List<WorkshiftTemplate> ListWorkshift(IPrincipal User, int WorkshiftsID)
		{
			Setting obj = new Setting(User);
			List<WorkshiftTemplate> list = new List<WorkshiftTemplate>();
			using (SqlConnection conn = obj.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					#region Query
					cmd.CommandText =
						  "SELECT   head.[Workshifts ID], head.[Office Staff], head.[Description], "
						+ "         line.[Workshift ID], line.[Time Begin], line.[Time Until], line.[Type], "
						+ "			line.[Description] AS Description_ "
						+ "FROM     [LV Workshift] line WITH(READUNCOMMITTED) "
						+ "         INNER JOIN [LV Workshifts] head WITH(READUNCOMMITTED) ON line.[Workshifts ID]=head.[Workshifts ID] "
						+ "WHERE    head.[Workshifts ID]=@WorkshiftsID "
						+ "ORDER BY line.[Type], line.[Time Begin], line.[Time Until]";
					cmd.Parameters.Clear();
					cmd.Parameters.Add("@WorkshiftsID", SqlDbType.Int).Value = (object)WorkshiftsID;
					using (SqlDataReader rs = cmd.ExecuteReader())
					{
						int i;
						WorkshiftTemplate rec;
						while (rs.Read())
						{
							rec = new WorkshiftTemplate();
							i = rs.GetOrdinal("Workshift ID");
							rec.WorkshiftID = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
							i = rs.GetOrdinal("Time Begin");
							rec.TimeBegin = rs.IsDBNull(i) ? null : (DateTime?)rs.GetDateTime(i);
							i = rs.GetOrdinal("Time Until");
							rec.TimeUntil = rs.IsDBNull(i) ? null : (DateTime?)rs.GetDateTime(i);
							i = rs.GetOrdinal("Type");
							rec.Type = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
							i = rs.GetOrdinal("Description_");
							rec.Description = rs.IsDBNull(i) ? null : rs.GetString(i);

							rec.Of = new WorkshiftsTemplate();
							i = rs.GetOrdinal("Workshifts ID");
							rec.Of.WorkshiftsID = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
							i = rs.GetOrdinal("Office Staff");
							rec.Of.OfficeStaff = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
							i = rs.GetOrdinal("Description");
							rec.Of.Description = rs.IsDBNull(i) ? null : rs.GetString(i);

							list.Add(rec);
						}
					}
					#endregion
				}
			}

			return list;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="WorkshiftsID"></param>
		/// <returns></returns>
		public static int AddWorkshift(IPrincipal User, int WorkshiftsID)
		{
			int result = 0;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
					foreach (var rec in InitWorkshift)
					{
						cmd.CommandText = "INSERT INTO	[LV Workshift] "
							+ "			([Workshift ID], [Workshifts ID], [Type], "
							+ "			 [Time Begin], [Time Until], [Description], [Modify Date], [Modify Person]) "
							+ "VALUES	((SELECT MAX([Workshift ID]) + 1 FROM [LV Workshift]), "
							+ "			 @WorkshiftsID, @Type, @TimeBegin, @TimeUntil, "
							+ "			 @Description, @ModifyDate, @ModifyPerson)";
						cmd.Parameters.Clear();
						cmd.Parameters.Add("@WorkshiftsID", SqlDbType.Int).Value = WorkshiftsID;
						cmd.Parameters.Add("@Type", SqlDbType.Int).Value = rec.Type;
						cmd.Parameters.Add("@TimeBegin", SqlDbType.DateTime).Value = rec.TimeBegin;
						cmd.Parameters.Add("@TimeUntil", SqlDbType.DateTime).Value = rec.TimeUntil;
						cmd.Parameters.Add("@Description", SqlDbType.VarChar).Value = rec.Description;
						cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
						cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
						result += cmd.ExecuteNonQuery();
					}
			    }
			}
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="WorkshiftID"></param>
		/// <param name="TimeBegin"></param>
		/// <param name="TimeUntil"></param>
		/// <returns></returns>
		public static int EditWorkshift(IPrincipal User, int WorkshiftID, TimeSpan TimeBegin, TimeSpan TimeUntil)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "UPDATE [LV Workshift] SET "
			            + " [Time Begin]=@TimeBegin "
						+ ",[Time Until]=@TimeUntil "
						+ ",[Modify Date]=@ModifyDate "
						+ ",[Modify Person]=@ModifyPerson "
			            + "WHERE [Workshift ID]=@WorkshiftID";
					cmd.Parameters.Clear();
			        cmd.Parameters.Add("@WorkshiftID", SqlDbType.Int).Value = WorkshiftID;
			        cmd.Parameters.Add("@TimeBegin", SqlDbType.DateTime).Value = 
						new DateTime(1900, 1, 1, TimeBegin.Hours, TimeBegin.Minutes, TimeBegin.Seconds);
			        cmd.Parameters.Add("@TimeUntil", SqlDbType.DateTime).Value = 
						new DateTime(1900, 1, 1, TimeUntil.Hours, TimeUntil.Minutes, TimeUntil.Seconds);
                    cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="WorkshiftsID"></param>
		/// <param name="WorkshiftID"></param>
		/// <returns></returns>
		public static int DeleteWorkshift(IPrincipal User, int WorkshiftsID, int WorkshiftID)
		{
			int result;
			BaseDB db = new BaseDB(User);
			List<SqlParameter> Params = new List<SqlParameter>();
            Params.Add(new SqlParameter("@headid", SqlDbType.BigInt));
            Params[0].Value = WorkshiftsID;
            Params.Add(new SqlParameter("@lineid", SqlDbType.BigInt));
            Params[1].Value = WorkshiftID;
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        string WhereHeadID = "";
			        string WhereLineID = "";
					if (WorkshiftID != 0) WhereLineID = " AND [Workshift ID]=@lineid ";
					if (WorkshiftsID != 0) WhereHeadID = " AND [Workshifts ID]=@headid ";
					cmd.CommandText = "DELETE FROM [LV Workshift] WHERE 1=1 " + WhereHeadID + WhereLineID;
					cmd.Parameters.Clear();
                    if (Params != null && Params.Count > 0)
                        cmd.Parameters.AddRange(Params.ToArray());
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}
		#endregion

		public class HolidayTemplate
		{
			public int HolidayID { get; set; }
			public DateTime? Date { get; set; }
			public string NameTH { get; set; }
			public string NameEN { get; set; }
			public DateTime? ModifyDate { get; set; }
			public string ModifyPerson { get; set; }
			public HolidaysTemplate Of { get; set; }
		}
		public class HolidaysTemplate
		{
			public int HolidaysID { get; set; }
			public string CompanyCode { get; set; }
			public string Description { get; set; }
			public int Active { get; set; }
			public DateTime? ModifyDate { get; set; }
			public string ModifyPerson { get; set; }
		}
		public class WorkshiftTemplate
		{
			public int WorkshiftID { get; set; }
			public DateTime? TimeBegin { get; set; }
			public DateTime? TimeUntil { get; set; }
			public int Type { get; set; }
			public string Description { get; set; }
			public DateTime? ModifyDate { get; set; }
			public string ModifyPerson { get; set; }
			public WorkshiftsTemplate Of { get; set; }
		}
		public class WorkshiftsTemplate
		{
			public int WorkshiftsID { get; set; }
			public int OfficeStaff { get; set; }
			public int Active { get; set; }
			public string Description { get; set; }
			public DateTime? ModifyDate { get; set; }
			public string ModifyPerson { get; set; }
		}
		public static List<WorkshiftTemplate> InitWorkshift
		{
			get
			{
				return new List<WorkshiftTemplate>()
				{
					new WorkshiftTemplate()
					{
						Type = 1,
						Description = "Morning",
						TimeBegin = new DateTime(1900, 1, 1, 0, 0, 0),
						TimeUntil = new DateTime(1900, 1, 1, 0, 0, 0)
					},
					new WorkshiftTemplate()
					{
						Type = 2,
						Description = "Afternoon",
						TimeBegin = new DateTime(1900, 1, 1, 0, 0, 0),
						TimeUntil = new DateTime(1900, 1, 1, 0, 0, 0)
					}
				};
			}
		}

	}
}