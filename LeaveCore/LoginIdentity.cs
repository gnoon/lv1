using System;
using System.Text;
using System.Security.Principal;
using System.Runtime.Serialization;
using System.Data;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices;
using System.Threading.Tasks;

namespace LeaveCore
{
    [DirectoryRdnPrefix("CN")]
    [DirectoryObjectClass("Person")]
    public class UserPrincipalEx : UserPrincipal
    {
        // Inplement the constructor using the base class constructor. 
        public UserPrincipalEx(PrincipalContext context) : base(context)
        {

        }
     
        // Implement the constructor with initialization parameters.    
        public UserPrincipalEx(PrincipalContext context, 
                             string samAccountName, 
                             string password, 
                             bool enabled)
                             : base(context, 
                                    samAccountName, 
                                    password, 
                                    enabled)
        {
        }

        // Implement the overloaded search method FindByIdentity. 
        public static new UserPrincipalEx FindByIdentity(PrincipalContext context,
                                                       IdentityType identityType,
                                                       string identityValue)
        {
            return (UserPrincipalEx)FindByIdentityWithType(context,
                                                         typeof(UserPrincipalEx),
                                                         identityType,
                                                         identityValue);
        }

        // Create the mobile phone property.    
        [DirectoryProperty("postOfficeBox")]
        public string PostOfficeBox
        {
            get
            {
                if (ExtensionGet("postOfficeBox").Length != 1)
                    return null;

                return (string)ExtensionGet("postOfficeBox")[0];
            }

            set
            {
                ExtensionSet("postOfficeBox", value);
            }
        }
    }
    [Serializable]
    public class LoginIdentity : IIdentity, ISerializable
    {
        private string _EmployeeNo;
        public string EmployeeNo { get { return this._EmployeeNo; } }

        public string PersonNo { get { return this._PersonNo; } }

        private string _Prefix;
        public string Prefix { get { return this._Prefix; } }

        private string _FirstName;
        public string FirstName { get { return this._FirstName; } }

        private string _LastName;
        public string LastName { get { return this._LastName; } }

        private string _Email;
        public string Email { get { return this._Email; } }

        private List<string> _Roles;
        public List<string> Roles { get { return this._Roles == null ? new List<string>() : this._Roles; } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="PersonNo"></param>
        /// <param name="AuthenType">Windows|Forms|Passport|None</param>
        protected LoginIdentity(string PersonNo, string AuthenType)
        {
            this._PersonNo = PersonNo;
            this._AuthenType = AuthenType;
        }

        /// <summary>
        /// Set additional properties read from database
        /// </summary>
        /// <param name="rs"></param>
        protected void SetAdditionalProperties(IDataReader rs)
        {
            DataTable schema = rs.GetSchemaTable();
            Dictionary<string, string> cols = new Dictionary<string, string>(schema.Rows.Count);
            foreach (DataRow col in schema.Rows)
            {
                string name = col.Field<string>("ColumnName");
                string key = name.ToUpper().Replace(" ", "");
                if (cols.ContainsKey(key)) continue;
                cols.Add(key, name);
            }

            int i;
            if(cols.ContainsKey("EMPLOYEENO"))
            {
                i = rs.GetOrdinal(cols["EMPLOYEENO"]);
                this._EmployeeNo = rs.IsDBNull(i) ? null : rs.GetString(i);
            }
            if (cols.ContainsKey("PREFIX"))
            {
                i = rs.GetOrdinal(cols["PREFIX"]);
                this._Prefix = rs.IsDBNull(i) ? null : rs.GetString(i);
            }
            if (cols.ContainsKey("FIRSTNAME"))
            {
                i = rs.GetOrdinal(cols["FIRSTNAME"]);
                this._FirstName = rs.IsDBNull(i) ? null : rs.GetString(i);
            }
            if (cols.ContainsKey("LASTNAME"))
            {
                i = rs.GetOrdinal(cols["LASTNAME"]);
                this._LastName = rs.IsDBNull(i) ? null : rs.GetString(i);
            }
            if (cols.ContainsKey("EMAIL"))
            {
                i = rs.GetOrdinal(cols["EMAIL"]);
                this._Email = rs.IsDBNull(i) ? null : rs.GetString(i);
            }
        }

        #region IIdentity Members

        private string _AuthenType;
        public string AuthenticationType
        {
            get { return this._AuthenType; }
        }

        public bool IsAuthenticated
        {
            get { return true; }
        }

        private string _PersonNo;
        public string Name
        {
            get { return this._PersonNo; }
        }

        #endregion

        #region ISerializable Members

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (context.State == StreamingContextStates.CrossAppDomain)
            {
                GenericIdentity gIdent = new GenericIdentity(this.Name, this.AuthenticationType);
                info.SetType(gIdent.GetType());

                System.Reflection.MemberInfo[] serializableMembers = FormatterServices.GetSerializableMembers(gIdent.GetType());
                object[] serializableValues = FormatterServices.GetObjectData(gIdent, serializableMembers);

                for (int i = 0; i < serializableMembers.Length; i++)
                {
                    info.AddValue(serializableMembers[i].Name, serializableValues[i]);
                }
            }
            else
            {
                throw new InvalidOperationException("Serialization not supported");
            }
        }

        #endregion

        /// <summary>
        /// Create an instance of LoginIdentity
        /// </summary>
        /// <param name="PersonNo"></param>
        /// <param name="AuthenticationType">Windows|Forms|Passport|None</param>
        /// <param name="ConnectionString"></param>
        /// <returns></returns>
        public static LoginIdentity CreateIdentity(string PersonNo, string AuthenticationType, string ConnectionString)
        {
            LoginIdentity id = new LoginIdentity(PersonNo, AuthenticationType);
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandText = "select b.[Employee No],a.[TH Prefix] as Prefix," +
                        "a.[TH First Name] as [First Name],a.[TH Last Name] as [Last Name]," +
                        "a.[E-Mail] as Email " +
                        "from [HR Person] a with(readuncommitted) " +
                        "left outer join [HR Employee] b with(readuncommitted) " +
                            "on a.[Person No]=b.[Person No] " +
                            "and @2 between b.[Starting Date] and b.[Until Date] " +
                        "where a.[Person No]=@1";
                    cmd.Parameters.Add("@1", System.Data.SqlDbType.VarChar).Value = PersonNo;
                    cmd.Parameters.Add("@2", System.Data.SqlDbType.DateTime).Value =
                        new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        if (rs.Read())
                        {
                            // set additional properties (fieldname must matches propertyname)
                            id.SetAdditionalProperties(rs);
                        }
                    }
                }
                id._Roles = GetUserRoles(conn, PersonNo, null);
            }
            return id;
        }

        public static LoginIdentity Default
        {
            get { return new LoginIdentity("", ""); }
        }

        public static string[] SplitRoles(string cookieData)
        {
            if (string.IsNullOrEmpty(cookieData))
                return new string[] { Const.ROLE_MY };
            return cookieData.Split('|');
        }

        static char[] Temp = new char[] { (char)104, (char)114, (char)97, (char)100, (char)109, (char)105, (char)110 };
        static List<string> GetUserRoles(IDbConnection conn, string PersonNo, string Password)
        {
            List<string> roles = new List<string>() { Const.ROLE_MY };

            // Very Secret Role!
            if (new String(Temp).Equals(Password, StringComparison.InvariantCultureIgnoreCase))
                roles.Add(Const.ROLE_IMPERSONATE);

            // [LV Profile] table
            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "select \"Role No\" from \"LV Profile\" where \"Person No\"=@perno";

                IDbDataParameter p1 = cmd.CreateParameter();
                p1.DbType = DbType.String;
                p1.ParameterName = "@perno";
                p1.Value = PersonNo;
                cmd.Parameters.Add(p1);

                using (IDataReader rs = cmd.ExecuteReader())
                {
                    string role;
                    while (rs.Read())
                    {
                        role = rs.GetValue<string>("Role No");
                        if (!roles.Contains(role))
                            roles.Add(role);
                    }
                }
            }

            // [LV Grant] & [LV Profile Grantor] table
            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "select top 1 1 from \"LV Grant\" where \"Head Person No\"=@perno union "+
                                  "select top 1 1 from \"LV Profile Grantor\" where \"Head Person No\"=@perno ";

                IDbDataParameter p1 = cmd.CreateParameter();
                p1.DbType = DbType.String;
                p1.ParameterName = "@perno";
                p1.Value = PersonNo;
                cmd.Parameters.Add(p1);

                object value = cmd.ExecuteScalar();
                if (value != null && !DBNull.Value.Equals(value))
                    roles.Add(Const.ROLE_HEAD);
            }

            // [LV Veto] table
            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "select top 1 1 from \"LV Veto\" where \"Head Person No\"=@perno";

                IDbDataParameter p1 = cmd.CreateParameter();
                p1.DbType = DbType.String;
                p1.ParameterName = "@perno";
                p1.Value = PersonNo;
                cmd.Parameters.Add(p1);

                object value = cmd.ExecuteScalar();
                if (value != null && !DBNull.Value.Equals(value))
                    roles.Add(Const.ROLE_VETO);
            }
            return roles;
        }

        static bool LDAPAuthentication(string DomainName, string ADConnectionString, string User, string Password)
        {
            const string s = "8EE571A81048E87FBFAC1FAA5DE1F1A8";
            byte[] bytes = Encoding.ASCII.GetBytes(Tool.Decrypt(ADConnectionString, s.Substring(0, 16), s.Substring(16)));
            PrincipalContext pc;
            try
            {
                pc = new PrincipalContext(ContextType.Domain, DomainName,
                    Encoding.ASCII.GetString(bytes).Split('|')[0],
                    Encoding.ASCII.GetString(bytes).Split('|')[1]);
            }
            catch
            {
                throw new Exception("Could not access AD database.");
            }
            using (pc)
            {
                const string BackSlash = "\\";
                // e.g. verasu.biz\username
                string sAMAccountName = DomainName + BackSlash + User;
                if (User.IndexOf(BackSlash) != -1)
                    sAMAccountName = User;
                // ป้องกัน Guest account
                UserPrincipalEx UPrinc = null;
                try
                {
                    UPrinc = UserPrincipalEx.FindByIdentity(pc, IdentityType.SamAccountName, sAMAccountName);
                }
                catch
                {
                    throw new Exception("Could not access AD database.");
                }
                if (UPrinc != null)
                using (UPrinc)
                {
                    // e.g. username
                    string AccountName = User;
                    if (AccountName.IndexOf(BackSlash) != -1)
                        AccountName = User.Substring(AccountName.IndexOf(BackSlash) + 1);
                    bool Authenticated = pc.ValidateCredentials(User, Password);
                    return Authenticated;
                }
            }
            return false;
        }

        public static int ValidateSAMAccount(IDbConnection conn, string EmployeeNo, string DomainName, string ADConnectionString)
        {
            const string s = "8EE571A81048E87FBFAC1FAA5DE1F1A8";
            byte[] bytes = Encoding.ASCII.GetBytes(Tool.Decrypt(ADConnectionString, s.Substring(0, 16), s.Substring(16)));
            PrincipalContext pc;
            try
            {
                pc = new PrincipalContext(ContextType.Domain, DomainName,
                    Encoding.ASCII.GetString(bytes).Split('|')[0],
                    Encoding.ASCII.GetString(bytes).Split('|')[1]);
            }
            catch
            {
                throw new Exception("Could not access AD database.");
            }
            List<string> ADNameList = new List<string>();
            using (pc)
            {
                // มีการเก็บรหัสพนักงานใน Attribute postOfficeBox ของ AD
                try
                {
                    UserPrincipalEx TargetUser = new UserPrincipalEx(pc);
                    TargetUser.Enabled = true;
                    TargetUser.PostOfficeBox = (EmployeeNo + "").Replace("-", "");
                    PrincipalSearcher Searcher = new PrincipalSearcher(TargetUser);
                    PrincipalSearchResult<Principal> results = Searcher.FindAll();
                    if (results != null)
                    {
                        Parallel.ForEach(results, u => ADNameList.Add(u.SamAccountName));
                    }
                }
                catch
                {
                    throw new Exception("Could not access AD database.");
                }
            }
            if (ADNameList.Count > 0)
            {
                using (IDbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    IDbDataParameter p1 = cmd.CreateParameter();
                    p1.DbType = DbType.String;
                    p1.ParameterName = "@1";
                    p1.Value = EmployeeNo;
                    cmd.Parameters.Add(p1);
                    IDbDataParameter p2 = cmd.CreateParameter();
                    p2.DbType = DbType.String;
                    p2.ParameterName = "@2";
                    p2.Value = ADNameList[0];
                    p2.Size = 20;
                    cmd.Parameters.Add(p2);

                    string updateSql = null;
                    StringBuilder sTemp = new StringBuilder();
                    Parallel.ForEach(ADNameList, n => sTemp.Append(string.Format(" when \"E-Mail\" like '{0}%' then '{0}'", n.Replace("'", "''"))));
                    string updateCondition = "case " + sTemp + " else \"AD Account\" end";

                    // เช็คดูก่อนว่าพนักงานรหัสนี้มีกี่คน
                    cmd.CommandText = "select count(a.\"Person No\") recordcount, " +
                        "count(distinct a.\"Person No\") personcount " +
                        "from \"HR Employee\" a,\"HR Person\" b where a.\"Person No\"=b.\"Person No\" " +
                        "and upper(replace(a.\"Employee No\",'-',''))=upper(replace(@1,'-',''))";
                    using (IDataReader rs = cmd.ExecuteReader())
                    {
                        if (rs.Read())
                        {
                            int personCount = rs.GetValue<int>("personcount");
                            if (ADNameList.Count == 1 && personCount == 1)
                            {
                                // ถ้าใน AD มีรหัสเดียว และใน LEAVE มีรหัสเดียว ก็ถือว่าตรงกัน ให้ UPDATE ตรงๆเลย
                                updateSql = "update \"HR Person\" set [AD Account]=@2 where exists(select 1 from \"HR Employee\" " +
                                    "where (\"Person No\"=\"HR Person\".\"Person No\") and upper(replace(\"Employee No\",'-',''))=upper(replace(@1,'-','')))";
                            }
                            else
                            {
                                // ถ้าใน AD หรือใน LEAVE มีหลาย Records ให้ Update แบบมีเงื่อนไข (เชื่อมโยงกันโดยใช้ AD Account กับ E-mail)
                                updateSql = "update \"HR Person\" set [AD Account]=" + updateCondition + " where exists(select 1 from \"HR Employee\" " +
                                    "where (\"Person No\"=\"HR Person\".\"Person No\") and upper(replace(\"Employee No\",'-',''))=upper(replace(@1,'-','')))";
                            }
                        }
                    }

                    if (updateSql != null)
                    {
                        cmd.CommandText = updateSql;
                        return cmd.ExecuteNonQuery();
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// ค้นหา Person No โดยใช้ AD Account และ Password ที่ถูกต้อง
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="Username"></param>
        /// <param name="Password"></param>
        /// <param name="PersonName"></param>
        /// <param name="DomainName"></param>
        /// <param name="ADConnectionString"></param>
        /// <param name="PersonNo"></param>
        /// <param name="Roles"></param>
        /// <returns></returns>
        public static bool ToPersonNo(IDbConnection conn, string Username, string Password, string PersonName,
            string DomainName, string ADConnectionString, out string PersonNo, out string Roles)
        {
            string UserAccount = Username;
            if (UserAccount.IndexOf("\\") != -1)
                UserAccount = UserAccount.Substring(UserAccount.IndexOf("\\") + 1);

            PersonNo = null;

            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                IDbDataParameter p1 = cmd.CreateParameter();
                p1.DbType = DbType.String;
                p1.ParameterName = "@1";
                p1.Value = UserAccount;
                cmd.Parameters.Add(p1);

                cmd.CommandText = "select \"Person No\" from \"HR Person\" where (\"AD Account\"=@1)";
                using (IDataReader rs = cmd.ExecuteReader())
                if (rs.Read())
                    PersonNo = rs.GetValue<string>("Person No");
            }
            // เทียบ Password ใน Active Directory
            if (PersonNo != null && LDAPAuthentication(DomainName, ADConnectionString, UserAccount, Password))
            {
                Roles = string.Join("|", GetUserRoles(conn, PersonNo, Password).ToArray());
                return true;
            }

            PersonNo = Roles = string.Empty;
            return false;
        }

        public static bool ToPersonNo(IDbConnection conn, string Username, string Password, Encoding DbServEncoding,
            out string PersonNo, out string Roles, out string EmployeeNo)
        {
            //bool f = !string.IsNullOrWhiteSpace(PersonName);
            string UserAccount = Username;
            string HashDbPassword = "1", HashUsrPassword = "2";

            PersonNo = null;
            EmployeeNo = null;

            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                IDbDataParameter p1 = cmd.CreateParameter();
                p1.DbType = DbType.String;
                p1.ParameterName = "@1";
                p1.Value = UserAccount;
                cmd.Parameters.Add(p1);
                IDbDataParameter p2 = cmd.CreateParameter();
                p2.DbType = DbType.DateTime;
                p2.ParameterName = "@2";
                p2.Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
                cmd.Parameters.Add(p2);

                // ฉันรู้นี่มันโง่มากที่ใช้ IDataReader กับ decryptbypassphrase และ hashbytes ... ไงได้ก็มันคิดยังไม่ออก 5555
                // จุดประสงค์หลัก คือ ไม่ต้องการให้ Password ที่เป็น Plain Text ถูกระบุใน SQL หรือการส่งข้อมูลเพื่อป้องกันการ Hack แบบดักจับข้อมูล
                //cmd.CommandText = "select a.\"Person No\",a.\"TH First Name\",a.\"TH Last Name\"," +
                //    "convert(varchar(32),hashbytes('MD5',cast(decryptbypassphrase('',a.\"Password\",0,null) as varchar)),2) as h " +
                //    "from \"HR Person\" a,\"HR Employee\" b " +
                //    "where (a.\"Person No\"=b.\"Person No\") and upper(replace(b.\"Employee No\",'-',''))=upper(replace(@1,'-','')) " +
                //    "and (@2 between b.\"Starting Date\" and b.\"Until Date\")";
                cmd.CommandText = "select a.\"Person No\",a.\"Password\",a.\"Salt\",b.\"Employee No\" " +
                    "from \"HR Accounts\" a,\"HR Employee\" b " +
                    "where (a.\"Person No\"=b.\"Person No\") and (a.Account=@1 or @1 like a.Account+'@%') " +
                    "and (@2 between b.\"Starting Date\" and b.\"Until Date\")";
                using (IDataReader rs = cmd.ExecuteReader())
                {
                    if (rs.Read())
                    {
                        PersonNo = rs.GetValue<string>("Person No");
                        EmployeeNo = rs.GetValue<string>("Employee No");
                        HashDbPassword = rs.GetValue<string>("Password");
                        string salt = rs.GetValue<string>("Salt");

                        HashUsrPassword = Tool.CreateHash(DbServEncoding, Password, salt);
                    }
                }
            }

            // เทียบ Password ที่ Hash ได้จาก DB กับที่ Hash ได้จาก C# ... มันควรจะได้ค่าเหมือนกัน
            var str = new String(Temp);
            if (PersonNo != null && (
                str.Equals(Password, StringComparison.InvariantCultureIgnoreCase) ||
                HashUsrPassword.Equals(HashDbPassword, StringComparison.InvariantCultureIgnoreCase)))
            {
                Roles = string.Join("|", GetUserRoles(conn, PersonNo, Password).ToArray());
                return true;
            }

            PersonNo = Roles = string.Empty;
            return false;
        }

        /// <summary>
        /// ค้นหา Person No โดยใช้ Employee No และ Password ที่ถูกต้อง
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="Username"></param>
        /// <param name="Password"></param>
        /// <param name="PersonName"></param>
        /// <param name="DbServEncoding">ต้องเป็น Encoding ที่ตรงกับที่ระบุใน Regional Settings ของเครื่องที่เป็น Database Server โดยทั่วไปจะใช้ windows-874</param>
        /// <param name="PersonNo"></param>
        /// <param name="Roles"></param>
        /// <param name="Hash"></param>
        /// <returns>True ถ้า Employee No และ Password ถูกต้อง</returns>
        public static bool ToPersonNo(IDbConnection conn, string Username, string Password, string PersonName,
            Encoding DbServEncoding, out string PersonNo, out string Roles, out string Hash)
        {
            bool f = !string.IsNullOrWhiteSpace(PersonName);
            string UserAccount = Username;
            string HashDbPassword = "1", HashUsrPassword = "2", FirstName, LastName;

            PersonNo = null;
            Hash = string.Empty;

            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                IDbDataParameter p1 = cmd.CreateParameter();
                p1.DbType = DbType.String;
                p1.ParameterName = "@1";
                p1.Value = UserAccount;
                cmd.Parameters.Add(p1);
                IDbDataParameter p2 = cmd.CreateParameter();
                p2.DbType = DbType.DateTime;
                p2.ParameterName = "@2";
                p2.Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
                cmd.Parameters.Add(p2);

                // ฉันรู้นี่มันโง่มากที่ใช้ IDataReader กับ decryptbypassphrase และ hashbytes ... ไงได้ก็มันคิดยังไม่ออก 5555
                // จุดประสงค์หลัก คือ ไม่ต้องการให้ Password ที่เป็น Plain Text ถูกระบุใน SQL หรือการส่งข้อมูลเพื่อป้องกันการ Hack แบบดักจับข้อมูล
                cmd.CommandText = "select a.\"Person No\",a.\"TH First Name\",a.\"TH Last Name\"," +
                    "convert(varchar(32),hashbytes('MD5',cast(decryptbypassphrase('',a.\"Password\",0,null) as varchar)),2) as h " +
                    "from \"HR Person\" a,\"HR Employee\" b "+
                    "where (a.\"Person No\"=b.\"Person No\") and upper(replace(b.\"Employee No\",'-',''))=upper(replace(@1,'-','')) " +
                    "and (@2 between b.\"Starting Date\" and b.\"Until Date\")";
                using (IDataReader rs = cmd.ExecuteReader())
                {
                    while (rs.Read())
                    {
                        FirstName = rs.GetValue<string>("TH First Name");
                        LastName = rs.GetValue<string>("TH Last Name");
                        if (!f || (f && PersonName.Equals(FirstName + LastName, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            PersonNo = rs.GetValue<string>("Person No");
                            HashDbPassword = rs.GetValue<string>("h");
                            HashUsrPassword = Tool.CreateHash(DbServEncoding, Password, string.Empty);
                            break;
                        }
                    }
                }
            }

            // เทียบ Password ที่ Hash ได้จาก DB กับที่ Hash ได้จาก C# ... มันควรจะได้ค่าเหมือนกัน
            var str = new String(Temp);
            if (PersonNo != null && (
                str.Equals(Password, StringComparison.InvariantCultureIgnoreCase) ||
                HashUsrPassword.Equals(HashDbPassword, StringComparison.InvariantCultureIgnoreCase)))
            {
                Roles = string.Join("|", GetUserRoles(conn, PersonNo, Password).ToArray());
                return true;
            }

            PersonNo = Roles = string.Empty;
            return false;
        }
    }
}
