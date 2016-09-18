using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Security.Principal;

namespace LeaveCore
{
    public class Weekends : BaseDB
    {
        private List<WeekendRecord> _Rules;
        public List<WeekendRecord> Rules
        {
            get
            {
                return this._Rules == null ? new List<WeekendRecord>() : this._Rules;
            }
            set { this._Rules = value; }
        }
        private List<KeyValuePair<DateTime, KeyValuePair<DateTime, string>>> _Moves;
        public List<KeyValuePair<DateTime, KeyValuePair<DateTime, string>>> Moves
        {
            get
            {
                return this._Moves == null ? new List<KeyValuePair<DateTime, KeyValuePair<DateTime, string>>>() : this._Moves;
            }
            set { this._Moves = value; }
        }

        public string PersonNo { get; set; }

        public Weekends(IPrincipal User, string PersonNo)
            : base(User)
        {
            this.PersonNo = PersonNo;
            if (!string.IsNullOrEmpty(PersonNo))
            {
                _Rules = this.Query(PersonNo);
                _Moves = this.QueryMoves(PersonNo);
            }
        }

        public List<WeekendRecord> Query(string PersonNo)
        {
            List<WeekendRecord> list = new List<WeekendRecord>();
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    #region Getting Weekends of Emp
                    cmd.CommandText =
                          "SELECT  [Person No],[Day Of Week],[Starting Week Of Month],[Every N Week Of Month], "
                        + "        [Exclude Week Of Month],[TH Name],[EN Name],[Remark],[Modify Date],[Modify person] "
                        + "FROM    [LV Profile Weekend] WITH(READUNCOMMITTED) "
                        + "WHERE   [Person No] = @perno "
                        + "ORDER BY [Day Of Week]";
                    cmd.Parameters.Add("@perno", SqlDbType.VarChar).Value = PersonNo == null ? DBNull.Value : (object)PersonNo;
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        int i;
                        WeekendRecord rec;
                        while (rs.Read())
                        {
                            rec = new WeekendRecord();
                            i = rs.GetOrdinal("Person No");
                            rec.PersonNo = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("Day Of Week");
                            rec.DayOfWeek = rs.IsDBNull(i) ? null
                                : (DayOfWeek?)Enum.ToObject(typeof(DayOfWeek), Convert.ToInt32(rs.GetValue(i)));
                            i = rs.GetOrdinal("Starting Week Of Month");
                            rec.StartingWeekOfMonth = rs.IsDBNull(i) ? -1 : Convert.ToInt32(rs.GetValue(i));
                            i = rs.GetOrdinal("Every N Week Of Month");
                            rec.EveryNWeekOfMonth = rs.IsDBNull(i) ? -1 : Convert.ToInt32(rs.GetValue(i));
                            i = rs.GetOrdinal("Exclude Week Of Month");
                            rec.ExcludeWeekOfMonth = rs.IsDBNull(i) ? -1 : Convert.ToInt32(rs.GetValue(i));
                            i = rs.GetOrdinal("TH Name");
                            rec.NameTH = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("EN Name");
                            rec.NameEN = rs.IsDBNull(i) ? null : rs.GetString(i);
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

        public List<KeyValuePair<DateTime, KeyValuePair<DateTime, string>>> QueryMoves(string PersonNo)
        {
            List<KeyValuePair<DateTime, KeyValuePair<DateTime, string>>> list = new List<KeyValuePair<DateTime, KeyValuePair<DateTime, string>>>();
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    #region Getting Weekends of Emp
                    cmd.CommandText =
                          "SELECT  [From Date],[To Date],[Remark] "
                        + "FROM    [LV Profile Weekend Movement] WITH(READUNCOMMITTED) "
                        + "WHERE   [Person No] = @perno "
                        + "ORDER BY [To Date]";
                    cmd.Parameters.Add("@perno", SqlDbType.VarChar).Value = PersonNo == null ? DBNull.Value : (object)PersonNo;
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        while (rs.Read())
                        {
                            list.Add(new KeyValuePair<DateTime, KeyValuePair<DateTime, string>>(
                                rs.GetValue<DateTime>("To Date"),
                                new KeyValuePair<DateTime, string>(
                                    rs.GetValue<DateTime>("From Date"),
                                    rs.GetValue<string>("Remark")
                                )
                            ));
                        }
                    }
                    #endregion
                }
            }
            return list;
        }

        public bool IsWeekend(DateTime date)
        {
            if (this._Rules == null) return false;
            if (this._Moves != null)
            {
                // ตรวจสอบวันที่การย้ายวันหยุดก่อน
                if (this._Moves.Exists(x => x.Key.Date == date.Date))
                    return true;
            }
            List<DateTime> Dates = ListWeekendDates(date.Month, date.Year);
            return Dates.Contains(date.Date);
        }

        protected List<DateTime> ListWeekendDates(int Month, int Year)
        {
            if (this._Rules == null) return new List<DateTime>();

            bool b = this._Moves != null;
            int WeekNo;
            DateTime dt;
            List<DateTime> list = new List<DateTime>();
            int Days = DateTime.DaysInMonth(Year, Month);
            foreach (WeekendRecord rec in this.Rules) // ตามจำนวน Rules
            {
                WeekNo = 0;
                for (int d = 1; d <= Days; d++) // ตามจำนวนวันของเดือน
                {
                    dt = new DateTime(Year, Month, d);
                    if (dt.DayOfWeek == rec.DayOfWeek)
                    {
                        WeekNo++;
                        if (RuleToWeeks(rec).Contains(WeekNo) && WeekNo != rec.ExcludeWeekOfMonth)
                        {
                            if (b && !this._Moves.Exists(x => x.Value.Key.Date == dt.Date && x.Key.Date != x.Value.Key.Date))
                                list.Add(dt);
                        }
                    }
                }
            }
            return list;
        }

        private List<int> RuleToWeeks(WeekendRecord rec)
        {
            List<int> list = new List<int>();
            for (int w = 1; w <= 5; w++) // 1 เดือนมี 5 สัปดาห์สูงสุด
            {
                for (int i = 0; i < 5; i++)
                {
                    if (w == (rec.StartingWeekOfMonth + (rec.EveryNWeekOfMonth * i)))
                        list.Add(w);
                }
            }
            return list;
        }

        public int[] WeekendDBDays
        {
            get
            {
                if (this._Rules == null) return new int[] { };
                CultureInfo enCulture = new CultureInfo("en-US");
                List<int> list = new List<int>();
                foreach (WeekendRecord rec in this.Rules)
                    if (rec.DayOfWeek.HasValue)
                        list.Add((int)rec.DayOfWeek.Value);
                return list.ToArray();
            }
        }

        public static List<WeekendRecord> DefaultDays
        {
            get
            {
                List<WeekendRecord> days = new List<WeekendRecord>(new WeekendRecord[] {
                    // Default ให้เป็นสำหรับพนักงานใหม่ช่วงฝึกงาน
                    //new WeekendRecord() {
                    //    DayOfWeek = DayOfWeek.Saturday
                    //    , StartingWeekOfMonth = 1
                    //    , EveryNWeekOfMonth = 1
                    //    , ExcludeWeekOfMonth = 5
                    //    , NameEN = DayOfWeek.Saturday.ToString()
                    //    , NameTH = DayOfWeek.Saturday.ToString()
                    //    , Remark = "System Default"
                    //}
                    //,
                    new WeekendRecord() {
                        DayOfWeek = DayOfWeek.Sunday
                        , StartingWeekOfMonth = 1
                        , EveryNWeekOfMonth = 1
                        , ExcludeWeekOfMonth = -1
                        , NameEN = DayOfWeek.Sunday.ToString()
                        , NameTH = DayOfWeek.Sunday.ToString()
                        , Remark = "System Default"
                    }
                });
                return days;
            }
        }

		#region Static Functions

		/// <summary>
		/// สร้างรายการ วันหยุดเว้นสัปดาห์ (วันปักษ์)
		/// </summary>
		/// <param name="User">User ที่ Login เข้าระบบ</param>
		/// <param name="PersonNo">พนักงาน</param>
		/// <param name="DayOfWeek"></param>
		/// <param name="StartingWeekOfMonth"></param>
		/// <param name="EveryNWeekOfMonth"></param>
		/// <param name="ExcludeWeekOfMonth"></param>
		/// <param name="Remark"></param>
		/// <returns>จำนวน record</returns>
		public static int AddWeekend(IPrincipal User, string PersonNo, int DayOfWeek, 
			int StartingWeekOfMonth, int EveryNWeekOfMonth, int ExcludeWeekOfMonth, string NameTH, string Remark)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "INSERT INTO [LV Profile Weekend] " +
			            "        ([Person No], [Day Of Week], [Starting Week Of Month], " +
			            "         [Every N Week Of Month], [Exclude Week Of Month], " +
			            "         [TH Name], [EN Name], [Remark], [Modify Date], [Modify Person]) " +
			            "VALUES  (@PersonNo, @DayOfWeek, @StartingWeekOfMonth, @EveryNWeekOfMonth, " +
						"         @ExcludeWeekOfMonth, @NameTH, @NameEN, @Remark, @ModifyDate, @ModifyPerson)";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
			        cmd.Parameters.Add("@DayOfWeek", SqlDbType.Int).Value = DayOfWeek;
			        cmd.Parameters.Add("@StartingWeekOfMonth", SqlDbType.Int).Value = StartingWeekOfMonth;
			        cmd.Parameters.Add("@EveryNWeekOfMonth", SqlDbType.Int).Value = EveryNWeekOfMonth;
			        cmd.Parameters.Add("@ExcludeWeekOfMonth", SqlDbType.Int).Value = ExcludeWeekOfMonth;
			        cmd.Parameters.Add("@NameTH", SqlDbType.VarChar).Value = NameTH ?? string.Empty;
			        cmd.Parameters.Add("@NameEN", SqlDbType.VarChar).Value = string.Empty;
			        cmd.Parameters.Add("@Remark", SqlDbType.VarChar).Value = Remark ?? string.Empty;
			        cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
			        cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		/// <summary>
		/// แก้ไขรายการ วันหยุดเว้นสัปดาห์ (วันปักษ์)
		/// </summary>
		/// <param name="User"></param>
		/// <param name="id">รายการเดิม</param>
		/// <param name="PersonNo"></param>
		/// <param name="DayOfWeek">รายการปัจจุบัน</param>
		/// <param name="StartingWeekOfMonth"></param>
		/// <param name="EveryNWeekOfMonth"></param>
		/// <param name="ExcludeWeekOfMonth"></param>
		/// <param name="Remark"></param>
		/// <returns></returns>
		public static int EditWeekend(IPrincipal User, int id, string PersonNo, int DayOfWeek, 
			int StartingWeekOfMonth, int EveryNWeekOfMonth, int ExcludeWeekOfMonth, string NameTH, string Remark)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "UPDATE [LV Profile Weekend] SET " +
			            " [Day Of Week]=@DayOfWeek " +
			            ",[Starting Week Of Month]=@StartingWeekOfMonth " +
			            ",[Every N Week Of Month]=@EveryNWeekOfMonth " +
			            ",[Exclude Week Of Month]=@ExcludeWeekOfMonth " +
			            ",[TH Name]=@NameTH " +
			            ",[EN Name]=@NameEN " +
			            ",[Remark]=@Remark " +
			            ",[Modify Date]=@ModifyDate " +
			            ",[Modify Person]=@ModifyPerson " +
			            "WHERE [Person No]=@PersonNo AND [Day Of Week]=@id";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
			        cmd.Parameters.Add("@DayOfWeek", SqlDbType.Int).Value = DayOfWeek;
			        cmd.Parameters.Add("@StartingWeekOfMonth", SqlDbType.Int).Value = StartingWeekOfMonth;
			        cmd.Parameters.Add("@EveryNWeekOfMonth", SqlDbType.Int).Value = EveryNWeekOfMonth;
			        cmd.Parameters.Add("@ExcludeWeekOfMonth", SqlDbType.Int).Value = ExcludeWeekOfMonth;
					cmd.Parameters.Add("@NameTH", SqlDbType.VarChar).Value = NameTH ?? string.Empty;
					cmd.Parameters.Add("@NameEN", SqlDbType.VarChar).Value = string.Empty;
			        cmd.Parameters.Add("@Remark", SqlDbType.VarChar).Value = Remark ?? string.Empty;
			        cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
			        cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		/// <summary>
		/// ลบรายการ วันหยุดเว้นสัปดาห์ (วันปักษ์)
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <param name="DayOfWeek"></param>
		/// <returns></returns>
		public static int DeleteWeekend(IPrincipal User, string PersonNo, int DayOfWeek)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "DELETE FROM [LV Profile Weekend] " +
			            "WHERE [Person No]=@PersonNo AND [Day Of Week]=@DayOfWeek";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
			        cmd.Parameters.Add("@DayOfWeek", SqlDbType.Int).Value = DayOfWeek;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		/// <summary>
		/// สร้างรายการ เปลี่ยนวันหยุด
		/// </summary>
		/// <param name="User">User ที่ Login เข้าระบบ</param>
		/// <param name="PersonNo">พนักงาน</param>
		/// <param name="FromDate"></param>
		/// <param name="ToDate"></param>
		/// <param name="Remark"></param>
		/// <returns></returns>
		public static int AddWeekendMovement(IPrincipal User, string PersonNo, DateTime FromDate, DateTime ToDate, string Remark)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "INSERT INTO [LV Profile Weekend Movement] " +
			            "        ([Person No], [From Date], [To Date], [Remark], [Modify Date], [Modify Person]) " +
			            "VALUES  (@PersonNo, @FromDate, @ToDate, @Remark, @ModifyDate, @ModifyPerson)";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
			        cmd.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = FromDate.Date;
			        cmd.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = ToDate.Date;
			        cmd.Parameters.Add("@Remark", SqlDbType.VarChar).Value = Remark;
			        cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
			        cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		/// <summary>
		/// แก้ไขรายการ เปลี่ยนวันหยุด
		/// </summary>
		/// <param name="User"></param>
		/// <param name="id">รายการเดิม (Key 1)</param>
		/// <param name="id1">รายการเดิม (Key 2)</param>
		/// <param name="PersonNo"></param>
		/// <param name="FromDate">รายการปัจจุบัน (Key 1)</param>
		/// <param name="ToDate">รายการปัจจุบัน (Key 2)</param>
		/// <param name="Remark"></param>
		/// <returns></returns>
		public static int EditWeekendMovement(IPrincipal User, string PersonNo, DateTime OldFromDate, DateTime OldToDate, 
			DateTime NewFromDate, DateTime NewToDate, string Remark)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "UPDATE [LV Profile Weekend Movement] SET " +
			            " [From Date]=@FromDate " +
			            ",[To Date]=@ToDate " +
			            ",[Remark]=@Remark " +
			            ",[Modify Date]=@ModifyDate " +
			            ",[Modify Person]=@ModifyPerson " +
			            "WHERE [Person No]=@PersonNo AND [From Date]=@fdate AND [To Date]=@tdate";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@fdate", SqlDbType.DateTime).Value = OldFromDate.Date;
			        cmd.Parameters.Add("@tdate", SqlDbType.DateTime).Value = OldToDate.Date;
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
			        cmd.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = NewFromDate.Date;
			        cmd.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = NewToDate.Date;
			        cmd.Parameters.Add("@Remark", SqlDbType.VarChar).Value = Remark;
			        cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
			        cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		/// <summary>
		/// ลบรายการ เปลี่ยนวันหยุด
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <param name="FromDate"></param>
		/// <param name="ToDate"></param>
		/// <returns></returns>
		public static int DeleteWeekendMovement(IPrincipal User, string PersonNo, DateTime FromDate, DateTime ToDate)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "DELETE FROM [LV Profile Weekend Movement] " +
			            "WHERE [Person No]=@PersonNo AND [From Date]=@FromDate AND [To Date]=@ToDate";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
			        cmd.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = FromDate.Date;
			        cmd.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = ToDate.Date;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		#endregion

    }
}
