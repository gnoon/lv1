using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Security.Principal;

namespace LeaveCore
{
    public class Grantors : BaseDB
    {
        private List<GrantorRecord> _Heads;
        public List<GrantorRecord> Heads
        {
            get
            {
                return this._Heads == null ? new List<GrantorRecord>() : this._Heads;
            }
            set { this._Heads = value; }
        }
        public string PersonNo { get; set; }
		protected GrantorRecord Record;

        public Grantors(IPrincipal User, string PersonNo)
            : base(User)
        {
            this.PersonNo = PersonNo;
            if(!string.IsNullOrEmpty(PersonNo))
                this._Heads = this.Query(PersonNo);
        }

        protected List<GrantorRecord> Query(string PersonNo)
        {
            return Query(PersonNo, null);
        }

        protected List<GrantorRecord> Query(string PersonNo, string HeadPersonNo)
        {
            List<GrantorRecord> list = new List<GrantorRecord>();
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();

                string Company = null;
                if (!string.IsNullOrWhiteSpace(PersonNo))
                {
                    Company = this.ExecuteScalar<string>(string.Format(
                     "SELECT [Company Code] FROM [HR Employee] WHERE [Person No]='{0}' ORDER BY [Starting Date] DESC",
                     PersonNo.Replace("'", "''")), conn);
                }

                using (SqlCommand cmd = conn.CreateCommand())
                {
                    #region Getting Grantors of Emp
                    //cmd.CommandText =
                    //      "SELECT  gt.[Person No],gt.[Head Person No], "
                    //    + "		   gt.[Priority],gt.[Head E-Mail],gt.[Head Mobile],gt.[Modify Date],gt.[Modify Person], "
                    //    + "        ps.[TH Prefix],ps.[TH First Name],ps.[TH Last Name],ps.[EN First Name],ps.[EN Last Name],ps.[EN Prefix], "
                    //    + "        em.[Employee No], em.[Company Code], em.Department, em.Section "
                    //    + "FROM    [LV Profile Grantor] gt WITH(READUNCOMMITTED) INNER JOIN "
                    //    + "        [HR Person] ps WITH(READUNCOMMITTED) ON gt.[Head Person No] = ps.[Person No] LEFT OUTER JOIN "
                    //    + "          [HR Employee] AS em INNER JOIN ("
                    //    + "             SELECT [Person No], MIN([Employee No]) AS [Employee No] "
                    //    + "             FROM [HR Employee] WHERE [Person No] = ISNULL(@headno, [Person No]) "
                    //    + "             AND [Company Code] = ISNULL(@comp, [Company Code]) GROUP BY [Person No] "
                    //    + "          ) latest ON em.[Person No]=latest.[Person No] "
                    //    + "			 ON ps.[Person No] = em.[Person No] and em.[Employee No] = latest.[Employee No] "
                    //    + "WHERE   (gt.[Person No] = @perno) OR (gt.[Head Person No] = @headno) "
                    //    + "ORDER BY gt.[Priority]";
                    cmd.CommandText =
                          "SELECT  gt.[Person No],gt.[Head Person No], "
                        + "		   gt.[Priority],gt.[Head E-Mail],gt.[Head Mobile],gt.[Modify Date],gt.[Modify Person], "
                        + "        ps.[TH Prefix],ps.[TH First Name],ps.[TH Last Name],ps.[EN First Name],ps.[EN Last Name],ps.[EN Prefix] "
                        + "FROM    [LV Profile Grantor] gt WITH(READUNCOMMITTED) INNER JOIN "
                        + "        [HR Person] ps WITH(READUNCOMMITTED) ON gt.[Head Person No] = ps.[Person No] "
                        + "WHERE   (gt.[Person No] = @perno) OR (gt.[Head Person No] = @headno) "
                        + "ORDER BY gt.[Priority]";
                    cmd.Parameters.Add("@perno", SqlDbType.VarChar).Value = PersonNo == null ? DBNull.Value : (object)PersonNo;
                    cmd.Parameters.Add("@headno", SqlDbType.VarChar).Value = HeadPersonNo == null ? DBNull.Value : (object)HeadPersonNo;
                    cmd.Parameters.Add("@comp", SqlDbType.VarChar).Value = Company == null ? DBNull.Value : (object)Company;
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        int i;
                        GrantorRecord rec;
                        while (rs.Read())
                        {
                            rec = new GrantorRecord();
							rec.Employee = new EmployeeRecord();
                            i = rs.GetOrdinal("Person No");
                            rec.PersonNo = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("Head Person No");
                            rec.HeadPersonNo = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("TH Prefix");
                            rec.HeadPrefixTH = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("EN Prefix");
                            rec.HeadPrefixEN = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("TH First Name");
                            rec.HeadNameFirstTH = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("EN First Name");
                            rec.HeadNameFirstEN = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("TH Last Name");
                            rec.HeadNameLastTH = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("EN Last Name");
                            rec.HeadNameLastEN = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("Priority");
                            rec.Priority = rs.IsDBNull(i) ? -1 : Convert.ToInt32(rs.GetValue(i));
                            i = rs.GetOrdinal("Head E-Mail");
                            rec.HeadEmail = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("Head Mobile");
                            rec.HeadMobile = rs.IsDBNull(i) ? null : rs.GetString(i);
                            i = rs.GetOrdinal("Modify Date");
                            rec.ModifyDate = rs.IsDBNull(i) ? null : (DateTime?)rs.GetDateTime(i);
                            i = rs.GetOrdinal("Modify Person");
                            rec.ModifyPerson = rs.IsDBNull(i) ? null : rs.GetString(i);

                            //i = rs.GetOrdinal("Employee No");
                            //rec.Employee.EmployeeNo = rs.IsDBNull(i) ? null : rs.GetString(i);
                            //i = rs.GetOrdinal("Company Code");
                            //rec.Employee.CompanyCode = rs.IsDBNull(i) ? null : rs.GetString(i);
                            //i = rs.GetOrdinal("Department");
                            //rec.Employee.Department = rs.IsDBNull(i) ? null : rs.GetString(i);
                            //i = rs.GetOrdinal("Section");
                            //rec.Employee.Section = rs.IsDBNull(i) ? null : rs.GetString(i);

                            list.Add(rec);
                        }
                    }
                    #endregion
                }

                //string Temp;
                //string[] Arr;
                //foreach (var rec in list)
                //{
                //    if (string.IsNullOrWhiteSpace(rec.Employee.EmployeeNo))
                //    {
                //        Temp = this.ExecuteScalar<string>(string.Format(
                //         "SELECT ISNULL([Employee No],'')+'|'+ISNULL([Company Code],'')+'|'+ISNULL([Department],'')+'|'+ISNULL([Section],'') " +
                //         "FROM [HR Employee] WHERE [Person No]='{0}' ORDER BY [Starting Date] DESC",
                //         rec.PersonNo.Replace("'", "''")), conn);
                //        if (Temp != null)
                //        {
                //            Arr = Temp.Split('|');
                //            if (Arr.Length == 4)
                //            {
                //                rec.Employee.EmployeeNo = Arr[0];
                //                rec.Employee.CompanyCode = Arr[1];
                //                rec.Employee.Department = Arr[2];
                //                rec.Employee.Section = Arr[3];
                //            }
                //        }
                //    }
                //}
                PersonRecord head;
                foreach (var rec in list)
                {
                    if (string.IsNullOrWhiteSpace(rec.Employee.EmployeeNo))
                    {
                        head = Person.GetInfo(User, rec.HeadPersonNo, conn);
                        if (head != null)
                        {
                            rec.Employee = head.Employee;
                        }
                    }
                }
            }

            return list;
        }

        #region Static Functions

        public static GrantorRecord GetGrantor(IPrincipal User, string HeadPersonNo)
        {
            Grantors gs = new Grantors(User, HeadPersonNo);
            List<GrantorRecord> list = gs.Query(null, HeadPersonNo);
            return list.Count == 0 ? null : list[0];
        }

        /// <summary>
        /// ค่าในฟิลด์ Grant.[Grant Step ID] ขึ้นอยู่กับการเปลี่ยนสถานะปัจจุบัน
        /// </summary>
        /// <param name="CurrentStatusID"></param>
        /// <param name="NewStatusID"></param>
        /// <returns></returns>
        public static int GetGrantStep(int CurrentStatusID, int NewStatusID)
        {
            switch (NewStatusID)
            {
                case Const.STATUS_LEAVE_PENDING_APPROVAL:
                case Const.STATUS_LEAVE_AWAITING:
                    return Const.STATUS_GRANTSTEP_PENDING_APPROVAL;
            }
            if (NewStatusID == Const.STATUS_LEAVE_CANCELLED)
            {
                switch (CurrentStatusID)
                {
                    case Const.STATUS_LEAVE_PENDING_APPROVAL:
                    case Const.STATUS_LEAVE_AWAITING:
                        return Const.STATUS_GRANTSTEP_PENDING_APPROVAL;
                }
            }
            return Const.STATUS_GRANTSTEP_APPROVED;
        }

        /// <summary>
        /// ค่าในฟิลด์ Grant.[Grant Date] ขึ้นอยู่กับการเปลี่ยนสถานะปัจจุบัน
        /// </summary>
        /// <param name="CurrentStatusID"></param>
        /// <param name="NewStatusID"></param>
        /// <returns></returns>
        public static DateTime? GetGrantDate(int CurrentStatusID, int NewStatusID)
        {
            switch (NewStatusID)
            {
                case Const.STATUS_LEAVE_PENDING_APPROVAL:
                case Const.STATUS_LEAVE_AWAITING:
                    return null;
            }
            if (NewStatusID == Const.STATUS_LEAVE_CANCELLED)
            {
                switch (CurrentStatusID)
                {
                    case Const.STATUS_LEAVE_PENDING_APPROVAL:
                    case Const.STATUS_LEAVE_AWAITING:
                        return null;
                }
            }
            return DateTime.Now;
        }

        /// <summary>
        /// ค่าในฟิลด์ Grant.[Cancel Grant Step ID] ขึ้นอยู่กับการเปลี่ยนสถานะปัจจุบัน
        /// </summary>
        /// <param name="CurrentStatusID"></param>
        /// <param name="NewStatusID"></param>
        /// <returns></returns>
        public static int? GetGrantCancelStep(int CurrentStatusID, int NewStatusID)
        {
            switch (NewStatusID)
            {
                case Const.STATUS_LEAVE_CANCELREQUEST:
                    return Const.STATUS_GRANTSTEP_PENDING_APPROVAL;
                case Const.STATUS_LEAVE_CANCELAPPROVED:
                case Const.STATUS_LEAVE_CANCELREJECTED:
                    return Const.STATUS_GRANTSTEP_APPROVED;
            }
            return null;
        }

        /// <summary>
        /// ค่าในฟิลด์ Grant.[Cancel Grant Date] ขึ้นอยู่กับการเปลี่ยนสถานะปัจจุบัน
        /// </summary>
        /// <param name="CurrentStatusID"></param>
        /// <param name="NewStatusID"></param>
        /// <returns></returns>
        public static DateTime? GetGrantCancelDate(int CurrentStatusID, int NewStatusID)
        {
            switch (NewStatusID)
            {
                case Const.STATUS_LEAVE_CANCELAPPROVED:
                case Const.STATUS_LEAVE_CANCELREJECTED:
                    return DateTime.Now;
            }
            return null;
        }

		/// <summary>
		/// สร้างรายการ ผู้อนุมัติลา
		/// </summary>
		/// <param name="User">User ที่ Login เข้าระบบ</param>
		/// <param name="PersonNo">พนักงาน</param>
		/// <param name="HeadPersonNo">ผู้อนุมัติลา</param>
		/// <param name="Priority">ลำดับอนุมัติ</param>
		/// <returns>จำนวน record</returns>
		public static int AddGrantor(IPrincipal User, string PersonNo, string HeadPersonNo, int Priority)
		{
			int result;
			BaseDB db = new BaseDB(User);
			PersonRecord Head = Person.GetInfo(User, HeadPersonNo, null);
			using (SqlConnection conn = db.GetConnection())
			{
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO [LV Profile Grantor] " +
                        "        ([Person No], [Head Person No], [Priority], " +
						"         [Head E-Mail], [Head Mobile], [Modify Date], [Modify Person]) " +
                        "VALUES  (@PersonNo, @HeadPersonNo, @Priority, @HeadEmail, @HeadMobile, @ModifyDate, @ModifyPerson)";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
                    cmd.Parameters.Add("@HeadPersonNo", SqlDbType.VarChar).Value = HeadPersonNo;
                    cmd.Parameters.Add("@Priority", SqlDbType.Int).Value = Priority;
                    cmd.Parameters.Add("@HeadEmail", SqlDbType.VarChar).Value = Head.Email ?? string.Empty;
                    cmd.Parameters.Add("@HeadMobile", SqlDbType.VarChar).Value = Head.Mobile ?? string.Empty;
                    cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
                    result = cmd.ExecuteNonQuery();
                }
			}
			return result;
		}

		/// <summary>
		/// แก้ไขรายการ ผู้อนุมัติลา
		/// </summary>
		/// <param name="User"></param>
		/// <param name="id">รายการเดิม</param>
		/// <param name="PersonNo"></param>
		/// <param name="HeadPersonNo">รายการปัจจุบัน</param>
		/// <param name="Priority"></param>
		/// <returns></returns>
		public static int EditGrantor(IPrincipal User, string id, string PersonNo, string HeadPersonNo, int Priority)
		{
			int result;
			BaseDB db = new BaseDB(User);
			PersonRecord Head = Person.GetInfo(User, HeadPersonNo, null);
			using (SqlConnection conn = db.GetConnection())
			{
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE [LV Profile Grantor] SET " +
                        " [Head Person No]=@HeadPersonNo " +
                        ",[Head E-Mail]=@HeadEmail " +
                        ",[Head Mobile]=@HeadMobile " +
                        ",[Modify Date]=@ModifyDate " +
                        ",[Modify Person]=@ModifyPerson " +
                        "WHERE [Person No]=@PersonNo AND [Priority]=@Priority AND [Head Person No]=@id";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add("@id", SqlDbType.VarChar).Value = id;
                    cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
                    cmd.Parameters.Add("@HeadPersonNo", SqlDbType.VarChar).Value = HeadPersonNo;
                    cmd.Parameters.Add("@Priority", SqlDbType.Int).Value = Priority;
                    cmd.Parameters.Add("@HeadEmail", SqlDbType.VarChar).Value = Head.Email ?? string.Empty;
                    cmd.Parameters.Add("@HeadMobile", SqlDbType.VarChar).Value = Head.Mobile ?? string.Empty;
                    cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
                    result = cmd.ExecuteNonQuery();
                }
			}
			return result;
		}

		/// <summary>
		/// ลบรายการ ผู้อนุมัติลา
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <param name="HeadPersonNo"></param>
		/// <param name="Priority"></param>
		/// <returns></returns>
		public static int DeleteGrantor(IPrincipal User, string PersonNo, string HeadPersonNo, int Priority)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM [LV Profile Grantor] " +
                        "WHERE [Person No]=@PersonNo AND [Priority]=@Priority AND [Head Person No]=@HeadPersonNo";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
                    cmd.Parameters.Add("@HeadPersonNo", SqlDbType.VarChar).Value = HeadPersonNo;
                    cmd.Parameters.Add("@Priority", SqlDbType.Int).Value = Priority;
                    result = cmd.ExecuteNonQuery();
                }
			}
			return result;
		}

        #endregion
    }
}
