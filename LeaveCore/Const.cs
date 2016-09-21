using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Principal;
using System.Data.SqlClient;

namespace LeaveCore
{
    public class Const
    {
        #region Leave
        public const int STATUS_MULTIPLE_STATUSES = 10;
        public const int STATUS_LEAVE_PENDING_APPROVAL = 11;
        public const int STATUS_LEAVE_APPROVED = 12;
        public const int STATUS_LEAVE_REJECTED = 13;
        public const int STATUS_LEAVE_CANCELREQUEST = 14;
        public const int STATUS_LEAVE_CANCELLED = 15;
        //public static const int STATUS_LEAVE_TAKEN = 16;
        public const int STATUS_LEAVE_AWAITING = 17;
        public const int STATUS_LEAVE_CANCELAPPROVED = 102;
        public const int STATUS_LEAVE_CANCELREJECTED = 103;
        public const int STATUS_LEAVE_INTERRUPTED = 20;

        /// <summary>
        /// Get name
        /// </summary>
        /// <param name="User"></param>
        /// <param name="StatusID"></param>
        /// <param name="Conn">null หากต้องการเปิด Connection ใหม่</param>
        /// <returns></returns>
        public static string GetStatusName(IPrincipal User, int StatusID, SqlConnection Conn)
        {
            BaseDB db = new BaseDB(User);
            return db.ExecuteScalar<string>(
                "select [TH Name] from [LV Status] where [Status ID]=" + StatusID, Conn);
        }

        public static int[] LEAVE_STATUS_ACTIVE() {
            return new int[]{
                STATUS_LEAVE_APPROVED,
                STATUS_LEAVE_PENDING_APPROVAL,
                STATUS_LEAVE_AWAITING,
                STATUS_LEAVE_CANCELREQUEST,
                STATUS_LEAVE_CANCELREJECTED
            };
        }
        public static int[] LEAVE_STATUS_TAKEN() {
            return new int[]{
                STATUS_LEAVE_APPROVED,
                STATUS_LEAVE_CANCELREQUEST,
                STATUS_LEAVE_CANCELREJECTED
            };
        }
        public static int[] LEAVE_STATUS_CANCEL() {
            return new int[]{
                STATUS_LEAVE_CANCELAPPROVED,
                STATUS_LEAVE_CANCELREJECTED
            };
        }
        #endregion

        public static int REQUEST_BUSINESS_PREDATE = 1;
        public static int REQUEST_VACATION_PREDATE = 3;
        public static int REQUEST_MATERNITY_PREDATE = 30;
        public static int REQUEST_INITIATION_PREDATE = 30;
        public static int REQUEST_CELEBRATEHULLVALLEY_PREDATE = 30;
        public static int REQUEST_EDUCATION_PREDATE = 7;
        public static int REQUEST_STERILIZATION_PREDATE = 7;
        public static int REQUEST_WEDDING_PREDATE = 30;

        public static int REQUEST_SICK_CONTINUALLY_HOURS = 24;
        public static string REQUEST_APPROVED_BCC = null;

		#region Grant Step
		public const int STATUS_GRANTSTEP_PENDING_APPROVAL = 1;
		public const int STATUS_GRANTSTEP_APPROVED = 2;

		/// <summary>
		/// Get name
		/// </summary>
		/// <param name="User"></param>
		/// <param name="GrantStepID"></param>
        /// <param name="Conn">null หากต้องการเปิด Connection ใหม่</param>
		/// <returns></returns>
        public static string GetGrantStepName(IPrincipal User, int GrantStepID, SqlConnection Conn)
        {
            BaseDB db = new BaseDB(User);
            return db.ExecuteScalar<string>(
                "select [TH Name] from [LV Grant Step] where [Grant Step ID]=" + GrantStepID, Conn);
        }
		#endregion

		#region Quota
        public static int QUOTA_MONTHSTART = 1;
        public static int QUOTA_VACATION_MONTHSTART = 1;
        public static int QUOTA_MINMONTHS4VACATION = 1;
        public const int QUOTA_UNLIMITED = 366;
        public static int QUOTA_VACATION_INIT_DAYS = 6;//10;
        public static int QUOTA_BUSINESS_INIT_DAYS = 7;//12;
        public static int QUOTA_SICK_INIT_DAYS = 30;

        public static void LoadQuotaAllocate(IPrincipal User)
        {
            BaseDB db = new BaseDB(User);
            using (var conn = db.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandText = "select [Type No],[Max Days Per Year] from [LV Type Sub]";
                    using (var rs = cmd.ExecuteReader())
                    {
                        while (rs.Read())
                        {
                            var typeNo = rs.GetValue<string>("Type No");
                            var quota = rs.GetValue<int>("Max Days Per Year");
                            if (TYPE_NO_SICK.Equals(typeNo, StringComparison.InvariantCultureIgnoreCase))
                                QUOTA_SICK_INIT_DAYS = quota;
                            else if (TYPE_NO_BUSINESS.Equals(typeNo, StringComparison.InvariantCultureIgnoreCase))
                                QUOTA_BUSINESS_INIT_DAYS = quota;
                            else if (TYPE_NO_VACATION.Equals(typeNo, StringComparison.InvariantCultureIgnoreCase))
                                QUOTA_VACATION_INIT_DAYS = quota;
                        }
                    }
                }
            }
        }
        #endregion

        #region Veto
        public const int VETO_PROCEEDING = 0;
        public const int VETO_INTERRUPTED = 1;
        #endregion

		#region Type No
		public const string TYPE_NO_SICK = "SICK";
		public const string TYPE_NO_BUSINESS = "BUSINESS";
		public const string TYPE_NO_VACATION = "VACATION";
		public const string TYPE_NO_MILITARY = "MILITARY";
		public const string TYPE_NO_MATERNITY = "MATERNITY";
		public const string TYPE_NO_SICKATWORK = "SICKATWORK";
		public const string TYPE_NO_INITIATION = "INITIATION";
		public const string TYPE_NO_CELEBRATEHULLVALLEY = "CELEBRATEHULLVALLEY";
		public const string TYPE_NO_EDUCATION = "EDUCATION";
		public const string TYPE_NO_STERILIZATION = "STERILIZATION";
		public const string TYPE_NO_WEDDING = "WEDDING";
		public const string TYPE_NO_OTHER = "OTHER";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="User"></param>
        /// <param name="TypeSubID"></param>
        /// <param name="Conn">null หากต้องการเปิด Connection ใหม่</param>
        /// <returns></returns>
        public static string GetTypeSubRef(IPrincipal User, int TypeSubID, SqlConnection Conn)
        {
            BaseDB db = new BaseDB(User);
            return db.ExecuteScalar<string>(
                "select [Reference No] from [LV Type Sub] where [Type Sub ID]=" + TypeSubID, Conn);
        }
		#endregion

		#region Workshift
        public static decimal DEFAULT_WORKHOURS_OF_DAY = 8;
        public static int DEFAULT_MORNING_BEGINOCLOCK = 8;
        public static int DEFAULT_MORNING_UNTILOCLOCK = 12;
        public static int DEFAULT_NOON_BEGINOCLOCK = 13;
        public static int DEFAULT_NOON_UNTILOCLOCK = 17;

		#endregion

        #region Roles
        public const string ROLE_MY = "MY";
        public const string ROLE_HR = "HR";
        public const string ROLE_HEAD = "HEAD";
        public const string ROLE_IMPERSONATE = "IMPERSONATE";
        public const string ROLE_ASSTHEAD = "ASSTHEAD";
        public const string ROLE_VETO = "VETO";
        public const string ROLE_ASSTHR = "ASSTHR";
        #endregion

        #region Log Actions
        public const string ACTION_LOGIN = "LIN";
        public const string ACTION_LOGOUT = "LOUT";
        public const string ACTION_APPLY = "APPLY";
        public const string ACTION_CONFIRM = "SAVED";
        public const string ACTION_SETPASSWORD = "SETPWD";
        #endregion
    }
}
