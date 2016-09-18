using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FingerScan.Interface;
using System.Data;
using System.Security.Cryptography;
using System.Data.SqlClient;
using System.Globalization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace FingerScan
{
    public class ActionCONSO : ActionBASE
    {
        protected DateTime _StartingDate;
        protected DateTime _UntilDate;
        protected long _LogID;

        public ActionCONSO(string ConnectionString, DateTime StartingDate, DateTime UntilDate, long LogID)
            : base(ConnectionString)
        {
            _StartingDate = StartingDate;
            _UntilDate = UntilDate;
            _LogID = LogID;
        }

        public override void Run(string DeviceCode, int? Month, int? Year)
        {
            try
            {
                using (SqlConnection conn = GetConnection())
                {
                    conn.Open();

                    if (_LogID == 0)
                    {
                        long Lid = DateTime.Now.Ticks;
                        _LogID = Lid;
                    }
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = 7200; // 2 hours
                        cmd.CommandText = "WorkTimeConsolicate";
                        cmd.Parameters.Add("@BeginDate", SqlDbType.DateTime).Value = _StartingDate;
                        cmd.Parameters.Add("@UntilDate", SqlDbType.DateTime).Value = _UntilDate;
                        cmd.Parameters.Add("@LogID", SqlDbType.BigInt).Value = _LogID;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                Program.ErrorLine(e.Message);
                try
                {
                    using (SqlConnection conn = GetConnection())
                    {
                        conn.Open();
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandType = CommandType.Text;
                            cmd.CommandText = "update [TM Log CONSO] set [Error]=@2 where [Log ID]=@1";
                            cmd.Parameters.Add("@1", SqlDbType.BigInt).Value = _LogID;
                            cmd.Parameters.Add("@2", SqlDbType.VarChar, 100).Value = e.Message;
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}
