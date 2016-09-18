using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Security.Principal;

namespace LeaveCore
{
    public class Workshifts : BaseDB
    {
        private List<WorkshiftRecord> _Times;
        public List<WorkshiftRecord> Times
        {
            get
            {
                return this._Times == null ? new List<WorkshiftRecord>() : this._Times;
            }
            set { this._Times = value; }
        }
        public string PersonNo { get; set; }

        public Workshifts(IPrincipal User, string PersonNo)
            : this(User, PersonNo, true)
        {
        }

        public Workshifts(IPrincipal User, string PersonNo, bool LoadDefault)
            : base(User)
        {
            this.PersonNo = PersonNo;
            if (!string.IsNullOrEmpty(PersonNo))
            {
                this._Times = this.Query(PersonNo);
				if (LoadDefault)
				{
					if (this._Times == null || this._Times.Count == 0)
						this._Times = Workshifts.DefaultWorkshifts;
				}
            }
        }

        public List<WorkshiftRecord> Query(string PersonNo)
        {
            List<WorkshiftRecord> list = new List<WorkshiftRecord>();
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    #region Getting Workshifts of Emp
                    cmd.CommandText =
                          "SELECT   [Person No],[Time Begin],[Time Until],[Type],[Remark],[Modify Date],[Modify Person] "
                        + "FROM     [LV Profile Workshift] WITH(READUNCOMMITTED) "
                        + "WHERE    [Person No] = @perno "
                        + "ORDER BY [Type], [Time Begin]";
                    cmd.Parameters.Add("@perno", SqlDbType.VarChar).Value = PersonNo == null ? DBNull.Value : (object)PersonNo;
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        int i;
                        WorkshiftRecord rec;
                        while (rs.Read())
                        {
                            rec = new WorkshiftRecord();
                            i = rs.GetOrdinal("Person No");
                            rec.PersonNo = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("Time Begin");
							rec.TimeBegin = rs.IsDBNull(i) ? null : (DateTime?)rs.GetDateTime(i);
							i = rs.GetOrdinal("Time Until");
							rec.TimeUntil = rs.IsDBNull(i) ? null : (DateTime?)rs.GetDateTime(i);
							i = rs.GetOrdinal("Type");
							rec.Type = rs.IsDBNull(i) ? -1 : Convert.ToInt32(rs.GetValue(i));
							i = rs.GetOrdinal("Remark");
							rec.Remark = rs.IsDBNull(i) ? null : rs.GetString(i);
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

        public bool IsInWorkTime(DateTime time)
        {
            if (this._Times == null) return false;
            foreach (WorkshiftRecord rec in this.Times)
                if (rec.TimeBegin.HasValue && rec.TimeUntil.HasValue)
                    if (rec.TimeBegin.Value.Hour <= time.Hour && rec.TimeBegin.Value.Minute <= time.Minute
                        && time.Hour <= rec.TimeUntil.Value.Hour && time.Minute <= rec.TimeUntil.Value.Minute)
                        return true;
            return false;
        }

        public WorkshiftRecord Get(DateTime time)
        {
            if (this._Times == null) return null;
            foreach (WorkshiftRecord rec in this.Times)
                if (rec.TimeBegin.HasValue && rec.TimeUntil.HasValue)
                    if (rec.TimeBegin.Value.Hour <= time.Hour && rec.TimeBegin.Value.Minute <= time.Minute
                        && time.Hour <= rec.TimeUntil.Value.Hour && time.Minute <= rec.TimeUntil.Value.Minute)
                        return rec;
            return null;
        }

        public decimal GetTotalHours(decimal DefaultWorkHoursADay)
        {
            if (this._Times == null) return DefaultWorkHoursADay;
            DateTime a, b;
            TimeSpan d;
            decimal hours = 0;
            foreach (WorkshiftRecord rec in this.Times)
                if (rec.TimeBegin.HasValue && rec.TimeUntil.HasValue)
                {
                    a = new DateTime(1900, 1, 1, rec.TimeBegin.Value.Hour, rec.TimeBegin.Value.Minute, 0);
                    b = new DateTime(1900, 1, 1, rec.TimeUntil.Value.Hour, rec.TimeUntil.Value.Minute, 0);
                    d = b - a;
                    hours += (decimal)d.Seconds / (decimal)3600;
                }
            return hours;
        }

        public WorkshiftRecord[] GetMinMax()
        {
            if (this._Times == null) return new WorkshiftRecord[] { };
            List<WorkshiftRecord> list = new List<WorkshiftRecord>(2);
            foreach (WorkshiftRecord rec in this.Times)
                if (rec.TimeBegin.HasValue && rec.TimeUntil.HasValue)
                {
                    //min
                    if (list.Count == 0) list.Add(rec);
                    else if (rec.TimeBegin.Value.Hour < list[0].TimeBegin.Value.Hour
                        && rec.TimeBegin.Value.Minute < list[0].TimeBegin.Value.Minute) list[0] = rec;

                    // max
                    else if (list.Count == 1) list.Add(rec);
                    else if (rec.TimeUntil.Value.Hour > list[1].TimeUntil.Value.Hour
                        && rec.TimeUntil.Value.Minute > list[1].TimeUntil.Value.Minute) list[1] = rec;
                }
            return list.ToArray();
        }

        public List<KeyValuePair<int, string>> List
        {
            get
            {
                List<KeyValuePair<int, string>> list = new List<KeyValuePair<int, string>>();
                using (SqlConnection conn = GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        #region Getting Workshifts
                        cmd.CommandText =
                              "SELECT  [Office Staff], [Workshifts ID], Description "
                            + "FROM    [LV Workshifts] WITH(READUNCOMMITTED) "
                            + "WHERE   (Active = 1) "
                            + "ORDER BY [Office Staff], [Workshifts ID] DESC";
                        using (SqlDataReader rs = cmd.ExecuteReader())
                        {
                            int i, key, flag;
                            string val;
                            KeyValuePair<int, string> rec;
                            while (rs.Read())
                            {
                                i = rs.GetOrdinal("Office Staff");
                                flag = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
                                i = rs.GetOrdinal("Workshifts ID");
                                key = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
                                i = rs.GetOrdinal("Description");
                                val = rs.IsDBNull(i) ? null : rs.GetString(i);

                                rec = new KeyValuePair<int, string>(key, string.Format("{0}|{1}",flag,val));
                                list.Add(rec);
                            }
                        }
                        #endregion
                    }
                }

                return list;
            }
        }

        public static List<WorkshiftRecord> DefaultWorkshifts
        {
            get
            {
                List<WorkshiftRecord> days = new List<WorkshiftRecord>(new WorkshiftRecord[] {
                    new WorkshiftRecord() {
                        TimeBegin = new DateTime(DateTime.Now.Year,DateTime.Now.Month,DateTime.Now.Day,Const.DEFAULT_MORNING_BEGINOCLOCK,0,0)
                        , TimeUntil = new DateTime(DateTime.Now.Year,DateTime.Now.Month,DateTime.Now.Day,Const.DEFAULT_MORNING_UNTILOCLOCK,0,0)
                        , Remark = "Morning Work"
                    }
                    ,
                    new WorkshiftRecord() {
                        TimeBegin = new DateTime(DateTime.Now.Year,DateTime.Now.Month,DateTime.Now.Day,Const.DEFAULT_NOON_BEGINOCLOCK,0,0)
                        , TimeUntil = new DateTime(DateTime.Now.Year,DateTime.Now.Month,DateTime.Now.Day,Const.DEFAULT_NOON_UNTILOCLOCK,0,0)
                        , Remark = "Afternoon Work"
                    }
                });
                return days;
            }
        }

		#region Static Functions

		/// <summary>
		/// สร้างรายการ กะเวลาทำงาน
		/// </summary>
		/// <param name="User">User ที่ Login เข้าระบบ</param>
		/// <param name="PersonNo">พนักงาน</param>
		/// <param name="Times">รายละเอียดกะเช้า-บ่าย</param>
		/// <returns>จำนวน record</returns>
		public static int AddWorkshift(IPrincipal User, string PersonNo, List<WorkshiftRecord> Times)
		{
			int result = 0;
			BaseDB db = new BaseDB(User);
			int resdel = DeleteWorkshift(User, PersonNo);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
					foreach (var rec in Times)
					{
						cmd.CommandText = "INSERT INTO [LV Profile Workshift] " +
							"        ([Person No], [Time Begin], [Time Until],  " +
							"         [Type], [Remark], [Modify Date], [Modify Person]) " +
							"VALUES  (@PersonNo, @TimeBegin, @TimeUntil, @Type, @Remark, @ModifyDate, @ModifyPerson)";
						cmd.Parameters.Clear();
						cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
						cmd.Parameters.Add("@TimeBegin", SqlDbType.DateTime).Value = rec.TimeBegin;
						cmd.Parameters.Add("@TimeUntil", SqlDbType.DateTime).Value = rec.TimeUntil;
						cmd.Parameters.Add("@Type", SqlDbType.Int).Value = rec.Type;
						cmd.Parameters.Add("@Remark", SqlDbType.VarChar).Value = rec.Remark;
						cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
						cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
						result += cmd.ExecuteNonQuery();
					}
			    }
			}
			return result;
		}

		/// <summary>
		/// แก้ไขรายการ กะเวลาทำงาน
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <param name="Type">บ่งบอกประเภทกะเช้า-บ่าย</param>
		/// <param name="TimeBegin">เริ่มเวลา</param>
		/// <param name="TimeUntil">ถึงเวลา</param>
		/// <returns></returns>
		public static int EditWorkshift(IPrincipal User, string PersonNo, int Type, DateTime TimeBegin, DateTime TimeUntil)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "UPDATE [LV Profile Workshift] SET " +
			            " [Time Begin]=@TimeBegin " +
			            ",[Time Until]=@TimeUntil " +
			            ",[Modify Date]=@ModifyDate " +
			            ",[Modify Person]=@ModifyPerson " +
			            "WHERE [Person No]=@PersonNo AND [Type]=@Type";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
					cmd.Parameters.Add("@TimeBegin", SqlDbType.DateTime).Value = TimeBegin;
					cmd.Parameters.Add("@TimeUntil", SqlDbType.DateTime).Value = TimeUntil;
					cmd.Parameters.Add("@Type", SqlDbType.Int).Value = Type;
			        cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
			        cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		/// <summary>
		/// ลบรายการ กะเวลาทำงาน
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <returns></returns>
		public static int DeleteWorkshift(IPrincipal User, string PersonNo)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "DELETE FROM [LV Profile Workshift] WHERE [Person No]=@PersonNo";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		#endregion
    }
}
