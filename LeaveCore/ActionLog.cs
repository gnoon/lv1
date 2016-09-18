using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Principal;
using System.Data.SqlClient;
using log4net;

namespace LeaveCore
{
    public enum Action { Login, Logout, Applying, ApplyConfirmed, SetPassword, Error }

    public interface ILogger
    {
        void Error(Exception ex);
        void Info(object msg);
        void Debug(string msg);
        void Error(string msg, Exception ex);
    }

    public class Logger : ILogger
    {
        private static ILog _log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        void ILogger.Info(object msg)
        {
            _log.Info(msg);
        }

        public void Error(Exception ex)
        {
            _log.Error(ex.Message, ex);
        }

        public void Debug(string msg)
        {
            _log.Debug(msg);
        }

        public void Error(string msg, Exception ex)
        {
            _log.Error(msg, ex);
        }
    }

    public class ActionLog : BaseDB
    {
        static readonly ILogger logFile = new Logger();
        public static ILogger File { get { return logFile; } }

        Action _Action {
            set
            {
                switch(value)
                {
                    case Action.Login:
                        _ActionVal = Const.ACTION_LOGIN;
                        break;
                    case Action.Logout:
                        _ActionVal = Const.ACTION_LOGOUT;
                        break;
                    case Action.Applying:
                        _ActionVal = Const.ACTION_APPLY;
                        break;
                    case Action.ApplyConfirmed:
                        _ActionVal = Const.ACTION_CONFIRM;
                        break;
                    case Action.SetPassword:
                        _ActionVal = Const.ACTION_SETPASSWORD;
                        break;
                }
            }
        }
        string _ActionVal;
        string _Username;
        Int64? _RequestID;

        private ActionLog(IPrincipal User)
            : base(User)
        {
        }

        private int NewRecord()
        {
            if(string.IsNullOrEmpty(_ActionVal))
                return 0;
            using(SqlConnection conn = GetConnection())
            {
                conn.Open();
                using(SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandText = "insert into [LV Action Log]([Action Time],[Action],[Person No],[Username],[Request ID]) values(@1,@2,@3,@4,@5)";
                    cmd.Parameters.Add("@1", System.Data.SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@2", System.Data.SqlDbType.VarChar, 5).Value = _ActionVal;
                    cmd.Parameters.Add("@3", System.Data.SqlDbType.VarChar, 80).Value = User.Identity.Name;
                    cmd.Parameters.Add("@4", System.Data.SqlDbType.VarChar, 20).Value = _Username ?? (object)DBNull.Value;
                    cmd.Parameters.Add("@5", System.Data.SqlDbType.BigInt).Value = _RequestID ?? (object)DBNull.Value;
                    return cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Create a new log record
        /// </summary>
        /// <param name="User"></param>
        /// <param name="action"></param>
        /// <param name="Username"></param>
        /// <param name="RequestID"></param>
        public static void New(IPrincipal User, Action Action, string Username, Int64? RequestID)
        {
            ActionLog l = new ActionLog(User);
            try
            {
                l._Action = Action;
                l._Username = Username;
                l._RequestID = RequestID;
                l.NewRecord();
            }
            catch
            {
            }
        }
    }
}
