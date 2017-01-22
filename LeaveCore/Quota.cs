using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Web.Configuration;
using System.Reflection;
using System.Security.Principal;
using System.Threading;

namespace LeaveCore
{
    public class Quota : BaseDB
    {
        List<QuotaRecord> _OfLeaveType;
        public List<QuotaRecord> OfLeaveType {
            get {
                return this._OfLeaveType == null ? new List<QuotaRecord>() : this._OfLeaveType;
            }
            set { this._OfLeaveType = value; }
        }
        public string PersonNo { get; set; }

        int _Year;
        public int Year { get { return _Year; } set { _Year = value; _ComputeDates(); } }

        int _MonthStart = 1;
        int _MonthVacationStart = 1;

        void _ComputeDates()
        {
            if (DateTime.Now.Month >= _MonthStart)
                _QuotaBegin = _ComputeQuotaBeginDate(_Year, _MonthStart);
            else
                _QuotaBegin = _ComputeQuotaBeginDate(_Year - 1, _MonthStart);
            _QuotaUntil = _QuotaBegin.AddMonths(12).AddDays(-1);

            if (DateTime.Now.Month >= _MonthVacationStart)
                _QuotaVacationBegin = _ComputeQuotaBeginDate(_Year, _MonthVacationStart);
            else
                _QuotaVacationBegin = _ComputeQuotaBeginDate(_Year - 1, _MonthVacationStart);
            _QuotaVacationUntil = _QuotaVacationBegin.AddMonths(12).AddDays(-1);
        }

        DateTime _ComputeQuotaBeginDate(int Year, int Month)
        {
            return new DateTime(Year, Month, 1);
        }

        DateTime _QuotaBegin;
        public DateTime QuotaBegin { get { return _QuotaBegin; } }

        DateTime _QuotaUntil;
        public DateTime QuotaUntil { get { return _QuotaUntil; } }

        DateTime _QuotaVacationBegin;
        public DateTime QuotaVacationBegin { get { return _QuotaVacationBegin; } }

        DateTime _QuotaVacationUntil;
        public DateTime QuotaVacationUntil { get { return _QuotaVacationUntil; } }

        /// <summary>
        /// จำนวนวันลาที่ลาไปแล้วใน 30 วันย้อนหลังนับจาก System Date
        /// </summary>
        public decimal LeaveDaysStat30
        {
            get
            {
                if (this._OfLeaveType == null) return 0;
                int mins = 0;
                foreach (QuotaRecord rec in this._OfLeaveType)
                    mins += rec.Days30Minutes;
                var ds = TimeSpan.FromMinutes(mins);
                return Convert.ToDecimal(ds.TotalDays);
            }
        }

        public Quota(IPrincipal User, string PersonNo)
            : this(User, PersonNo, DateTime.Now.Year)
        {
        }

        public Quota(IPrincipal User, string PersonNo, int Year)
            : base(User)
        {
            this.PersonNo = PersonNo;
            this._Year = Year;
            this._MonthStart = Const.QUOTA_MONTHSTART;
            this._MonthVacationStart = Const.QUOTA_VACATION_MONTHSTART;
            this._ComputeDates();
            if (!string.IsNullOrEmpty(PersonNo))
                this._OfLeaveType = this.Query(PersonNo, Year, false);
        }

        /// <summary>
        /// Get ค่าของ Property ของ QuotaRecord class ที่ตรงกับประเภทการลาที่ระบุ เช่น
        /// decimal QuotaSickDays = (decimal) q.Get("SICK", "QuotaAmount");
        /// </summary>
        /// <param name="TypeNo"></param>
        /// <param name="PropertyName"></param>
        /// <returns>null ถ้าไม่มีข้อมูล Quota หรือไม่พบ Property ที่ระบุ</returns>
        public object GetValue(string TypeNo, string PropertyName)
        {
            if (this._OfLeaveType == null) return null;
            object val = null;
            Type t = typeof(QuotaRecord);
            PropertyInfo p = t.GetProperty(PropertyName);
			if (p == null)
			{
				t = typeof(TypeRecord);
				p = t.GetProperty(PropertyName);
			}
            if (p == null) return val;
			foreach (QuotaRecord rec in this._OfLeaveType)
				if (rec.LeaveType.TypeNo.Equals(TypeNo))
				{
					object obj = t == typeof(QuotaRecord) ? rec : (object)rec.LeaveType;
					val = p.GetValue(obj, null);
					break;
				}
            return val;
        }

        public QuotaRecord GetRecord(string TypeNo)
        {
            if (this._OfLeaveType == null) return null;
            foreach (QuotaRecord rec in this._OfLeaveType)
                if (rec.LeaveType.TypeNo.Equals(TypeNo))
                    return rec;
            return null;
        }

        /// <summary>
        /// ตรวจสอบ(แบบหยวนๆ)ว่า Quota ของประเภทการลาที่ระบุเหลือ 0 หรือน้อยกว่าหรือไม่
        /// </summary>
        /// <param name="TypeNo"></param>
        /// <param name="Year"></param>
        /// <returns></returns>
        public bool IsBalanceZero(string TypeNo, int Year)
        {
            return this.IsBalanceZero(TypeNo, Year, true);
        }

        /// <summary>
        /// ตรวจสอบว่า Quota ของประเภทการลาที่ระบุเหลือ 0 หรือน้อยกว่าหรือไม่
        /// </summary>
        /// <param name="TypeNo"></param>
        /// <param name="Year"></param>
        /// <param name="GraceFully">True คือ หยวนให้พนักงานกรณียังไม่มีระบุโควต้า</param>
        /// <returns></returns>
        public bool IsBalanceZero(string TypeNo, int Year, bool GraceFully)
        {
            bool isZero = false;
            List<QuotaRecord> list = this.OfLeaveType;
            if (this.Year != Year)
                list = this.Query(this.PersonNo, Year);
            int n = 0;
            foreach (QuotaRecord rec in list)
            {
                n++;
                if (rec.LeaveType.TypeNo.Equals(TypeNo))
                {
                    isZero = (0 >= rec.QuotaAmount + rec.QuotaPrevAmount);
                    break;
                }
            }
            if (n == 0 && GraceFully == true) return false;
            return isZero;
        }

        /// <summary>
        /// เช็คว่าโควต้าที่เหลืออยู่พอสำหรับที่จะลา days วันหรือไม่
        /// </summary>
        /// <param name="TypeNo"></param>
        /// <param name="Year"></param>
        /// <param name="Minutes"></param>
        /// <returns></returns>
        public bool IsQuotaExceeded(string TypeNo, int Year, int Minutes)
        {
            bool isExceeded = false;
            List<QuotaRecord> list = this.OfLeaveType;
            if (this.Year != Year)
                list = this.Query(this.PersonNo, Year);
            foreach (QuotaRecord rec in list)
            {
                if (rec.LeaveType.TypeNo.Equals(TypeNo))
                {
                    var RemainMinutes = (rec.QuotaAmount * Const.DEFAULT_WORKHOURS_OF_DAY * 60)
                                      + (rec.QuotaPrevAmount * Const.DEFAULT_WORKHOURS_OF_DAY * 60)
                                      - rec.TakenMinutes;
                    isExceeded = (Minutes > RemainMinutes);
                    break;
                }
            }
            return isExceeded;
        }

        public List<QuotaRecord> Query(string PersonNo, int Year)
        {
            return this.Query(PersonNo, Year, false);
        }

        public List<QuotaRecord> Query(string PersonNo, int Year, bool TakenOnly)
        {
            List<QuotaRecord> OfLeaveType = new List<QuotaRecord>();

            string ACTIVE_STATUSES = string.Join(",", Array.ConvertAll<int, string>(Const.LEAVE_STATUS_ACTIVE(), Convert.ToString));
            string TAKEN_STATUSES = string.Join(",", Array.ConvertAll<int, string>(Const.LEAVE_STATUS_TAKEN(), Convert.ToString));

            DateTime? StartingDate = null;
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    #region Getting Starting Date of Emp
                    cmd.CommandTimeout = this.CommandTimeout;
                    cmd.CommandType = CommandType.Text;
                    //cmd.CommandText = "SELECT [Starting Date] FROM [HR Employee] with(readuncommitted) WHERE [Person No]=@person AND @today BETWEEN [Starting Date] AND [Until Date]";
                    // ดึงวันที่เริ่มงานของรหัสล่าสุด เผื่อกรณีพนักงานที่ลาออกแล้วด้วย
                    cmd.CommandText = "SELECT [Starting Date] FROM [HR Employee] with(readuncommitted) WHERE [Person No]=@person ORDER BY [Starting Date] DESC";
                    cmd.Parameters.Add("@person", SqlDbType.VarChar).Value = PersonNo;
                    //cmd.Parameters.Add("@today", SqlDbType.DateTime).Value = DateTime.Now;
                    object val = cmd.ExecuteScalar();
                    if (val is DBNull) val = null;
                    StartingDate = (DateTime?)val;
                    #endregion

                    if (!StartingDate.HasValue)
                        throw new Exception("Starting date is empty.");

                    #region Getting Quota
					DateTime EffectiveDate = new LeaveRequestEffectiveVacationException(PersonNo, (DateTime)StartingDate, Const.QUOTA_MINMONTHS4VACATION).EffectiveDate;
					string SubQueryForData =
                          " SELECT     typesub.[Type No], typesub.[Reference No], typesub.[Display Order], "
                        + "			   typesub.[Type Sub ID], 0 AS [Period ID], typesub.[TH Name], typesub.[EN Name], lvtype.Category, "
                        + "            SUM(typesub.[Max Days Per Year]) AS lvt_quota,  "
                        + "            0 AS lvt_quotaex, "
                        + "            SUM(ISNULL(lvtk.takenmins, 0)) AS lvt_takenmins, "
                        + "			   SUM(ISNULL(lvtk.approvemins, 0)) AS lvt_approvemins, "
                        + "            SUM(ISNULL(lvtk.approvemins_for30, 0)) AS lvt_approvemins_for30 "
                        + "  FROM      [LV Type] AS lvtype with(readuncommitted) INNER JOIN "
                        + "            [LV Type Sub] AS typesub with(readuncommitted) ON lvtype.[Type No] = typesub.[Type No] "
                        + "            LEFT OUTER JOIN "
                        + "            ( "
                        + "             SELECT @year AS taken_year,l.[Type Sub ID], "
                        + "				 SUM(l.[Total Minutes]) AS takenmins, SUM(l.[Approve Minutes]) AS approvemins, "
                        + "              SUM(CASE WHEN DATEADD(dd,30,l.[Leave Date])>=@sysdate THEN l.[Approve Minutes] ELSE 0 END) AS approvemins_for30 "
                        + "             FROM [LV Leave] l with(readuncommitted) INNER JOIN [LV Type Sub] t with(readuncommitted) ON l.[Type Sub ID]=t.[Type Sub ID] WHERE l.[Person No]=@perno "
                        + "              AND ((t.[Reference No]='" + Const.TYPE_NO_VACATION + "' AND l.[Leave Date] between @qtvcbegin and @qtvcuntil) "
                        + "                OR (t.[Reference No]<>'" + Const.TYPE_NO_VACATION + "' AND l.[Leave Date] between @qtbegin and @qtuntil))  "
                        + "              AND l.[Status ID] IN (" + (TakenOnly ? TAKEN_STATUSES : ACTIVE_STATUSES) + ")  "
                        + "              AND NOT EXISTS(SELECT 1 FROM [LV Veto] with(readuncommitted) WHERE [Leave ID]=l.[Leave ID] AND [Action Status]=" + Const.VETO_INTERRUPTED + ") "
                        + "             GROUP BY l.[Type Sub ID] "
                        + "            ) lvtk ON typesub.[Type Sub ID] = lvtk.[Type Sub ID] "
                        + "  WHERE     (lvtype.Active = 1) AND (typesub.Active = 1) "
                        + "  GROUP BY  typesub.[Type No], typesub.[Reference No], typesub.[Type Sub ID], "
                        + "			   typesub.[TH Name], typesub.[EN Name], lvtype.Category, typesub.[Display Order] ";

                    cmd.CommandText =
                         "SELECT t.[Reference No] AS [Type No], "
						+ "		 t.[Type Sub ID], t.[Period ID], t.[TH Name], t.[EN Name], "
                        + "      t.Category, t.[Display Order], t.lvt_takenmins, t.lvt_approvemins, t.lvt_approvemins_for30, "
                        + "      CASE  "
                        + "        WHEN @year<@stayear "
                        + "          THEN 0 "
                        + "        WHEN t.[Reference No]<>'" + Const.TYPE_NO_VACATION + "' AND @stadate between @qtbegin and @qtuntil "
                        + "          THEN CEILING(lvt_quota-(lvt_quota*datediff(month,@qtbegin,@stadate)/12.0)) "
                        + "        WHEN t.[Reference No]='" + Const.TYPE_NO_VACATION + "' "
						+ "          THEN (CASE WHEN @year<@effyear THEN 0 WHEN @effdate between @qtvcbegin and @qtvcuntil "
                        + "                 THEN CEILING(lvt_quota-(lvt_quota*datediff(month,@qtvcbegin,@effdate)/12.0)) "
						+ "                 ELSE lvt_quota END) "
                        + "        ELSE lvt_quota END AS lvt_quota, lvt_quotaex "
                        + "FROM ( " + SubQueryForData + ") t INNER JOIN [HR Person] p with(readuncommitted) ON p.[Person No] = @perno "
                        + "ORDER BY t.[Display Order], t.[Type No] ";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add("@stadate", SqlDbType.Date).Value = StartingDate.Value.Date;
                    cmd.Parameters.Add("@stayear", SqlDbType.Int).Value = StartingDate.Value.Year;
                    //cmd.Parameters.Add("@stamonth", SqlDbType.Int).Value = StartingDate.Value.Month;
                    cmd.Parameters.Add("@effyear", SqlDbType.Int).Value = EffectiveDate.Year;
                    cmd.Parameters.Add("@effmonth", SqlDbType.Int).Value = EffectiveDate.Month;
                    cmd.Parameters.Add("@effdate", SqlDbType.DateTime).Value = EffectiveDate.Date;
                    //cmd.Parameters.Add("@curdate", SqlDbType.DateTime).Value = Year > DateTime.Now.Year ? QuotaBegin.Date : DateTime.Now.Date;
                    cmd.Parameters.Add("@qtbegin", SqlDbType.DateTime).Value = QuotaBegin.Date;
                    cmd.Parameters.Add("@qtuntil", SqlDbType.DateTime).Value = QuotaUntil.Date;
                    cmd.Parameters.Add("@qtvcbegin", SqlDbType.DateTime).Value = QuotaVacationBegin.Date;
                    cmd.Parameters.Add("@qtvcuntil", SqlDbType.DateTime).Value = QuotaVacationUntil.Date;
                    cmd.Parameters.Add("@sysdate", SqlDbType.DateTime).Value = DateTime.Now.Date;
                    //cmd.Parameters.Add("@curyear", SqlDbType.Int).Value = DateTime.Now.Year;
                    cmd.Parameters.Add("@year", SqlDbType.Int).Value = Year;
                    cmd.Parameters.Add("@qtbusiness", SqlDbType.Int).Value = Const.QUOTA_BUSINESS_INIT_DAYS;
                    cmd.Parameters.Add("@qtsick", SqlDbType.Int).Value = Const.QUOTA_SICK_INIT_DAYS;
                    cmd.Parameters.Add("@qtvocation", SqlDbType.Int).Value = Const.QUOTA_VACATION_INIT_DAYS;
                    cmd.Parameters.Add("@perno", SqlDbType.VarChar).Value = PersonNo == null ? DBNull.Value : (object)PersonNo;
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        int i;
                        QuotaRecord rec;
                        TypeRecord type;
                        while (rs.Read())
                        {
                            type = new TypeRecord();
                            i = rs.GetOrdinal("Type No");
                            type.TypeNo = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("Type Sub ID");
                            type.TypeSubID = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
                            i = rs.GetOrdinal("TH Name");
                            type.NameTH = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("EN Name");
                            type.NameEN = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("Category");
                            type.Category = rs.IsDBNull(i) ? null : rs.GetString(i);

                            rec = new QuotaRecord();
                            rec.LeaveType = type;
                            rec.PersonNo = PersonNo;
                            i = rs.GetOrdinal("Period ID");
                            rec.PeriodID = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
                            i = rs.GetOrdinal("lvt_takenmins");
							rec.TakenMinutes = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
							i = rs.GetOrdinal("lvt_approvemins");
							rec.ApproveMinutes = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
                            i = rs.GetOrdinal("lvt_approvemins_for30");
                            rec.Days30Minutes = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
                            i = rs.GetOrdinal("lvt_quota");
                            rec.QuotaAmount = rs.IsDBNull(i) ? 0 : Convert.ToDecimal(rs.GetValue(i));
                            i = rs.GetOrdinal("lvt_quotaex");
                            rec.QuotaPrevAmount = rs.IsDBNull(i) ? 0 : Convert.ToDecimal(rs.GetValue(i));

                            OfLeaveType.Add(rec);
                        }
                    }
                    #endregion
                }
            }

            return OfLeaveType;
        }

        public void SetQuota(decimal QuotaThisYear, decimal TransferedQuota)
        {
            this.SetQuota(this.PersonNo, this.Year, QuotaThisYear, TransferedQuota);
        }

        public void SetQuota(string PersonNo, int Year, decimal QuotaThisYear, decimal TransferedQuota)
        {
            throw new Exception("ยังไม่ได้ Implement");
        }
    }
}
