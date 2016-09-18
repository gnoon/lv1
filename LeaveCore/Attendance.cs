using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Principal;
using System.Data.SqlClient;
using System.Data;
using System.Globalization;

namespace LeaveCore
{
	public class Attendance : TimeDB
	{
		public Attendance(IPrincipal User)
			: base(User)
		{
		}

		/// <summary>
		/// ดึงข้อมูลสรุปเวลาทำงานแบบ Year to Date
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <param name="Year"></param>
		/// <returns></returns>
		public static List<AttDashBoardRecord> ListDashBoard(IPrincipal User, string PersonNo, int Year)
		{
			Attendance obj = new Attendance(User);
			List<AttDashBoardRecord> list = new List<AttDashBoardRecord>();
			using (SqlConnection conn = obj.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText = 
						  "SELECT	[Person No], MONTH([Work Date]) AS [Month], "
						+ "			SUM([Leave Sick]) AS [Leave Sick], SUM([Leave Business]) AS [Leave Business], "
						+ "			SUM([Leave Vocation]) AS [Leave Vocation], SUM([Leave Other]) AS [Leave Other], "
						+ "			SUM([Late Minutes]) AS [Late Minutes], SUM(CASE WHEN [Late Minutes] > 0 THEN 1 ELSE 0 END) AS [Late Count] "
						+ "FROM		[TM Worktime] WITH(READUNCOMMITTED) "
						+ "WHERE	YEAR([Work Date]) = @year AND [Person No] = @perno "
						+ "GROUP BY [Person No], MONTH([Work Date]) "
						+ "ORDER BY	[Person No], MONTH([Work Date])";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@year", SqlDbType.Int).Value = Year;
			        cmd.Parameters.Add("@perno", SqlDbType.VarChar).Value = PersonNo;
					using (SqlDataReader rs = cmd.ExecuteReader())
					{
						AttDashBoardRecord rec;
						while (rs.Read())
						{
							rec = new AttDashBoardRecord();
							rec.Month = rs.GetValue<int>("Month");
							rec.PersonNo = rs.GetValue<string>("Person No"); 
							rec.LeaveSick = rs.GetValue<decimal>("Leave Sick"); 
							rec.LeaveBusiness = rs.GetValue<decimal>("Leave Business"); 
							rec.LeaveVocation = rs.GetValue<decimal>("Leave Vocation"); 
							rec.LeaveOther = rs.GetValue<decimal>("Leave Other"); 
							rec.LateMinutes = rs.GetValue<decimal>("Late Minutes"); 
							rec.LateCount = rs.GetValue<int>("Late Count");

							list.Add(rec);
						}
					}
				}
			}
			return list;
		}

		/// <summary>
		/// ดึงข้อมูลเวลาทำงานรายวัน
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <param name="Month"></param>
		/// <returns></returns>
		public static List<WorktimeRecord> ListDaily(IPrincipal User, string PersonNo, int Year, int Month)
		{
			Attendance obj = new Attendance(User);
			List<WorktimeRecord> list = new List<WorktimeRecord>();
			using (SqlConnection conn = obj.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText = 
						  "SELECT	[Person No], [Work Date], [Is Working Day], [Work Minutes], "
						+ "			[Regular Minutes], [Leave Sick], [Leave Business], [Leave Vocation], [Leave Other], "
						+ "			[WShift Morning In], [WShift Morning Out], [WShift Noon In], [WShift Noon Out], "
						+ "			[FScan In], [FScan Out], [Late Minutes], [Absence Minutes] "
						+ "FROM		[TM Worktime] WITH(READUNCOMMITTED) "
						+ "WHERE	YEAR([Work Date]) = @year AND MONTH([Work Date]) = @month AND [Person No] = @perno "
						+ "ORDER BY	[Person No], [Work Date]";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@year", SqlDbType.Int).Value = Year;
			        cmd.Parameters.Add("@month", SqlDbType.Int).Value = Month;
			        cmd.Parameters.Add("@perno", SqlDbType.VarChar).Value = PersonNo;
					using (SqlDataReader rs = cmd.ExecuteReader())
					{
						WorktimeRecord rec;
						while (rs.Read())
						{
							rec = new WorktimeRecord();
							rec.PersonNo = rs.GetValue<string>("Person No"); 
							rec.WorkDate = rs.GetValue<DateTime?>("Work Date");
							rec.IsWorkingDay = rs.GetValue<string>("Is Working Day");
							rec.WorkMinutes = rs.GetValue<decimal>("Work Minutes");
							rec.RegularMinutes = rs.GetValue<decimal>("Regular Minutes");
							rec.WorkMinutes = rs.GetValue<decimal>("Work Minutes");
							rec.LeaveSick = rs.GetValue<decimal>("Leave Sick"); 
							rec.LeaveBusiness = rs.GetValue<decimal>("Leave Business"); 
							rec.LeaveVocation = rs.GetValue<decimal>("Leave Vocation"); 
							rec.LeaveOther = rs.GetValue<decimal>("Leave Other"); 
							rec.MorningIn = rs.GetValue<DateTime?>("WShift Morning In");
							rec.MorningOut = rs.GetValue<DateTime?>("WShift Morning Out");
							rec.AfternoonIn = rs.GetValue<DateTime?>("WShift Noon In");
							rec.AfternoonOut = rs.GetValue<DateTime?>("WShift Noon Out");
							rec.FScanIn = rs.GetValue<DateTime?>("FScan In");
							rec.FScanOut = rs.GetValue<DateTime?>("FScan Out");
							rec.LateMinutes = rs.GetValue<decimal>("Late Minutes"); 
							rec.AbsenceMinutes = rs.GetValue<decimal>("Absence Minutes"); 

							list.Add(rec);
						}
					}
				}
			}
			return list;
		}

		/// <summary>
		/// ดึง Log ของการคำนวณสรุปเวลาทำงาน
		/// </summary>
		/// <param name="User"></param>
		/// <returns></returns>
		public static List<LogConsoRecord> ListLogConso(IPrincipal User)
		{
			Attendance obj = new Attendance(User);
			List<LogConsoRecord> list = new List<LogConsoRecord>();
			using (SqlConnection conn = obj.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText = 
						  "SELECT	log.[Log ID],log.[Begin Date],log.[Until Date],[Locked], "
						+ "			log.[Total Record],log.[Progress Record],log.[Log Time],log.[Log Person], "
						+ "			per.[TH Prefix],per.[TH First Name],per.[TH Last Name] "
						+ "FROM		[TM Log CONSO] AS log WITH(READUNCOMMITTED) LEFT OUTER JOIN "
						+ "			[LEAVE].[dbo].[HR Person] AS per WITH(READUNCOMMITTED) ON log.[Log Person]=per.[Person No] "
						+ "ORDER BY	log.[Log Time] DESC";
					using (SqlDataReader rs = cmd.ExecuteReader())
					{
						LogConsoRecord rec;
						while (rs.Read())
						{
							rec = new LogConsoRecord();
							rec.LogID = rs.GetValue<Int64>("Log ID");
							rec.BeginDate = rs.GetValue<DateTime?>("Begin Date");
							rec.UntilDate = rs.GetValue<DateTime?>("Until Date");
							rec.TotalRecord = rs.GetValue<int>("Total Record");
							rec.ProgressRecord = rs.GetValue<int>("Progress Record");
							rec.LogTime = rs.GetValue<DateTime?>("Log Time");
							//rec.LogPerson = rs.GetValue<string>("Log Person");
							rec.Locked = rs.GetValue<int>("Locked");
							rec.Person = new PersonRecord();
							rec.Person.PrefixTH = rs.GetValue<string>("TH Prefix");
							rec.Person.NameFirstTH = rs.GetValue<string>("TH First Name");
							rec.Person.NameLastTH = rs.GetValue<string>("TH Last Name");

							list.Add(rec);
						}
					}
				}
			}
			return list;
		}

		/// <summary>
		/// Add Log ของการคำนวณสรุปเวลาทำงาน
		/// </summary>
		/// <param name="User"></param>
		/// <param name="BeginDate"></param>
		/// <param name="UntilDate"></param>
		/// <returns></returns>
		public static int AddLogConso(IPrincipal User, Int64 LogID, DateTime BeginDate, DateTime UntilDate)
		{
			Attendance db = new Attendance(User);
			//int t = db.ExecuteScalar<int>(string.Format(
			//    "SELECT COUNT(*) FROM [TM GET Summary] WITH(READUNCOMMITTED) " +
			//    "WHERE CONVERT(VARCHAR(8),[Check Date],112) BETWEEN '{0}' AND '{1}'"
			//    ,BeginDate.Date.ToString("yyyyMMdd", new CultureInfo("en-US"))
			//    ,UntilDate.Date.ToString("yyyyMMdd", new CultureInfo("en-US"))), null);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "INSERT INTO [TM Log CONSO] " +
			            "        ([Log ID], [Begin Date], [Until Date], [Total Record], " +
			            "         [Progress Record], [Error], [Log Time], [Log Person], [Locked]) " +
			            "VALUES  (@id, @begin, @until, @total, @progress, @error, @time, @person, 0)";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = LogID;
			        cmd.Parameters.Add("@begin", SqlDbType.DateTime).Value = BeginDate.Date;
			        cmd.Parameters.Add("@until", SqlDbType.DateTime).Value = UntilDate.Date;
			        cmd.Parameters.Add("@total", SqlDbType.Int).Value = 0;
			        cmd.Parameters.Add("@progress", SqlDbType.Int).Value = 0;
			        cmd.Parameters.Add("@error", SqlDbType.VarChar).Value = string.Empty;
			        cmd.Parameters.Add("@time", SqlDbType.DateTime).Value = DateTime.Now;
			        cmd.Parameters.Add("@person", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        return cmd.ExecuteNonQuery();
			    }
			}
		}

		/// <summary>
		/// ล็อค record
		/// (การล็อค record เป็นการป้องกันการคำนวณซ้ำ หรือใช้เป็นตัวบอกว่าได้ complete ช่วงข้อมูลนั้นแล้ว)
		/// </summary>
		/// <param name="User"></param>
		/// <param name="LogID"></param>
		/// <returns></returns>
		public static int LockData(IPrincipal User, Int64 LogID)
		{
			Attendance db = new Attendance(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "UPDATE [TM Log CONSO] SET [Locked]=1, [Log Time]=@time, [Log Person]=@person WHERE [Log ID]=@id";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = LogID;
			        cmd.Parameters.Add("@time", SqlDbType.DateTime).Value = DateTime.Now;
			        cmd.Parameters.Add("@person", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        return cmd.ExecuteNonQuery();
			    }
			}
		}

		/// <summary>
		/// ตรวจสอบช่วงวันที่ ที่ทำการคำนวณว่ามีการถูกล็อคอยู่หรือไม่
		/// </summary>
		/// <param name="User"></param>
		/// <param name="BeginDate"></param>
		/// <param name="UntilDate"></param>
		/// <returns></returns>
		public static bool Locked(IPrincipal User, DateTime BeginDate, DateTime UntilDate)
		{
			Attendance db = new Attendance(User);
			return Convert.ToBoolean(db.ExecuteScalar<int>(string.Format(
				"SELECT COUNT(*) FROM [TM Log CONSO] WITH(READUNCOMMITTED) " +
				"WHERE [Locked] = 1 AND " +
				"	   (CONVERT(VARCHAR(8),[Begin Date],112) BETWEEN '{0}' AND '{1}' OR " +
				"	    CONVERT(VARCHAR(8),[Until Date],112) BETWEEN '{0}' AND '{1}')"
				,BeginDate.Date.ToString("yyyyMMdd", new CultureInfo("en-US"))
				,UntilDate.Date.ToString("yyyyMMdd", new CultureInfo("en-US"))), null));
		}

		/// <summary>
		/// ดึง Log ของการส่งเมลล์สรุปให้พนักงาน
		/// </summary>
		/// <param name="User"></param>
		/// <returns></returns>
		public static List<LogMailRecord> ListLogMail(IPrincipal User)
		{
			Attendance obj = new Attendance(User);
			List<LogMailRecord> list = new List<LogMailRecord>();
			using (SqlConnection conn = obj.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText = 
						  "SELECT	head.[Log ID],head.[Schedule Date],head.[Log Time],head.[Log Person], "
						+ "			person.[TH Prefix],person.[TH First Name],person.[TH Last Name], "
						+ "			COUNT(line.[Person No]) AS [Total Record],MAX(line.[Send Time]) AS [Send Time], "
						+ "			MAX(line.[Sent]) AS [Sent],MAX(line.[Error]) AS [Error] "
						+ "FROM		[TM Log MAIL Trans] AS line WITH(READUNCOMMITTED) INNER JOIN "
						+ "			[TM Log MAIL Header] AS head WITH(READUNCOMMITTED) ON line.[Log ID]=head.[Log ID] LEFT OUTER JOIN "
						+ "			[LEAVE].[dbo].[HR Person] AS person WITH(READUNCOMMITTED) ON head.[Log Person]=person.[Person No] "
						+ "GROUP BY	head.[Log ID],head.[Schedule Date],head.[Log Time],head.[Log Person], "
						+ "			person.[TH Prefix],person.[TH First Name],person.[TH Last Name] "
						+ "ORDER BY	head.[Log Time] DESC";
					using (SqlDataReader rs = cmd.ExecuteReader())
					{
						LogMailRecord rec;
						while (rs.Read())
						{
							rec = new LogMailRecord();
							rec.LogID = rs.GetValue<Int64>("Log ID");
							rec.ScheduleDate = rs.GetValue<DateTime?>("Schedule Date");
							rec.TotalRecord = rs.GetValue<int>("Total Record");
							rec.Sent = rs.GetValue<int>("Sent");
							rec.SendTime = rs.GetValue<DateTime?>("Send Time");
							rec.Error = rs.GetValue<string>("Error");
							rec.LogTime = rs.GetValue<DateTime?>("Log Time");
							
							rec.Person = new PersonRecord();
							rec.Person.PrefixTH = rs.GetValue<string>("TH Prefix");
							rec.Person.NameFirstTH = rs.GetValue<string>("TH First Name");
							rec.Person.NameLastTH = rs.GetValue<string>("TH Last Name");

							list.Add(rec);
						}
					}
				}
			}
			return list;
		}

		/// <summary>
		/// บันทึกรายการส่งอีเมลล์สรุปประจำเดือน
		/// </summary>
		/// <param name="User"></param>
		/// <param name="ScheduleDate">วันที่สำหรับส่งอีเมลล์</param>
		/// <param name="Person">รายชื่อพนักงาน</param>
		/// <returns></returns>
		public static int AddLogMail(IPrincipal User, DateTime ScheduleDate, List<PersonRecord> Person)
		{
			SqlTransaction t = null;
			Attendance db = new Attendance(User);
			using (SqlConnection conn = db.GetConnection())
			{
				conn.Open();
				t = conn.BeginTransaction();
				try
				{
					#region Log Mail Header
					Int64 ID = DateTime.Now.Ticks;
					using (SqlCommand cmd = conn.CreateCommand())
					{
						cmd.Transaction = t;
						cmd.CommandType = CommandType.Text;
						cmd.CommandText = "INSERT INTO [TM Log MAIL Header] " +
							"        ([Log ID], [Schedule Date], [Log Time], [Log Person]) " +
							"VALUES  (@id, @date, @time, @person)";
						cmd.Parameters.Clear();
						cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = ID;
						cmd.Parameters.Add("@date", SqlDbType.DateTime).Value = ScheduleDate.Date;
						cmd.Parameters.Add("@time", SqlDbType.DateTime).Value = DateTime.Now;
						cmd.Parameters.Add("@person", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
						cmd.ExecuteNonQuery();
					}
					#endregion

					#region Log Mail Transection
					int lineCount = 0;
					foreach (PersonRecord rec in Person)
					{
						using (SqlCommand cmd = conn.CreateCommand())
						{
							cmd.Transaction = t;
							cmd.CommandType = CommandType.Text;
							cmd.CommandText = "INSERT INTO [TM Log MAIL Trans] " +
								"        ([Log ID], [Person No], [E-Mail], [Sent]) " +
								"VALUES  (@id, @person, @email, 0)";
							cmd.Parameters.Clear();
							cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = ID;
							cmd.Parameters.Add("@email", SqlDbType.VarChar).Value = rec.Email;
							cmd.Parameters.Add("@person", SqlDbType.VarChar).Value = rec.PersonNo;
							lineCount += cmd.ExecuteNonQuery();
						}
					}
					#endregion

					t.Commit();

					return lineCount;
				}
				catch (Exception e)
				{
					try
					{
						if (t != null)
							t.Rollback();
					}
					catch
					{
					}
					throw e;
				}
			}
		}

		/// <summary>
		/// ดึงข้อมูลเวลาเข้า-ออกงาน
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <param name="BeginDate"></param>
		/// <param name="UntilDate"></param>
		/// <returns></returns>
		public static List<SummaryRecord> ListCheckTime(IPrincipal User, string PersonNo, DateTime BeginDate, DateTime UntilDate)
		{
			Attendance obj = new Attendance(User);
			List<SummaryRecord> list = new List<SummaryRecord>();
			using (SqlConnection conn = obj.GetConnection())
			{
				conn.Open();
				
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText =
						  "SELECT   att.[User ID],att.[Person No],att.[Check Date],att.[Check In],att.[Check Out], "
						+ "         att.[WShift Morning In],att.[WShift Morning Out],att.[WShift Noon In],att.[WShift Noon Out], "
						+ "         att.[Hours Gross],att.[Hours by WShift] "
						+ "FROM     [TM GET Summary] AS att WITH(READUNCOMMITTED) LEFT OUTER JOIN "
						+ "         [TM GET Summary Revise] AS rev WITH(READUNCOMMITTED) "
						+ "				ON att.[User ID]=rev.[User ID] AND att.[Check Date]=rev.[Check Date] "
						+ "WHERE    att.[User ID]=@id AND att.[Check Date] BETWEEN @begin AND @until "
						+ "ORDER BY att.[Person No], att.[Check Date]";
					cmd.Parameters.Clear();
					cmd.Parameters.Add("@id", SqlDbType.Int).Value = Finger.GetUserID(User, PersonNo);
					cmd.Parameters.Add("@begin", SqlDbType.DateTime).Value = BeginDate;
					cmd.Parameters.Add("@until", SqlDbType.DateTime).Value = UntilDate;
					using (SqlDataReader rs = cmd.ExecuteReader())
					{
						SummaryRecord rec;
						while (rs.Read())
						{
							rec = new SummaryRecord();
							rec.UserID = rs.GetValue<Int64>("User ID");
							rec.CheckDate = rs.GetValue<DateTime?>("Check Date");
							rec.CheckIn = rs.GetValue<DateTime?>("Check In");
							rec.CheckOut = rs.GetValue<DateTime?>("Check Out");
							rec.HoursGross = rs.GetValue<DateTime?>("Hours Gross");
							rec.MorningIn = rs.GetValue<DateTime?>("WShift Morning In");
							rec.MorningOut = rs.GetValue<DateTime?>("WShift Morning Out");
							rec.AfternoonIn = rs.GetValue<DateTime?>("WShift Noon In");
							rec.AfternoonOut = rs.GetValue<DateTime?>("WShift Noon Out");
							rec.HoursByShift = rs.GetValue<DateTime?>("Hours by WShift");

							//rec.Person = new PersonRecord();
							//rec.Person.PersonNo = rs.GetValue<string>("Person No");
							//rec.Person.PrefixTH = rs.GetValue<string>("TH Prefix");
							//rec.Person.NameFirstTH = rs.GetValue<string>("TH First Name");
							//rec.Person.NameLastTH = rs.GetValue<string>("TH Last Name");
							//rec.Person.PrefixEN = rs.GetValue<string>("EN Prefix");
							//rec.Person.NameFirstEN = rs.GetValue<string>("EN First Name");
							//rec.Person.NameLastEN = rs.GetValue<string>("EN Last Name");

							//rec.Person.Employee = new EmployeeRecord();
							//rec.Person.Employee.EmployeeNo = rs.GetValue<string>("Employee No");
							//rec.Person.Employee.CompanyCode = rs.GetValue<string>("Company Code");
							//rec.Person.Employee.Department = rs.GetValue<string>("Department");
							//rec.Person.Employee.Section = rs.GetValue<string>("Section");
							//rec.Person.Employee.PositionTH = rs.GetValue<string>("TH Position");
							//rec.Person.Employee.PositionEN = rs.GetValue<string>("EN Position");
							list.Add(rec);
						}
					}
				}
			}
			return list;
		}

		/// <summary>
		/// แก้่ไขเวลาเข้า-ออกงาน
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo">รหัสพนักงาน</param>
		/// <param name="CheckDate">วันที่</param>
		/// <param name="ShiftIn">กะเข้างาน</param>
		/// <param name="ShiftOut">กะออกงาน</param>
		/// <param name="CheckIn">เวลาเข้างาน</param>
		/// <param name="CheckOut">เวลาออกงาน</param>
		/// <returns></returns>
		public static int ChangeTime(IPrincipal User, Int64 UserID, DateTime CheckDate, TimeSpan ShiftIn, TimeSpan ShiftOut, TimeSpan CheckIn, TimeSpan CheckOut)
		{
			Attendance db = new Attendance(User);
			bool hasRecord = Convert.ToBoolean(db.ExecuteScalar<int>(string.Format(
				"SELECT COUNT(*) FROM [TM GET Summary Revise] WITH(READUNCOMMITTED) " +
				"WHERE  [User ID] = {0} AND CONVERT(VARCHAR(8),[Check Date],112) = '{1}'"
				,UserID.ToString(), CheckDate.Date.ToString("yyyyMMdd", new CultureInfo("en-US"))), null));

			using (SqlConnection conn = db.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					if (hasRecord)
					{
						//cmd.CommandText = "UPDATE [TM GET Summary Revise] SET "
						//    + " [Check In]=@cin "
						//    + ",[Check Out]=@cout "
						//    + ",[WShift Morning In]=@min "
						//    + ",[WShift Morning Out]=@mout "
						//    + ",[WShift Noon In]=@nin "
						//    + ",[WShift Noon Out]=@nout "
						//    + "WHERE [Check Date]=@date";
						//cmd.Parameters.Clear();
						//cmd.Parameters.Add("@date", SqlDbType.DateTime).Value = CheckDate.Date;
						//cmd.Parameters.Add("@cin", SqlDbType.DateTime).Value = 
						//    new DateTime(1900, 1, 1, CheckIn.Hours, CheckIn.Minutes, CheckIn.Seconds);
						//cmd.Parameters.Add("@cout", SqlDbType.DateTime).Value = 
						//    new DateTime(1900, 1, 1, CheckOut.Hours, CheckOut.Minutes, CheckOut.Seconds);
						//return cmd.ExecuteNonQuery();
					}
					else
					{
						//cmd.CommandText = "INSERT INTO	[LV Workshift] "
						//    + "			([Workshift ID], [Workshifts ID], [Type], "
						//    + "			 [Time Begin], [Time Until], [Description], [Modify Date], [Modify Person]) "
						//    + "VALUES	((SELECT MAX([Workshift ID]) + 1 FROM [LV Workshift]), "
						//    + "			 @WorkshiftsID, @Type, @TimeBegin, @TimeUntil, "
						//    + "			 @Description, @ModifyDate, @ModifyPerson)";
						//cmd.Parameters.Clear();
						//cmd.Parameters.Add("@WorkshiftsID", SqlDbType.Int).Value = WorkshiftsID;
						//cmd.Parameters.Add("@Type", SqlDbType.Int).Value = rec.Type;
						//cmd.Parameters.Add("@TimeBegin", SqlDbType.DateTime).Value = rec.TimeBegin;
						//cmd.Parameters.Add("@TimeUntil", SqlDbType.DateTime).Value = rec.TimeUntil;
						//cmd.Parameters.Add("@Description", SqlDbType.VarChar).Value = rec.Description;
						//cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
						//cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
						//result += cmd.ExecuteNonQuery();
					}
				}
			}

			return 0;
		}

	}
}
