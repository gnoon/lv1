using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Principal;
using System.Data.SqlClient;
using System.Data;

namespace LeaveCore
{
	public class Finger : TimeDB
	{
		public Finger(IPrincipal User)
			: base(User)
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <returns></returns>
		public static int GetUserID(IPrincipal User, string PersonNo)
		{
			Finger obj = new Finger(User);
			return obj.ExecuteScalar<int>(string.Format(
			    "SELECT [User ID] FROM [TM Person] WITH(READUNCOMMITTED) WHERE [Person No] = '{0}'", PersonNo), null);
		}

		/// <summary>
		/// ดึงรหัสสแกนนิ้วมือของพนักงานทั้งหมด
		/// </summary>
		/// <param name="User"></param>
		/// <returns></returns>
		public static Dictionary<string, string> ListUserID(IPrincipal User)
		{
			Finger obj = new Finger(User);
			Dictionary<string, string> list = new Dictionary<string, string>();
			using (SqlConnection conn = obj.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText = "SELECT [Person No], [User ID] FROM [TM Person] WITH(READUNCOMMITTED)";
					using (SqlDataReader rs = cmd.ExecuteReader())
					{
						while (rs.Read())
						{
							list.Add(rs.GetValue<string>("Person No"), rs.GetValue<string>("User ID"));
						}
					}
				}
			}
			return list;
		}

		/// <summary>
		/// เซ็ตรหัสสแกนนิ้วมือ
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <param name="UserID"></param>
		/// <returns></returns>
		public static int ChangeUserID(IPrincipal User, string PersonNo, int UserID)
		{
			Finger obj = new Finger(User);
			bool hasRecord = Convert.ToBoolean(obj.ExecuteScalar<int>(string.Format(
			    "SELECT COUNT(*) FROM [TM Person] WITH(READUNCOMMITTED) WHERE [Person No] = '{0}'", PersonNo), null));
			using (SqlConnection conn = obj.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
					if (hasRecord)
					{
						cmd.CommandText = "UPDATE [TM Person] SET [User ID]=@id WHERE [Person No]=@no";
						cmd.Parameters.Clear();
						cmd.Parameters.Add("@id", SqlDbType.Int).Value = UserID;
						cmd.Parameters.Add("@no", SqlDbType.VarChar).Value = PersonNo;
						return cmd.ExecuteNonQuery();
					}
					else
					{
						cmd.CommandText = "INSERT INTO [TM Person] ([Person No], [User ID]) VALUES (@no, @id)";
						cmd.Parameters.Clear();
						cmd.Parameters.Add("@id", SqlDbType.Int).Value = UserID;
						cmd.Parameters.Add("@no", SqlDbType.VarChar).Value = PersonNo;
						return cmd.ExecuteNonQuery();
					}
			    }
			}
		}

		/// <summary>
		/// ดึงข้อมูลบุคลากรยกเว้นการลงเวลา
		/// </summary>
		/// <param name="User"></param>
		/// <returns></returns>
		public static List<PersonRecord> ListException(IPrincipal User)
		{
			Finger obj = new Finger(User);
			List<PersonRecord> list = new List<PersonRecord>();
			using (SqlConnection conn = obj.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText =
							"SELECT  ps.[Person No], em.[Employee No], ps.[TH Prefix], "
						+ "       ps.[EN Prefix], ps.[TH First Name], ps.[EN First Name], ps.[TH Last Name], ps.[EN Last Name], "
						+ "       em.[Company Code], em.Department, em.Section, em.[TH Position], em.[EN Position] "
						+ "FROM   [TM Person Exceptions] AS ex WITH(READUNCOMMITTED) INNER JOIN "
						+ "		  [LEAVE].[dbo].[HR Person] AS ps WITH(READUNCOMMITTED) ON ex.[Person No]=ps.[Person No] LEFT OUTER JOIN "
						+ "         [LEAVE].[dbo].[HR Employee] AS em WITH(READUNCOMMITTED) INNER JOIN ("
						+ "             SELECT em.[Person No], MAX(em.[Starting Date]) AS [Starting Date] "
						+ "             FROM [LEAVE].[dbo].[HR Employee] em WITH(READUNCOMMITTED) "
						+ "             WHERE EXISTS (SELECT 1 FROM [TM Person Exceptions] "
						+ "					WITH(READUNCOMMITTED) WHERE [Person No]=em.[Person No]) "
						+ "             GROUP BY em.[Person No] "
						+ "         ) latest ON em.[Person No]=latest.[Person No] AND em.[Starting Date]=latest.[Starting Date] "
						+ "         ON ps.[Person No] = em.[Person No] "
						+ "ORDER BY em.[Starting Date]";
					using (SqlDataReader rs = cmd.ExecuteReader())
					{
						PersonRecord rec;
						while (rs.Read())
						{
							rec = new PersonRecord();
							rec.Employee = new EmployeeRecord();
							rec.PersonNo = rs.GetValue<string>("Person No");
							rec.PrefixTH = rs.GetValue<string>("TH Prefix");
							rec.PrefixEN = rs.GetValue<string>("EN Prefix");
							rec.NameFirstTH = rs.GetValue<string>("TH First Name");
							rec.NameFirstEN = rs.GetValue<string>("EN First Name");
							rec.NameLastTH = rs.GetValue<string>("TH Last Name");
							rec.NameLastEN = rs.GetValue<string>("EN Last Name");
							rec.Employee.EmployeeNo = rs.GetValue<string>("Employee No");
							rec.Employee.CompanyCode = rs.GetValue<string>("Company Code");
							rec.Employee.Department = rs.GetValue<string>("Department");
							rec.Employee.Section = rs.GetValue<string>("Section");
							rec.Employee.PositionTH = rs.GetValue<string>("TH Position");
							rec.Employee.PositionEN = rs.GetValue<string>("EN Position");

							list.Add(rec);
						}
					}
				}
			}
			return list;
		}

		/// <summary>
		/// เพิ่มบุคลากรยกเว้นการลงเวลา
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <returns></returns>
		public static int AddException(IPrincipal User, string PersonNo)
		{
			Finger obj = new Finger(User);
			using (SqlConnection conn = obj.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "INSERT INTO [TM Person Exceptions] " +
			            "        ([Person No], [Check In Out], [Send Report Email]) " +
			            "VALUES  (@id, 1, 0)";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@id", SqlDbType.VarChar).Value = PersonNo;
			        return cmd.ExecuteNonQuery();
			    }
			}
		}

		/// <summary>
		/// ลบบุคลากรยกเว้นการลงเวลา
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <returns></returns>
		public static int DeleteException(IPrincipal User, string PersonNo)
		{
			Finger obj = new Finger(User);
			using (SqlConnection conn = obj.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "DELETE FROM [TM Person Exceptions] WHERE [Person No] = @id";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@id", SqlDbType.VarChar).Value = PersonNo;
			        return cmd.ExecuteNonQuery();
			    }
			}
		}

		/// <summary>
		/// ดึง Log ของการดึงข้อมูลเวลาเข้า-ออกงาน
		/// </summary>
		/// <param name="User"></param>
		/// <returns></returns>
		public static List<LogGetRecord> ListLogGet(IPrincipal User)
		{
			Finger obj = new Finger(User);
			List<LogGetRecord> list = new List<LogGetRecord>();
			using (SqlConnection conn = obj.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText = 
						  "SELECT	log.[Log ID],log.[Begin Date],log.[Until Date],log.[Total Record], "
						+ "			log.[Status Progress],log.[Log Time],log.[Log Person],log.[Source], "
						+ "			per.[TH Prefix],per.[TH First Name],per.[TH Last Name] "
						+ "FROM		[TM Log GET] AS log WITH(READUNCOMMITTED) LEFT OUTER JOIN "
						+ "			[LEAVE].[dbo].[HR Person] AS per WITH(READUNCOMMITTED) ON log.[Log Person]=per.[Person No] "
						+ "ORDER BY	log.[Log Time] DESC";
					using (SqlDataReader rs = cmd.ExecuteReader())
					{
						LogGetRecord rec;
						while (rs.Read())
						{
							rec = new LogGetRecord();
							rec.LogID = rs.GetValue<Int64>("Log ID");
							rec.BeginDate = rs.GetValue<DateTime?>("Begin Date");
							rec.UntilDate = rs.GetValue<DateTime?>("Until Date");
							rec.TotalRecord = rs.GetValue<int>("Total Record");
							rec.StatusProgress = rs.GetValue<string>("Status Progress");
							rec.LogTime = rs.GetValue<DateTime?>("Log Time");
							//rec.LogPerson = rs.GetValue<string>("Log Person");
							rec.Source = rs.GetValue<string>("Source");
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
		/// Add Log ของการดึงข้อมูลเวลาเข้า-ออกงาน
		/// </summary>
		/// <param name="User"></param>
		/// <param name="LogID"></param>
		/// <param name="BeginDate"></param>
		/// <param name="UntilDate"></param>
		/// <returns></returns>
		public static int AddLogGet(IPrincipal User, Int64 LogID, DateTime BeginDate, DateTime UntilDate)
		{
			Finger db = new Finger(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "INSERT INTO [TM Log GET] " +
			            "        ([Log ID], [Begin Date], [Until Date], " +
			            "         [Total Record], [Status Progress], [Log Time], [Log Person], [Source]) " +
			            "VALUES  (@id, @begin, @until, @record, @status, @time, @person, @source)";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = LogID;
			        cmd.Parameters.Add("@begin", SqlDbType.DateTime).Value = BeginDate.Date;
			        cmd.Parameters.Add("@until", SqlDbType.DateTime).Value = UntilDate.Date;
			        cmd.Parameters.Add("@record", SqlDbType.Int).Value = 0;
			        cmd.Parameters.Add("@status", SqlDbType.VarChar).Value = "PENDING";
			        cmd.Parameters.Add("@time", SqlDbType.DateTime).Value = DateTime.Now;
			        cmd.Parameters.Add("@person", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
					cmd.Parameters.Add("@source", SqlDbType.VarChar).Value = "MANUAL";
			        return cmd.ExecuteNonQuery();
			    }
			}
		}

	}
}
