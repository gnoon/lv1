using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Security.Principal;

namespace LeaveCore
{
    public class Vetoes : BaseDB
    {
		private List<PersonRecord> _Heads;
        public List<PersonRecord> Heads
        {
            get
            {
                return this._Heads == null ? new List<PersonRecord>() : this._Heads;
            }
            set { this._Heads = value; }
        }
        public string PersonNo { get; set; }

		public Vetoes(IPrincipal User, string PersonNo)
            : base(User)
        {
            this.PersonNo = PersonNo;
            if(!string.IsNullOrEmpty(PersonNo))
                this._Heads = this.Query(PersonNo);
        }

        protected List<PersonRecord> Query(string PersonNo)
        {
            List<PersonRecord> list = new List<PersonRecord>();
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                List<string[]> Vetoes = new List<string[]>();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    #region Getting Vetoes of Emp
                    cmd.CommandText =
                          "SELECT  [Head Person No],[Head E-Mail],[Head Mobile] FROM [LV Profile Veto] WITH(READUNCOMMITTED) "
                        + "WHERE   [Person No] = @perno ORDER BY [Person No]";
                    cmd.Parameters.Add("@perno", SqlDbType.VarChar).Value = PersonNo == null ? DBNull.Value : (object)PersonNo;
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        while (rs.Read())
                        {
                            Vetoes.Add(new string[] {
                                rs.GetValue<string>("Head Person No"),
                                rs.GetValue<string>("Head E-Mail"),
                                rs.GetValue<string>("Head Mobile")
                            });
                        }
                    }
                    #endregion
                }
                PersonRecord rec;
                foreach (string[] arr in Vetoes)
                {
                    // Get Employee Information of Veto
                    rec = Person.GetInfo(User, arr[0], conn);
                    // Override Email & Mobile
                    rec.Email = arr[1];
                    rec.Mobile = arr[2];
                    list.Add(rec);
                }
            }
            return list;
        }

		#region Static Functions

		/// <summary>
		/// สร้างรายการ ผู้ระงับลา
		/// </summary>
		/// <param name="User">User ที่ Login เข้าระบบ</param>
		/// <param name="PersonNo">พนักงาน</param>
		/// <param name="HeadPersonNo">ผู้ระงับลา</param>
		/// <returns>จำนวน record</returns>
		public static int AddVetoes(IPrincipal User, string PersonNo, string HeadPersonNo)
		{
			int result;
			BaseDB db = new BaseDB(User);
			PersonRecord Head = Person.GetInfo(User, HeadPersonNo, null);
			using (SqlConnection conn = db.GetConnection())
			{
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO [LV Profile Veto] " +
                        "        ([Person No], [Head Person No], [Head E-Mail],  " +
						"         [Head Mobile], [Modify Date], [Modify Person]) " +
                        "VALUES  (@PersonNo, @HeadPersonNo, @HeadEmail, @HeadMobile, @ModifyDate, @ModifyPerson)";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
                    cmd.Parameters.Add("@HeadPersonNo", SqlDbType.VarChar).Value = HeadPersonNo;
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
		/// แก้ไขรายการ ผู้ระงับลา
		/// </summary>
		/// <param name="User"></param>
		/// <param name="id">รายการเดิม</param>
		/// <param name="PersonNo"></param>
		/// <param name="HeadPersonNo">รายการปัจจุบัน</param>
		/// <returns></returns>
		public static int EditVetoes(IPrincipal User, string id, string PersonNo, string HeadPersonNo)
		{
			int result;
			BaseDB db = new BaseDB(User);
			PersonRecord Head = Person.GetInfo(User, HeadPersonNo, null);
			using (SqlConnection conn = db.GetConnection())
			{
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE [LV Profile Veto] SET " +
                        " [Head Person No]=@HeadPersonNo " +
                        ",[Head E-Mail]=@HeadEmail " +
                        ",[Head Mobile]=@HeadMobile " +
                        ",[Modify Date]=@ModifyDate " +
                        ",[Modify Person]=@ModifyPerson " +
                        "WHERE [Person No]=@PersonNo AND [Head Person No]=@id";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add("@id", SqlDbType.VarChar).Value = id;
                    cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
                    cmd.Parameters.Add("@HeadPersonNo", SqlDbType.VarChar).Value = HeadPersonNo;
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
		/// ลบรายการ ผู้ระงับลา
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <param name="HeadPersonNo"></param>
		/// <returns></returns>
		public static int DeleteVetoes(IPrincipal User, string PersonNo, string HeadPersonNo)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM [LV Profile Veto] " +
                        "WHERE [Person No]=@PersonNo AND [Head Person No]=@HeadPersonNo";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
                    cmd.Parameters.Add("@HeadPersonNo", SqlDbType.VarChar).Value = HeadPersonNo;
                    result = cmd.ExecuteNonQuery();
                }
			}
			return result;
		}

		#endregion
    }
}
