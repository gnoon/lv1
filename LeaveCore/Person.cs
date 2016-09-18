using System;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Security.Principal;

namespace LeaveCore
{
    public class Person : BaseDB, IDisposable
    {
        public string PersonNo { get; set; }
        private PersonRecord _Record;
        public PersonRecord Record { get { return this._Record; } }

        private Quota _Quota;
        public Quota Quota { get { return this._Quota; } }

        private Holidays _Holidays;
        public Holidays Holidays { get { return this._Holidays; } }

        private Weekends _Weekends;
        public Weekends Weekends { get { return this._Weekends; } }

        private Workshifts _Workshifts;
        public Workshifts Workshifts { get { return this._Workshifts; } }

        private List<GrantorRecord> _Grantors;
        public List<GrantorRecord> Grantors { get { return this._Grantors; } }

		private List<PersonRecord> _Vetoes;
        public List<PersonRecord> Vetoes { get { return this._Vetoes; } }

        public Person(IPrincipal User, string PersonNo)
            : this(User, PersonNo, false)
        {
        }

        public Person(IPrincipal User, string PersonNo, bool LoadProfileOnly)
            : base(User)
        {
            this.Update(PersonNo, LoadProfileOnly);
        }

        public Person Update(string PersonNo, bool LoadProfileOnly)
        {
            this.PersonNo = PersonNo;
            if (this._Quota != null) this._Quota.OfLeaveType.Clear(); this._Quota = null;
            if (this._Holidays != null) this._Holidays.Dates.Clear(); this._Holidays = null;
            if (this._Weekends != null) this._Weekends.Rules.Clear(); this._Weekends = null;
            if (this._Workshifts != null) this._Workshifts.Times.Clear(); this._Workshifts = null;
			if (this._Grantors != null) this._Grantors.Clear(); this._Grantors = null;
			if (this._Vetoes != null) this._Vetoes.Clear(); this._Vetoes = null;

            if (string.IsNullOrEmpty(PersonNo)) return this;

            this._Record = this.Query(PersonNo, null, null).DefaultIfEmpty(null).FirstOrDefault();
            if (!LoadProfileOnly)
            {
                this._Quota = new Quota(User, PersonNo);
                this._Holidays  = new Holidays(User, PersonNo);
                this._Weekends  = new Weekends(User, PersonNo);
                this._Workshifts = new Workshifts(User, PersonNo);
				this._Grantors = new Grantors(User, PersonNo).Heads;
				this._Vetoes = new Vetoes(User, PersonNo).Heads;
			}

            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="PersonNo"></param>
        /// <param name="Year"></param>
        /// <param name="ConnRead">null ถ้าต้องการให้เปิด Connection ใหม่</param>
        /// <returns></returns>
        protected List<PersonRecord> Query(string PersonNo, int? Year, SqlConnection ConnRead)
        {
            SqlCommand cmd;
            SqlDataReader rs;
            return Query(PersonNo, Year, false, out cmd, out rs, ConnRead);
        }

        /// <summary>
        /// Query ข้อมูล Person & Employee โดยเอาเฉพาะข้อมูล Employee ตามปีที่ระบุ หรือ ล่าสุดของพนักงาน
        /// </summary>
        /// <param name="PersonNo">null หากต้องการดึงข้อมูลพนักงานทุกคน</param>
        /// <param name="Year">null หากต้องการดึงข้อมูลปีล่าสุด</param>
        /// <param name="ReadManually">ถ้า true จะ return null แต่จะได้ Cmd และ Reader ไป Control เอง</param>
        /// <param name="Cmd"></param>
        /// <param name="Reader"></param>
        /// <param name="ConnRead">ส่ง null หาก CloseManually เป็น false</param>
        protected List<PersonRecord> Query(string PersonNo, int? Year, bool ReadManually, out SqlCommand Cmd, out SqlDataReader Reader, SqlConnection ConnRead)
        {
            bool InternalClose = (ConnRead == null || ConnRead.State != ConnectionState.Open);
            SqlConnection _Conn = null;
            if (InternalClose)
            {
                _Conn = GetConnection();
                _Conn.Open();
            }
            else
            {
                _Conn = ConnRead;
            }

            try
            {
                Cmd = _Conn.CreateCommand();
                Cmd.CommandType = CommandType.Text;
                Cmd.Parameters.Add("@perno", SqlDbType.VarChar).Value = PersonNo == null ? DBNull.Value : (object)PersonNo;
                Cmd.Parameters.Add("@year", SqlDbType.Int).Value = !Year.HasValue ? DBNull.Value : (object)Year.Value;

                string WhereYear = string.Empty;
                if (Year.HasValue)
                    WhereYear = " AND (@year BETWEEN YEAR(em.[Starting Date]) AND YEAR(em.[Until Date]) OR YEAR(em.[Starting Date]) > @year) ";

                #region Getting The Latest Information of Employee
                Cmd.CommandText =
                        "SELECT  ps.[Person No], em.[Employee No], em.[Starting Date], em.[Until Date], ps.[TH Prefix], "
                    + "       ps.[EN Prefix], ps.[TH First Name], ps.[EN First Name], ps.[TH Last Name], ps.[EN Last Name], "
                    + "       ps.[E-Mail], ps.[Mobile No], em.[Company Code], em.Department, em.Section, em.[TH Position], "
                    + "       em.[EN Position], ps.[AD Account], ps.[Gender], ps.[Use Password Default], "
					+ "		  CAST(DECRYPTBYPASSPHRASE('',ps.[Password],0,null) as VARCHAR) AS [Password] "
                    + "FROM   [HR Person] AS ps LEFT OUTER JOIN "
                    + "         [HR Employee] AS em INNER JOIN ("
                    + "             SELECT em.[Person No], MAX(em.[Starting Date]) AS [Starting Date] "
                    + "             FROM [HR Employee] em WHERE em.[Person No] = ISNULL(@perno, em.[Person No]) " + WhereYear
                    + "             GROUP BY em.[Person No] "
                    + "         ) latest ON em.[Person No]=latest.[Person No] AND em.[Starting Date]=latest.[Starting Date] "
                    + "         ON ps.[Person No] = em.[Person No] "
                    + "WHERE  ps.[Person No] = ISNULL(@perno, ps.[Person No]) " + WhereYear
                    + "ORDER BY em.[Starting Date]";
                Reader = Cmd.ExecuteReader();
                if (!ReadManually)
                {
                    List<PersonRecord> list = new List<PersonRecord>();
                    using (Reader)
                    {
                        while (Reader.Read())
                        {
                            list.Add(this.SetReaderValue(Reader, new PersonRecord()));
                        }
                    }
                    Cmd.Dispose();
                    return list;
                }
                return null;
                #endregion
            }
            finally
            {
                if (InternalClose && !ReadManually)
                    _Conn.Dispose();
            }
        }

        private PersonRecord SetReaderValue(SqlDataReader rs, PersonRecord rec)
        {
            rec.Employee = new EmployeeRecord();
            rec.PersonNo = rs.GetValue<string>("Person No");
            rec.PrefixTH = rs.GetValue<string>("TH Prefix");
            rec.PrefixEN = rs.GetValue<string>("EN Prefix");
            rec.NameFirstTH = rs.GetValue<string>("TH First Name");
            rec.NameFirstEN = rs.GetValue<string>("EN First Name");
            rec.NameLastTH = rs.GetValue<string>("TH Last Name");
            rec.NameLastEN = rs.GetValue<string>("EN Last Name");
            rec.Email = rs.GetValue<string>("E-Mail");
			rec.Mobile = rs.GetValue<string>("Mobile No");
			rec.Gender = rs.GetValue<string>("Gender");
            rec.Employee.EmployeeNo = rs.GetValue<string>("Employee No");
            rec.Employee.StartingDate = rs.GetValue<DateTime?>("Starting Date");
            rec.Employee.UntilDate = rs.GetValue<DateTime?>("Until Date");
            rec.Employee.CompanyCode = rs.GetValue<string>("Company Code");
            rec.Employee.Department = rs.GetValue<string>("Department");
            rec.Employee.Section = rs.GetValue<string>("Section");
            rec.Employee.PositionTH = rs.GetValue<string>("TH Position");
            rec.Employee.PositionEN = rs.GetValue<string>("EN Position");

            rec.ADAccount = rs.GetValue<string>("AD Account");
            rec.Password = rs.GetValue<string>("Password");
            rec.UsePasswordDefault = rs.GetValue<int>("Use Password Default");

            return rec;
        }

        #region Static Functions
        /// <summary>
        /// ดึงข้อมูล Person &amp; Employee โดยเอาเฉพาะข้อมูล Employee ล่าสุดของพนักงาน
        /// </summary>
        /// <param name="User">User ที่ login เข้าระบบ HR online</param>
        /// <param name="PersonNo">null หากต้องการดึงข้อมูลพนักงานทุกคน</param>
        /// <param name="Conn">null หากต้องการให้เปิด Connection ใหม่</param>
        /// <returns></returns>
        public static PersonRecord GetInfo(IPrincipal User, string PersonNo, SqlConnection Conn)
        {
            if (string.IsNullOrEmpty(PersonNo))
                return null;
            var person = new Person(User, null, true);
            return person.Query(PersonNo, null, Conn).DefaultIfEmpty(null).FirstOrDefault();
        }

		/// <summary>
		/// Genetare "PersonNo" จาก ชื่อ - สกุล
		/// </summary>
		/// <param name="User"></param>
		/// <param name="FirstName">ชื่อ</param>
		/// <param name="LastName">สกุล</param>
		/// <returns>PersonNo</returns>
		public static string GenPersonNo(IPrincipal User, string FirstName, string LastName)
		{
			if (string.IsNullOrEmpty(FirstName) || string.IsNullOrEmpty(LastName))
				return null;
			BaseDB db = new BaseDB(User);
			return db.ExecuteScalar<string>(string.Format("SELECT CONVERT(NVARCHAR(32),HashBytes('MD5',LTRIM(RTRIM('{0}'))+LTRIM(RTRIM('{1}'))),2)", FirstName, LastName), null);
		}

		/// <summary>
		/// ตรวจสอบ "PersonNo" ว่ามีอยู่ในระบบแล้วหรือไม่
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <returns></returns>
		public static bool CheckPersonNo(IPrincipal User, string PersonNo)
		{
            if (string.IsNullOrEmpty(PersonNo))
                return false;
			int Count;
			BaseDB db = new BaseDB(User);
			Count = db.ExecuteScalar<int>(string.Format("SELECT COUNT(*) FROM [HR Employee] WHERE [Person No]='{0}'", PersonNo), null);
			return Count > 0;
		}

		/// <summary>
		/// เพิ่มพนักงานใหม่ ตาราง [HR Person], [HR Employee]
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <param name="EmployeeNo"></param>
		/// <param name="PrefixTH"></param>
		/// <param name="NameFirstTH"></param>
		/// <param name="NameLastTH"></param>
		/// <param name="Gender"></param>
		/// <param name="Email"></param>
		/// <param name="Password"></param>
		/// <param name="StartingDate"></param>
		/// <param name="UntilDate"></param>
		/// <param name="PositionTH"></param>
		/// <param name="CompanyCode"></param>
		/// <param name="Department"></param>
		/// <param name="Section"></param>
		/// <returns></returns>
		public static int New(IPrincipal User, string PersonNo, string EmployeeNo, string PrefixTH, string NameFirstTH, string NameLastTH, string Gender, string Email, string Password, DateTime StartingDate, DateTime UntilDate, string PositionTH, string CompanyCode, string Department, string Section)
		{
			// ตรวจสอบ PersonNo ที่ได้จากการ Generate
			if (string.IsNullOrEmpty(PersonNo))
				throw new Exception("PERSONNO_IS_UNDEFINED");
			
			SqlTransaction t = null;
			Person Person = new Person(User, PersonNo, true);
			using (SqlConnection conn = Person.GetConnection())
			{
				conn.Open();
                t = conn.BeginTransaction();
				try
				{
					int lineCount = 0;
					// เพิ่มข้อมูลในตาราง [HR Person]
					using (SqlCommand cmd = conn.CreateCommand())
					{
						// กรณีพนักงานใหม่
						if (Person.Record == null)
						{
                            cmd.Transaction = t;
                            #region Perform DB Execution Preparation
                            cmd.CommandType = CommandType.Text;
							cmd.CommandText = "INSERT INTO [HR Person] " +
								"        ([Person No], [TH Prefix], [TH First Name], [TH Last Name], " +
								"         [EN Prefix], [EN First Name], [EN Last Name], [Birth Date], " +
								"         [Address], [Address2], [City], [Post Code], [Country Code], " +
								"         [Phone No], [Mobile No], [E-Mail], [Social Security No], [Tax No], " +
								"         [Personal No], [AD Account], [Gender], [Use Password Default], [Password]) " +
								"VALUES  (@PersonNo, @PrefixTH, @NameFirstTH, @NameLastTH, @PrefixEN, @NameFirstEN, @NameLastEN, " +
								"         @BirthDate, @Address, @Address2, @City, @PostCode, @CountryCode, @PhoneNo, @MobileNo, " +
								"         @Email, @SocialNo, @TaxNo, @PersonalNo, @ADAccount, @Gender, @UseDefault, " +
								"         ENCRYPTBYPASSPHRASE('', @Password, 0, NULL))";
							cmd.Parameters.Clear();
							cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
							cmd.Parameters.Add("@PrefixTH", SqlDbType.VarChar).Value = PrefixTH;
							cmd.Parameters.Add("@NameFirstTH", SqlDbType.VarChar).Value = NameFirstTH;
							cmd.Parameters.Add("@NameLastTH", SqlDbType.VarChar).Value = NameLastTH;
							cmd.Parameters.Add("@PrefixEN", SqlDbType.VarChar).Value = PrefixTH;
							cmd.Parameters.Add("@NameFirstEN", SqlDbType.VarChar).Value = NameFirstTH;
							cmd.Parameters.Add("@NameLastEN", SqlDbType.VarChar).Value = NameLastTH;
							cmd.Parameters.Add("@BirthDate", SqlDbType.DateTime).Value = new DateTime(1900,1,1).Date;
							cmd.Parameters.Add("@Address", SqlDbType.VarChar).Value = string.Empty;
							cmd.Parameters.Add("@Address2", SqlDbType.VarChar).Value = string.Empty;
							cmd.Parameters.Add("@City", SqlDbType.VarChar).Value = string.Empty;
							cmd.Parameters.Add("@PostCode", SqlDbType.VarChar).Value = string.Empty;
							cmd.Parameters.Add("@CountryCode", SqlDbType.VarChar).Value = string.Empty;
							cmd.Parameters.Add("@PhoneNo", SqlDbType.VarChar).Value = string.Empty;
							cmd.Parameters.Add("@MobileNo", SqlDbType.VarChar).Value = string.Empty;
							cmd.Parameters.Add("@Email", SqlDbType.VarChar).Value = Email ?? string.Empty;
							cmd.Parameters.Add("@SocialNo", SqlDbType.VarChar).Value = string.Empty;
							cmd.Parameters.Add("@TaxNo", SqlDbType.VarChar).Value = string.Empty;
							cmd.Parameters.Add("@PersonalNo", SqlDbType.VarChar).Value = string.Empty;
							cmd.Parameters.Add("@ADAccount", SqlDbType.VarChar).Value = string.Empty;
							cmd.Parameters.Add("@Gender", SqlDbType.VarChar).Value = Gender ?? string.Empty;
							cmd.Parameters.Add("@Password", SqlDbType.VarChar).Value = Password;
							cmd.Parameters.Add("@UseDefault", SqlDbType.Int).Value = 1;
                            #endregion
                            lineCount += cmd.ExecuteNonQuery();
						}
						// กรณีมีชื่ออยู่ในระบบแล้ว (เปลี่ยนสังกัด, โอนย้าย)
						else
						{
							cmd.Transaction = t;
                            #region Perform DB Execution Preparation
                            cmd.CommandType = CommandType.Text;
							cmd.CommandText = "UPDATE [HR Person] SET " +
								" [TH Prefix]=@PrefixTH " +
								",[TH First Name]=@NameFirstTH " +
								",[TH Last Name]=@NameLastTH " +
								",[EN Prefix]=@PrefixEN " +
								",[EN First Name]=@NameFirstEN " +
								",[EN Last Name]=@NameFirstEN " +
								",[E-Mail]=@Email " +
								",[Gender]=@Gender " +
								",[Use Password Default]=@UseDefault " +
								",[Password]=ENCRYPTBYPASSPHRASE('', @Password, 0, NULL) " +
								"WHERE [Person No]=@PersonNo";
							cmd.Parameters.Clear();
							cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
							cmd.Parameters.Add("@PrefixTH", SqlDbType.VarChar).Value = PrefixTH;
							cmd.Parameters.Add("@NameFirstTH", SqlDbType.VarChar).Value = NameFirstTH;
							cmd.Parameters.Add("@NameLastTH", SqlDbType.VarChar).Value = NameLastTH;
							cmd.Parameters.Add("@PrefixEN", SqlDbType.VarChar).Value = PrefixTH;
							cmd.Parameters.Add("@NameFirstEN", SqlDbType.VarChar).Value = NameFirstTH;
							cmd.Parameters.Add("@NameLastEN", SqlDbType.VarChar).Value = NameLastTH;
							cmd.Parameters.Add("@Email", SqlDbType.VarChar).Value = string.IsNullOrEmpty(Email)
								? Person.Record.Email : Email;
							cmd.Parameters.Add("@Gender", SqlDbType.VarChar).Value = string.IsNullOrEmpty(Gender)
								? Person.Record.Gender : Gender;
							cmd.Parameters.Add("@Password", SqlDbType.VarChar).Value = Password;
							cmd.Parameters.Add("@UseDefault", SqlDbType.Int).Value = 1;
                            #endregion
                            lineCount += cmd.ExecuteNonQuery();
						}
					}
					// เพิ่มข้อมูลในตาราง [HR Employee]
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = t;
                        #region Perform DB Execution Preparation
                        cmd.CommandType = CommandType.Text;
						cmd.CommandText = "INSERT INTO [HR Employee] " +
							"        ([Employee No], [Person No], [Starting Date], [Until Date], " +
							"         [Company Code], [Department], [Section], [TH Position], [EN Position]) " +
							"VALUES  (@EmployeeNo, @PersonNo, @StartingDate, @UntilDate, " +
							"         @CompanyCode, @Department, @Section, @PositionTH, @PositionEN)";
						cmd.Parameters.Clear();
						cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
						cmd.Parameters.Add("@EmployeeNo", SqlDbType.VarChar).Value = EmployeeNo;
						cmd.Parameters.Add("@StartingDate", SqlDbType.DateTime).Value = StartingDate.Date;
						cmd.Parameters.Add("@UntilDate", SqlDbType.DateTime).Value = UntilDate.Date;
						cmd.Parameters.Add("@CompanyCode", SqlDbType.VarChar).Value = CompanyCode ?? string.Empty;
						cmd.Parameters.Add("@Department", SqlDbType.VarChar).Value = Department ?? string.Empty;
						cmd.Parameters.Add("@Section", SqlDbType.VarChar).Value = Section ?? string.Empty;
						cmd.Parameters.Add("@PositionTH", SqlDbType.VarChar).Value = PositionTH ?? string.Empty;
						cmd.Parameters.Add("@PositionEN", SqlDbType.VarChar).Value = PositionTH ?? string.Empty;
                        #endregion
                        cmd.ExecuteNonQuery();
                    }
					// ดึงรายการวันหยุดประจำปี จาก Templates ในตาราง [LV Holiday]
					try
					{
						using (SqlCommand cmd = conn.CreateCommand())
						{
							cmd.Transaction = t;
							#region Perform DB Execution Preparation
							cmd.CommandType = CommandType.Text;
							cmd.CommandText = "INSERT INTO [LV Profile Holiday] " +
								"        ([Person No], [Holiday Date], [TH Name], [EN Name], [Remark], [Modify Date], [Modify Person]) " +
								"(SELECT  @PersonNo, [Holiday Date], [TH Name], [EN Name], @Remark, @ModifyDate, @ModifyPerson " +
								" FROM    [LV Holiday] WHERE YEAR([Holiday Date]) = CASE WHEN @Year < @Now THEN @Now ELSE @Year END)";
							cmd.Parameters.Clear();
							cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
							cmd.Parameters.Add("@Now", SqlDbType.Int).Value = DateTime.Now.Year;
							cmd.Parameters.Add("@Year", SqlDbType.Int).Value = StartingDate.Year;
							cmd.Parameters.Add("@Remark", SqlDbType.VarChar).Value = string.Empty;
							cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
							cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
							#endregion
							cmd.ExecuteNonQuery();
						}
					}
					catch {}

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
		/// เปลี่ยนแปลงข้อมูลพนักงาน ตาราง [HR Person], [HR Employee]
		/// </summary>
		/// <param name="User"></param>
		/// <param name="PersonNo"></param>
		/// <param name="OldEmployeeNo">รหัสเดิม</param>
		/// <param name="NewEmployeeNo">รหัสใหม่ [OldEmployeeNo = NewEmployeeNo กรณีไม่มีการเปลี่ยนแปลง]</param>
		/// <param name="PrefixTH"></param>
		/// <param name="NameFirstTH"></param>
		/// <param name="NameLastTH"></param>
		/// <param name="Gender"></param>
		/// <param name="Email"></param>
		/// <param name="Password"></param>
		/// <param name="StartingDate"></param>
		/// <param name="UntilDate"></param>
		/// <param name="PositionTH"></param>
		/// <param name="CompanyCode"></param>
		/// <param name="Department"></param>
		/// <param name="Section"></param>
		/// <returns></returns>
		public static int Change(IPrincipal User, string PersonNo, string OldEmployeeNo, string NewEmployeeNo, string PrefixTH, string NameFirstTH, string NameLastTH, string Gender, string Email, string Password, DateTime StartingDate, DateTime UntilDate, string PositionTH, string CompanyCode, string Department, string Section)
		{
			// ดึงข้อมูลจากรายการเดิม
			Person Person = new Person(User, PersonNo, true);
			if (Person.Record == null)
				throw new Exception("PERSON_IS_UNDEFINED");
			
			SqlTransaction t = null;
			using (SqlConnection conn = Person.GetConnection())
			{
				conn.Open();
                t = conn.BeginTransaction();
				try
				{
					int lineCount = 0;
					// เปลี่ยนแปลงข้อมูลในตาราง [HR Person]
					using (SqlCommand cmd = conn.CreateCommand())
					{
                        cmd.Transaction = t;
                        #region Perform DB Execution Preparation
                        cmd.CommandType = CommandType.Text;
						cmd.CommandText = "UPDATE [HR Person] SET " +
							" [TH Prefix]=@PrefixTH " +
							",[TH First Name]=@NameFirstTH " +
							",[TH Last Name]=@NameLastTH " +
							",[EN Prefix]=@PrefixEN " +
							",[EN First Name]=@NameFirstEN " +
							",[EN Last Name]=@NameFirstEN " +
							",[E-Mail]=@Email " +
							",[Gender]=@Gender " +
							",[Password]=ENCRYPTBYPASSPHRASE('', @Password, 0, NULL) " +
							"WHERE [Person No]=@PersonNo";
						cmd.Parameters.Clear();
						cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
						cmd.Parameters.Add("@PrefixTH", SqlDbType.VarChar).Value = PrefixTH;
						cmd.Parameters.Add("@NameFirstTH", SqlDbType.VarChar).Value = NameFirstTH;
						cmd.Parameters.Add("@NameLastTH", SqlDbType.VarChar).Value = NameLastTH;
						cmd.Parameters.Add("@PrefixEN", SqlDbType.VarChar).Value = PrefixTH;
						cmd.Parameters.Add("@NameFirstEN", SqlDbType.VarChar).Value = NameFirstTH;
						cmd.Parameters.Add("@NameLastEN", SqlDbType.VarChar).Value = NameLastTH;
						cmd.Parameters.Add("@Email", SqlDbType.VarChar).Value = Email;
						cmd.Parameters.Add("@Gender", SqlDbType.VarChar).Value = Gender;
						cmd.Parameters.Add("@Password", SqlDbType.VarChar).Value = Password;
                        #endregion
                        lineCount += cmd.ExecuteNonQuery();
					}
					// เปลี่ยนแปลงข้อมูลในตาราง [HR Employee]
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = t;
                        #region Perform DB Execution Preparation
                        cmd.CommandType = CommandType.Text;
						cmd.CommandText = "UPDATE [HR Employee] SET " +
							" [Employee No]=@NewEmployeeNo " +
							",[Starting Date]=@StartingDate " +
							",[Until Date]=@UntilDate " +
							",[Company Code]=@CompanyCode " +
							",[Department]=@Department " +
							",[Section]=@Section " +
							",[TH Position]=@PositionTH " +
							",[EN Position]=@PositionEN " +
							"WHERE [Person No]=@PersonNo AND [Employee No]=@OldEmployeeNo";
						cmd.Parameters.Clear();
						cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
						cmd.Parameters.Add("@OldEmployeeNo", SqlDbType.VarChar).Value = OldEmployeeNo;
						cmd.Parameters.Add("@NewEmployeeNo", SqlDbType.VarChar).Value = NewEmployeeNo;
						cmd.Parameters.Add("@StartingDate", SqlDbType.DateTime).Value = StartingDate.Date;
						cmd.Parameters.Add("@UntilDate", SqlDbType.DateTime).Value = UntilDate.Date;
						cmd.Parameters.Add("@CompanyCode", SqlDbType.VarChar).Value = CompanyCode ?? string.Empty;
						cmd.Parameters.Add("@Department", SqlDbType.VarChar).Value = Department ?? string.Empty;
						cmd.Parameters.Add("@Section", SqlDbType.VarChar).Value = Section ?? string.Empty;
						cmd.Parameters.Add("@PositionTH", SqlDbType.VarChar).Value = PositionTH ?? string.Empty;
						cmd.Parameters.Add("@PositionEN", SqlDbType.VarChar).Value = PositionTH ?? string.Empty;
                        #endregion
                        cmd.ExecuteNonQuery();
                    }
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

        //static Person _Person;
        //static SqlConnection _Conn;
        //static SqlCommand _Command;
        //static SqlDataReader _Reader;
        ///// <summary>
        ///// ดึงรายการข้อมูล Person & Employee โดยเอาเฉพาะข้อมูล Employee ตามปีที่ระบุ หรือ ล่าสุดของพนักงาน
        ///// </summary>
        ///// <param name="User">User ที่ login เข้าระบบ HR online</param>
        ///// <param name="PersonNo">null หากต้องการดึงข้อมูลพนักงานทุกคน</param>
        ///// <param name="Year">null หากต้องการดึงข้อมูลปีล่าสุด</param>
        ///// <returns>Instance ID ของ Person object</returns>
        //public static string List(IPrincipal User, int? Year)
        //{
        //    if (_Reader != null && !_Reader.IsClosed)
        //        _Reader.Close();
        //    _Person = new Person(User, null, true);
        //    _Conn = _Person.GetConnection();
        //    _Person.Query(null, Year, true, out _Command, out _Reader, _Conn);
        //    return _Person.InstanceID;
        //}
        //public static PersonRecord Next(string InstanceID)
        //{
        //    if (_Reader == null || _Reader.IsClosed)
        //        return null;
        //    if (_Person == null || !_Person.InstanceID.Equals(InstanceID))
        //        return null;
        //    if (_Reader.Read())
        //        return _Person.SetReaderValue(_Reader, new PersonRecord());
        //    _Reader.Close();
        //    _Reader = null;
        //    _Command.Dispose();
        //    _Conn.Dispose();
        //    return null;
        //}

        static Dictionary<string, Person> _Sessions = new Dictionary<string, Person>();
        SqlConnection Connection;
        SqlCommand Command;
        SqlDataReader Reader;

        public void Dispose()
        {
            try
            {
                if (Reader != null) Reader.Close();
                Reader = null;
            }
            catch { }
            try
            {
                if (Command != null) Command.Dispose();
                Command = null;
            }
            catch { }
            try
            {
                if (Connection != null) Connection.Dispose();
                Connection = null;
            }
            catch { }
            lock (typeof(Person))
            {
                if (_Sessions.ContainsKey(InstanceID))
                {
                    _Sessions.Remove(InstanceID);
                }
            }
        }
        /// <summary>
        /// ดึงรายการข้อมูล Person & Employee โดยเอาเฉพาะข้อมูล Employee ตามปีที่ระบุ หรือ ล่าสุดของพนักงาน
        /// </summary>
        /// <param name="User">User ที่ login เข้าระบบ HR online</param>
        /// <param name="PersonNo">null หากต้องการดึงข้อมูลพนักงานทุกคน</param>
        /// <param name="Year">null หากต้องการดึงข้อมูลปีล่าสุด</param>
        /// <returns>Instance ID ของ Person object</returns>
        public static string List(IPrincipal User, int? Year)
        {
            Person _Person = new Person(User, null, true);
            lock (typeof(Person))
            {
                _Sessions.Add(_Person.InstanceID, _Person);
            }
            _Person.Connection = _Person.GetConnection();
            _Person.Query(null, Year, true, out _Person.Command, out _Person.Reader, _Person.Connection);
            return _Person.InstanceID;
        }
        public static PersonRecord Next(string InstanceID)
        {
            Person _Person = null;
            SqlDataReader _Reader = null;
            lock (typeof(Person))
            {
                if (_Sessions.ContainsKey(InstanceID))
                {
                    _Person = _Sessions[InstanceID];
                    _Reader = _Person.Reader;
                }
            }
            if (_Person == null || _Reader == null || _Reader.IsClosed)
                return null;
            if (_Reader.Read())
                return _Person.SetReaderValue(_Reader, new PersonRecord());
            _Person.Dispose();
            return null;
        }

		/// <summary>
		/// ดึงรายการข้อมูล Person & Employee โดยเอาเฉพาะข้อมูลพนักงานในสังกัด
		/// </summary>
		/// <param name="User">User ที่ login เข้าระบบ HR online</param>
		/// <param name="PersonNo">รหัสหัวหน้างาน</param>
		/// <returns></returns>
		public static List<PersonRecord> ListUnderling(IPrincipal User, string PersonNo)
		{
			BaseDB db = new BaseDB(User);
			List<PersonRecord> list = new List<PersonRecord>();
			using (SqlConnection conn = db.GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					#region Query
					cmd.CommandText =
							"SELECT  ps.[Person No], em.[Employee No], em.[Starting Date], em.[Until Date], ps.[TH Prefix], "
						+ "       ps.[EN Prefix], ps.[TH First Name], ps.[EN First Name], ps.[TH Last Name], ps.[EN Last Name], "
						+ "       ps.[E-Mail], ps.[Mobile No], em.[Company Code], em.Department, em.Section, "
						+ "       em.[TH Position], em.[EN Position] "
						+ "FROM   [HR Person] AS ps INNER JOIN "
						+ "         [HR Employee] AS em INNER JOIN ("
						+ "             SELECT [Person No], MAX([Starting Date]) AS [Starting Date] "
						+ "             FROM [HR Employee] WHERE [Person No] = [Person No] "
						+ "                 AND @now BETWEEN [Starting Date] AND [Until Date] "
						+ "             GROUP BY [Person No] "
						+ "         ) latest ON em.[Person No]=latest.[Person No] AND em.[Starting Date]=latest.[Starting Date] "
						+ "         ON ps.[Person No] = em.[Person No] "
						+ "WHERE  EXISTS ( "
						+ "         SELECT 1 FROM [LV Profile Grantor] WHERE [Head Person No]=@perno AND [Person No]=ps.[Person No] "
						+ ")"
						+ "GROUP BY ps.[Person No], em.[Employee No], em.[Starting Date], em.[Until Date], ps.[TH Prefix], "
						+ "       ps.[EN Prefix], ps.[TH First Name], ps.[EN First Name], ps.[TH Last Name], ps.[EN Last Name], "
						+ "       ps.[E-Mail], ps.[Mobile No], em.[Company Code], em.Department, em.Section, "
						+ "       em.[TH Position], em.[EN Position] "
						+ "ORDER BY em.[Starting Date]";
						cmd.Parameters.Clear();
						cmd.Parameters.Add("@now", SqlDbType.DateTime).Value = DateTime.Now;
						cmd.Parameters.Add("@perno", SqlDbType.VarChar).Value = PersonNo;
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
							rec.Email = rs.GetValue<string>("E-Mail");
							rec.Mobile = rs.GetValue<string>("Mobile No");
							rec.Employee.EmployeeNo = rs.GetValue<string>("Employee No");
							rec.Employee.StartingDate = rs.GetValue<DateTime?>("Starting Date");
							rec.Employee.UntilDate = rs.GetValue<DateTime?>("Until Date");
							rec.Employee.CompanyCode = rs.GetValue<string>("Company Code");
							rec.Employee.Department = rs.GetValue<string>("Department");
							rec.Employee.Section = rs.GetValue<string>("Section");
							rec.Employee.PositionTH = rs.GetValue<string>("TH Position");
							rec.Employee.PositionEN = rs.GetValue<string>("EN Position");

							list.Add(rec);
						}
					}
					#endregion
				}
			}

			return list;
		}

		/// <summary>
		/// เปลี่ยนแปลงข้อมูลผู้ใช้งานระบบ
		/// </summary>
		/// <param name="User">User ที่ login เข้าระบบ HR online</param>
		/// <param name="PersonNo">รหัสพนักงาน</param>
		/// <param name="Email">E-mail พนักงาน</param>
		/// <param name="Password">รหัสผ่าน 4 หลัก</param>
		/// <param name="UsePasswordDefault">เปิดใช้งานรหัสผ่าน 4 หลัก</param>
		/// <returns></returns>
		public static int SetUserAccount(IPrincipal User, string PersonNo, string Email, string Password, int UsePasswordDefault)
		{
			int result;
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
			        cmd.CommandText = "UPDATE [HR Person] SET " +
			            " [E-Mail]=@Email " +
                        ",[Password]=ENCRYPTBYPASSPHRASE('',@Password,0,NULL) " +
			            ",[Use Password Default]=@UsePasswordDefault " +
			            "WHERE [Person No]=@PersonNo";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
			        cmd.Parameters.Add("@Email", SqlDbType.VarChar).Value = Email ?? string.Empty;
			        cmd.Parameters.Add("@Password", SqlDbType.VarChar, 20).Value = Password ?? string.Empty;
			        cmd.Parameters.Add("@UsePasswordDefault", SqlDbType.VarChar).Value = UsePasswordDefault;
			        result = cmd.ExecuteNonQuery();
			    }
			}
			if (result > 0)
			{
				try
				{
					SetProfileVeto(User, PersonNo, Email);
					SetProfileGrantor(User, PersonNo, Email);
				}
				catch { }
			}
			return result;
		}

		/// <summary>
		/// เมื่อมีการเปลี่ยนแปลงข้อมูลส่วนตัว ให้ Update ที่ตาราง Veto ด้วย
		/// </summary>
		/// <param name="User">User ที่ login เข้าระบบ HR online</param>
		/// <param name="PersonNo">รหัสพนักงาน</param>
		/// <param name="Email">E-mail พนักงาน</param>
		protected static void SetProfileVeto(IPrincipal User, string PersonNo, string Email)
		{
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
                    cmd.CommandText = "UPDATE [LV Profile Veto] SET " +
                        " [Head E-Mail]=@Email " +
                        ",[Modify Date]=@ModifyDate " +
                        ",[Modify Person]=@ModifyPerson " +
                        "WHERE [Head Person No]=@PersonNo";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
			        cmd.Parameters.Add("@Email", SqlDbType.VarChar).Value = Email;
                    cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
					cmd.ExecuteNonQuery();
			    }
			}
		}

		/// <summary>
		/// เมื่อมีการเปลี่ยนแปลงข้อมูลส่วนตัว ให้ Update ที่ตาราง Grantor ด้วย
		/// </summary>
		/// <param name="User">User ที่ login เข้าระบบ HR online</param>
		/// <param name="PersonNo">รหัสพนักงาน</param>
		/// <param name="Email">E-mail พนักงาน</param>
		protected static void SetProfileGrantor(IPrincipal User, string PersonNo, string Email)
		{
			BaseDB db = new BaseDB(User);
			using (SqlConnection conn = db.GetConnection())
			{
			    conn.Open();
			    using (SqlCommand cmd = conn.CreateCommand())
			    {
                    cmd.CommandText = "UPDATE [LV Profile Grantor] SET " +
                        " [Head E-Mail]=@Email " +
                        ",[Modify Date]=@ModifyDate " +
                        ",[Modify Person]=@ModifyPerson " +
                        "WHERE [Head Person No]=@PersonNo";
			        cmd.Parameters.Clear();
			        cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = PersonNo;
			        cmd.Parameters.Add("@Email", SqlDbType.VarChar).Value = Email;
                    cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name ?? string.Empty;
					cmd.ExecuteNonQuery();
			    }
			}
		}

        #endregion
    }
}
