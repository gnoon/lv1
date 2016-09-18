using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Security.Principal;
using System.Reflection;

namespace LeaveCore
{
    public class Holidays : BaseDB
    {
        private List<HolidayRecord> _Dates;
        public List<HolidayRecord> Dates
        {
            get
            {
                return this._Dates == null ? new List<HolidayRecord>() : this._Dates;
            }
            set { this._Dates = value; }
        }
        public string PersonNo { get; set; }

        public Holidays(IPrincipal User, string PersonNo)
            : base(User)
        {
            this.PersonNo = PersonNo;
            if(!string.IsNullOrEmpty(PersonNo))
                this._Dates = this.Query(PersonNo);
        }

        public List<HolidayRecord> Query(string PersonNo)
        {
            List<HolidayRecord> list = new List<HolidayRecord>();
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    #region Getting Holidays of Emp
                    cmd.CommandText =
                          "SELECT   [Person No],[Holiday Date],[TH Name],[EN Name],[Remark],[Modify Date],[Modify Person] "
                        + "FROM     [LV Profile Holiday] WITH(READUNCOMMITTED) "
                        + "WHERE    YEAR([Holiday Date]) BETWEEN YEAR(GETDATE()) AND YEAR(GETDATE()) + 1 "
                        + "    AND  [Person No] = @perno "
                        + "ORDER BY [Holiday Date]";
                    cmd.Parameters.Add("@perno", SqlDbType.VarChar).Value = PersonNo == null ? DBNull.Value : (object)PersonNo;
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        int i;
                        HolidayRecord rec;
                        while (rs.Read())
                        {
                            rec = new HolidayRecord();
                            i = rs.GetOrdinal("Person No");
                            rec.PersonNo = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("Holiday Date");
                            rec.Date = rs.IsDBNull(i) ? null : (DateTime?)rs.GetDateTime(i);
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

		/// <summary>
		/// Get ค่าของ Property ของ HolidayRecord class ที่ตรงกับประเภทการลาที่ระบุ เช่น
		/// string NameTH = (string) h.Get(2013-01-01, "NameTH");
		/// </summary>
		/// <param name="Date"></param>
		/// <param name="PropertyName"></param>
		/// <returns>null ถ้าไม่มีข้อมูล Quota หรือไม่พบ Property ที่ระบุ</returns>
		public object GetValue(DateTime Date, string PropertyName)
		{
			if (this._Dates == null) return null;
			object val = null;
			Type t = typeof(HolidayRecord);
			PropertyInfo p = t.GetProperty(PropertyName);
			if (p == null) return val;
			foreach (HolidayRecord rec in this._Dates)
				if (rec.Date.Value.Date == Date.Date)
				{
					val = p.GetValue(rec, null);
					break;
				}
			return val;
		}

        public bool IsHoliday(DateTime date)
        {
            if (this._Dates == null) return false;
            foreach (HolidayRecord rec in this.Dates)
                if (rec.Date.HasValue)
                    if (date.Date == rec.Date.Value.Date)
                        return true;
            return false;
        }

        public List<KeyValuePair<int, string>> List(string CompanyCode)
        {
            List<KeyValuePair<int, string>> list = new List<KeyValuePair<int, string>>();
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    #region Getting Holidays
                    cmd.CommandText =
                            "SELECT  hs.[Holidays ID], hs.Description "
                        + "FROM    [LV Holidays] AS hs WITH(READUNCOMMITTED) "
                        + "WHERE   (hs.Active = 1) AND (hs.[Company Code] = @com)  "
                        + "ORDER BY hs.[Holidays ID] DESC";
                    cmd.Parameters.Add("@com", SqlDbType.VarChar).Value = CompanyCode == null ? DBNull.Value : (object)CompanyCode;
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        int i, key;
                        string val;
                        KeyValuePair<int, string> rec;
                        while (rs.Read())
                        {
                            i = rs.GetOrdinal("Holidays ID");
                            key = rs.IsDBNull(i) ? 0 : Convert.ToInt32(rs.GetValue(i));
                            i = rs.GetOrdinal("Description");
                            val = rs.IsDBNull(i) ? null : rs.GetString(i);

                            rec = new KeyValuePair<int, string>(key, val);
                            list.Add(rec);
                        }
                    }
                    #endregion
                }
            }

            return list;
        }

        public string[] HolidayDBDates
        {
            get
            {
                if (this._Dates == null) return new string[] { };
                CultureInfo enCulture = new CultureInfo("en-US");
                List<string> list = new List<string>();
                foreach (HolidayRecord rec in this.Dates)
                    if (rec.Date.HasValue)
                        list.Add(string.Format("CONVERT(DATETIME,'{0}',126)"
                            , ((DateTime)rec.Date).ToString("yyyy-MM-ddT00:00:00", enCulture)));
                return list.ToArray();
            }
        }

		#region Static Functions

		/// <summary>
		/// สร้างรายการ วันหยุดประจำปี
		/// </summary>
		/// <param name="User">User ที่ Login เข้าระบบ</param>
		/// <param name="PersonNo">พนักงาน</param>
		/// <param name="Date"></param>
		/// <param name="NameTH"></param>
		/// <param name="NameEN"></param>
		/// <param name="Remark"></param>
		/// <returns>จำนวนรายการ</returns>
		public static int AddHolidays(IPrincipal User, string PersonNo, DateTime Date, string NameTH, string NameEN, string Remark)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "INSERT INTO [LV Profile Holiday] " +
			            "        ([Person No], [Holiday Date], [TH Name], " +
			            "         [EN Name], [Remark], [Modify Date], [Modify Person]) " +
			            "VALUES  (@PersonNo, @HolidayDate, @NameTH, @NameEN, @Remark, @ModifyDate, @ModifyPerson)";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
			        cmd.Parameters.Add("@HolidayDate", SqlDbType.Date).Value = Date.Date;
			        cmd.Parameters.Add("@NameTH", SqlDbType.VarChar).Value = NameTH ?? string.Empty;
			        cmd.Parameters.Add("@NameEN", SqlDbType.VarChar).Value = NameEN ?? string.Empty;
			        cmd.Parameters.Add("@Remark", SqlDbType.VarChar).Value = Remark ?? string.Empty;
			        cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
			        cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		/// <summary>
		/// แก้ไขรายการ วันหยุดประจำปี
		/// </summary>
		/// <param name="User"></param>
		/// <param name="id">รายการเดิม</param>
		/// <param name="PersonNo"></param>
		/// <param name="Date">รายการปัจจุบัน</param>
		/// <param name="NameTH"></param>
		/// <param name="NameEN"></param>
		/// <param name="Remark"></param>
		/// <returns>จำนวนรายการ</returns>
		public static int EditHolidays(IPrincipal User, DateTime id, string PersonNo, DateTime Date, string NameTH, string NameEN, string Remark)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "UPDATE [LV Profile Holiday] SET " +
			            " [Holiday Date]=@HolidayDate " +
			            ",[TH Name]=@NameTH " +
			            ",[EN Name]=@NameEN " +
			            ",[Remark]=@Remark " +
			            ",[Modify Date]=@ModifyDate " +
			            ",[Modify Person]=@ModifyPerson " +
			            "WHERE [Person No]=@PersonNo AND [Holiday Date]=@id";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@id", SqlDbType.Date).Value = id.Date;
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
			        cmd.Parameters.Add("@HolidayDate", SqlDbType.Date).Value = Date.Date;
					cmd.Parameters.Add("@NameTH", SqlDbType.VarChar).Value = NameTH ?? string.Empty;
					cmd.Parameters.Add("@NameEN", SqlDbType.VarChar).Value = NameEN ?? string.Empty;
			        cmd.Parameters.Add("@Remark", SqlDbType.VarChar).Value = Remark ?? string.Empty;
			        cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
			        cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		/// <summary>
		/// ลบรายการ วันหยุดประจำปี
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <param name="Date"></param>
		/// <returns>จำนวนรายการ</returns>
		public static int DeleteHolidays(IPrincipal User, string PersonNo, DateTime Date)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "DELETE FROM [LV Profile Holiday] " +
			            "WHERE [Person No]=@PersonNo AND [Holiday Date]=@HolidayDate";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
			        cmd.Parameters.Add("@HolidayDate", SqlDbType.Date).Value = Date.Date;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			return result;
		}

		#endregion
    }
}
