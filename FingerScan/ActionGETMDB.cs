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
    public class ActionGETMDB : ActionBASE
    {
        protected string _MDBConnectionString;
        protected DateTime _StartingDate;
        protected DateTime _UntilDate;
        protected long _LogID;

        public ActionGETMDB(string ConnectionString, string MDBConnectionString, DateTime StartingDate, DateTime UntilDate, long LogID)
            : base(ConnectionString)
        {
            _MDBConnectionString = MDBConnectionString;
            _StartingDate = StartingDate;
            _UntilDate = UntilDate;
            _LogID = LogID;
        }

        public override void Run(string DeviceCode, int? Month, int? Year)
        {
            RunMDB("", Month, Year);
        }

        private void RunMDB(string DeviceCode, int? iMonth, int? iYear)
        {
            ///
            /// DEBUG INFO
            ///
            DateTime begin, until;
            TimeSpan watch;
            ///

            string NL = Environment.NewLine;
            try
            {
                begin = DateTime.Now;
                int recs = CleanTempTable();
                until = DateTime.Now;

                watch = until - begin;
                Program.LogDb(null, null, EventLevel.Debug,
                    string.Format("Done clear {0} record{2} from dbo.[TM GET Temp] within {1} secs",
                    recs.ToString(N_FORMAT), watch.TotalSeconds.ToString(N_FORMAT), recs > 1 ? "s" : ""));
            }
            catch(Exception e)
            {
                string txt = "Error clearing table dbo.[TM GET Temp]: " + e.Message;
                Program.LogDb(null, null, EventLevel.Critical, txt);
                throw new Exception(txt, e);
            }

            RawRecord r;
            DataRow row;
            DataRow[] rows;
            string temp;
            DateTime now = DateTime.Now;
            int newupdate, tempRows;
            double networkspeed;
            ulong bytes;
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream mem;
            const string DeviceID = "att2000.mdb";

            using(MD5 md5 = MD5CryptoServiceProvider.Create())
            try
            {
                DataTable dt = MemoryTempTable;

                using (FingerprintMDB fscan = new FingerprintMDB(_MDBConnectionString, _StartingDate, _UntilDate))
                {
                    try
                    {
                        fscan.Connect();
                        Program.LogDb(DeviceCode, DeviceID, EventLevel.Debug,
                            "Done connected");
                    }
                    catch (Exception ex)
                    {
                        string txt = "Error connect device: " + ex.Message;
                        Program.LogDb(DeviceCode, DeviceID, EventLevel.Info, txt);
                        throw new Exception(txt, ex);
                    }
                    fscan.DeviceCode = DeviceCode;

                    int recordCount = 0;
                    try
                    {
                        using(SqlConnection conn = GetConnection())
                        using (FingerprintReader rs = fscan.CreateReader())
                        {
                            conn.Open();

                            if (_LogID == 0)
                            {
                                long Lid = DateTime.Now.Ticks;
                                using (SqlCommand cmd = conn.CreateCommand())
                                {
                                    cmd.CommandType = CommandType.Text;
                                    cmd.CommandText =
                                        "insert into [TM Log GET]([Log ID],[Begin Date],[Until Date],[Total Record],[Status Progress],[Log Time],[Log Person],[Source]) " +
                                        "values(@1,@2,@3,@4,@5,@6,@7,@8)";
                                    cmd.Parameters.Add("@1", SqlDbType.BigInt).Value = Lid;
                                    cmd.Parameters.Add("@2", SqlDbType.DateTime).Value = _StartingDate;
                                    cmd.Parameters.Add("@3", SqlDbType.DateTime).Value = _UntilDate;
                                    cmd.Parameters.Add("@4", SqlDbType.Int).Value = 0;
                                    cmd.Parameters.Add("@5", SqlDbType.VarChar, 20).Value = "PENDING";
                                    cmd.Parameters.Add("@6", SqlDbType.DateTime).Value = DateTime.Now;
                                    cmd.Parameters.Add("@7", SqlDbType.VarChar, 80).Value = "";
                                    cmd.Parameters.Add("@8", SqlDbType.VarChar, 6).Value = "AUTO";
                                    cmd.ExecuteNonQuery();
                                }
                                _LogID = Lid;
                            }
                            using (SqlCommand cmd = conn.CreateCommand())
                            {
                                cmd.CommandType = CommandType.Text;
                                cmd.CommandText = "update [TM Log GET] set [Status Progress]='READING',[Total Record]=0 where [Log ID]=" + _LogID;
                                cmd.ExecuteNonQuery();
                            }
                            const int commit = 5000; // update ทุกๆ 5000 รายการ

                            dt.BeginLoadData();
                            bytes = 0;
                            begin = DateTime.Now;
                            while (rs.Read())
                            {
                                r = rs.Current;
                                // calculate size of data
                                using (mem = new MemoryStream())
                                {
                                    bf.Serialize(mem, r);
                                    bytes += (ulong)mem.Length;
                                }

                                temp = string.Format("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}{11}",
                                    r.DeviceCode, r.Year, r.Month, r.Day, r.Hour, r.Minute, r.Second,
                                    r.InOutMode, r.EnrollNumber, r.VerifyMode, r.SensorID, r.Workcode);

                                temp = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(temp))).Replace("-", "");

                                // check if exists
                                rows = dt.Select("[Hash Code]='" + temp + "'");
                                if (rows != null && rows.Length > 0)
                                {
                                    continue;
                                }

                                row = dt.NewRow();
                                row["Hash Code"] = temp;
                                row["Log Time"] = now;
                                row["Device Code"] = r.DeviceCode;
                                row["Check Date"] = new DateTime(r.Year, r.Month, r.Day, 0, 0, 0);
                                row["Check Time"] = new DateTime(1900, 1, 1, r.Hour, r.Minute, r.Second);
                                row["Check Type"] = r.InOutMode.ToString();
                                row["User ID"] = r.EnrollNumber;
                                row["Verify Code"] = r.VerifyMode;
                                row["Sensor ID"] = r.SensorID;
                                row["Work Code"] = r.Workcode;

                                dt.Rows.Add(row);

                                recordCount++;
                                if (_LogID > 0)
                                if (recordCount % commit == 0)
                                {
                                    using (SqlCommand cmd = conn.CreateCommand())
                                    {
                                        cmd.CommandType = CommandType.Text;
                                        cmd.CommandText = "update [TM Log GET] set [Total Record]=" + recordCount + " where [Log ID]=" + _LogID;
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                            until = DateTime.Now;
                            dt.EndLoadData();
                            tempRows = dt.Rows.Count;

                            if (_LogID > 0)
                            using (SqlCommand cmd = conn.CreateCommand())
                            {
                                cmd.CommandType = CommandType.Text;
                                cmd.CommandText = "update [TM Log GET] set [Total Record]=" + recordCount + " where [Log ID]=" + _LogID;
                                cmd.ExecuteNonQuery();
                            }

                            watch = until - begin;
                            networkspeed = watch.TotalSeconds;
                            Program.LogDb(DeviceCode, DeviceID, EventLevel.Debug,
                                string.Format("Done read {0} record{1} ({2} bytes) into memory within {3} secs",
                                dt.Rows.Count.ToString(N_FORMAT), dt.Rows.Count > 1 ? "s" : "", bytes.ToString(N_FORMAT),
                                networkspeed.ToString(N_FORMAT)));
                        }
                    }
                    catch (Exception ex)
                    {
                        string txt = "Read device interrupted: " + ex.Message;
                        Program.LogDb(DeviceCode, DeviceID, EventLevel.Info, txt);
                        throw new Exception(txt, ex);
                    }
                }

                Program.LogDb(DeviceCode, DeviceID, EventLevel.Debug,
                            "Done disconnected");

                if (tempRows > 0)
                {
                    using (SqlConnection conn = GetConnection())
                    {
                        conn.Open();

                        if (_LogID > 0)
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandType = CommandType.Text;
                            cmd.CommandText = "update [TM Log GET] set [Status Progress]='CACHING' where [Log ID]=" + _LogID;
                            cmd.ExecuteNonQuery();
                        }

                        try
                        {
                            begin = DateTime.Now;
                            SqlBulkCopy dbWriter = new SqlBulkCopy(conn);
                            dbWriter.DestinationTableName = "dbo.[TM GET Temp]";
                            dbWriter.WriteToServer(dt);
                            until = DateTime.Now;
                            watch = until - begin;
                            Program.LogDb(DeviceCode, DeviceID, EventLevel.Debug,
                                string.Format("Done save to dbo.[TM GET Temp] within {0} secs",
                                watch.TotalSeconds.ToString(N_FORMAT)));
                            dt.Clear();
                        }
                        catch (Exception ex)
                        {
                            string txt = "Error saving to dbo.[TM GET Temp]: " + ex.Message;
                            Program.LogDb(DeviceCode, DeviceID, EventLevel.Warning, txt);
                            throw new Exception(txt, ex);
                        }

                        if (_LogID > 0)
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandType = CommandType.Text;
                            cmd.CommandText = "update [TM Log GET] set [Status Progress]='WRITING' where [Log ID]=" + _LogID;
                            cmd.ExecuteNonQuery();
                        }

                        try
                        {
                            newupdate = 0;
                            begin = DateTime.Now;
                            using (SqlCommand cmd = conn.CreateCommand())
                            {
                                cmd.CommandType = CommandType.Text;

                                temp = "WHERE [Check Date] BETWEEN @1 and @2";

                                cmd.Parameters.Add("@1", SqlDbType.DateTime).Value = _StartingDate.Date;
                                cmd.Parameters.Add("@2", SqlDbType.DateTime).Value = _UntilDate.Date;

                                cmd.CommandText =
                                    "WITH s AS (SELECT  " + NL +
                                    "[Hash Code],[Log Time],[Device Code],[Check Date],[Check Time],  " + NL +
                                    "[Check Type],[User ID],[Verify Code],[Sensor ID],[Work Code]  " + NL +
                                    "FROM [TM GET Temp] WITH(READUNCOMMITTED)  " + NL + temp + ")  " + NL +
                                    "MERGE INTO [TM GET History] AS t  " + NL +
                                    "USING s ON (s.[Hash Code]=t.[Hash Code])  " + NL +
                                    "WHEN MATCHED THEN " + NL +
                                    "UPDATE SET t.[Log Time]=s.[Log Time] " + NL +
                                    ",t.[Device Code]=s.[Device Code] " + NL +
                                    ",t.[Check Date]=s.[Check Date] " + NL +
                                    ",t.[Check Time]=s.[Check Time] " + NL +
                                    ",t.[Check Type]=s.[Check Type] " + NL +
                                    ",t.[User ID]=s.[User ID] " + NL +
                                    ",t.[Verify Code]=s.[Verify Code] " + NL +
                                    ",t.[Sensor ID]=s.[Sensor ID] " + NL +
                                    ",t.[Work Code]=s.[Work Code] " + NL +
                                    "WHEN NOT MATCHED BY TARGET THEN  " + NL +
                                    "INSERT(  " + NL +
                                    "    [Hash Code],[Log Time],[Device Code],[Check Date],[Check Time],  " + NL +
                                    "    [Check Type],[User ID],[Verify Code],[Sensor ID],[Work Code])  " + NL +
                                    "VALUES(  " + NL +
                                    "    s.[Hash Code],s.[Log Time],s.[Device Code],s.[Check Date],s.[Check Time],  " + NL +
                                    "    s.[Check Type],s.[User ID],s.[Verify Code],s.[Sensor ID],s.[Work Code]);";

                                newupdate = cmd.ExecuteNonQuery();
                            }
                            until = DateTime.Now;
                            watch = until - begin;
                            Program.LogDb(DeviceCode, DeviceID, EventLevel.Debug,
                                string.Format("Done merge to dbo.[TM GET History] within {0} secs (new update {1} record{2})",
                                watch.TotalSeconds.ToString(N_FORMAT), newupdate.ToString(N_FORMAT), newupdate > 1 ? "s" : ""));
                        }
                        catch (Exception ex)
                        {
                            string txt = "Error merging to dbo.[TM GET History]: " + ex.Message;
                            Program.LogDb(DeviceCode, DeviceID, EventLevel.Warning, txt);
                            throw new Exception(txt, ex);
                        }

                        if (_LogID > 0)
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandType = CommandType.Text;
                            cmd.CommandText = "update [TM Log GET] set [Status Progress]='SUMMARIZING' where [Log ID]=" + _LogID;
                            cmd.ExecuteNonQuery();
                        }

                        try
                        {
                            begin = DateTime.Now;
                            using (SqlCommand cmd = conn.CreateCommand())
                            {
                                cmd.CommandType = CommandType.Text;

                                temp = "WHERE a.[Check Date] BETWEEN @1 and @2 ";

                                cmd.Parameters.Add("@1", SqlDbType.DateTime).Value = _StartingDate.Date;
                                cmd.Parameters.Add("@2", SqlDbType.DateTime).Value = _UntilDate.Date;
                                cmd.Parameters.Add("@now", SqlDbType.DateTime).Value = now;

                                cmd.CommandText =
                                    "WITH s AS (SELECT a.[User ID] " + NL +
                                    "	,a.[Check Date] " + NL +
                                    "   ,CONVERT(DATETIME,CONVERT(VARCHAR(16),MIN(a.[Check Time]),120)+ ':00') AS [Check In] " + NL +
                                    "   ,CONVERT(DATETIME,CONVERT(VARCHAR(16),MAX(a.[Check Time]),120)+ ':00') AS [Check Out] " + NL +
                                    "	,CONVERT(DATETIME,CONVERT(VARCHAR(16),MAX(a.[Check Time]),120)+ ':00')-CONVERT(DATETIME,CONVERT(VARCHAR(16),MIN(a.[Check Time]),120)+ ':00') AS [Hours Gross] " + NL +
                                    "	,CASE WHEN MAX(b.[Break Time]) IS NOT NULL THEN (CASE " + NL +
                                    "	   WHEN MAX(a.[Check Time])<=MAX(b.[WShift Morning In]) THEN CAST('1900-01-01' AS DATETIME) " + NL +
                                    "	   WHEN MIN(a.[Check Time])>=MAX(b.[WShift Noon Out]) THEN CAST('1900-01-01' AS DATETIME) " + NL +
                                    "	   WHEN MAX(a.[Check Time])<=MAX(b.[WShift Noon In]) THEN ( " + NL +
                                    "	     (CASE WHEN MAX(a.[Check Time])<MAX(b.[WShift Morning Out]) THEN CONVERT(DATETIME,CONVERT(VARCHAR(16),MAX(a.[Check Time]),120)+ ':00') ELSE MAX(b.[WShift Morning Out]) END) " + NL +
                                    "		-(CASE WHEN MIN(a.[Check Time])>MAX(b.[WShift Morning In]) THEN CONVERT(DATETIME,CONVERT(VARCHAR(16),MIN(a.[Check Time]),120)+ ':00') ELSE MAX(b.[WShift Morning In]) END) " + NL +
                                    "	   ) " + NL +
                                    "	   WHEN MIN(a.[Check Time])>=MAX(b.[WShift Morning Out]) THEN ( " + NL +
                                    "	     (CASE WHEN MAX(a.[Check Time])<MAX(b.[WShift Noon Out]) THEN CONVERT(DATETIME,CONVERT(VARCHAR(16),MAX(a.[Check Time]),120)+ ':00') ELSE MAX(b.[WShift Noon Out]) END) " + NL +
                                    "		-(CASE WHEN MIN(a.[Check Time])>MAX(b.[WShift Noon In]) THEN CONVERT(DATETIME,CONVERT(VARCHAR(16),MIN(a.[Check Time]),120)+ ':00') ELSE MAX(b.[WShift Noon In]) END) " + NL +
                                    "	   ) " + NL +
                                    "	   ELSE ( " + NL +
                                    "	     (CASE WHEN MAX(a.[Check Time])<MAX(b.[WShift Noon Out]) THEN CONVERT(DATETIME,CONVERT(VARCHAR(16),MAX(a.[Check Time]),120)+ ':00') ELSE MAX(b.[WShift Noon Out]) END) " + NL +
                                    "		-(CASE WHEN MIN(a.[Check Time])>MAX(b.[WShift Morning In]) THEN CONVERT(DATETIME,CONVERT(VARCHAR(16),MIN(a.[Check Time]),120)+ ':00') ELSE MAX(b.[WShift Morning In]) END) " + NL +
                                    "		-MAX(b.[Break Time]) " + NL +
                                    "	   ) " + NL +
                                    "	   END " + NL +
                                    "	 ) " + NL +
                                    "	 END AS [Hours by WShift] " + NL +
                                    "    ,MAX(b.[WShift Morning In]) AS [WShift Morning In] " + NL +
                                    "    ,MAX(b.[WShift Morning Out]) AS [WShift Morning Out] " + NL +
                                    "    ,MAX(b.[WShift Noon In]) AS [WShift Noon In] " + NL +
                                    "    ,MAX(b.[WShift Noon Out]) AS [WShift Noon Out] " + NL +
                                    "	,GETDATE() AS [Computed Date] " + NL +
                                    "	,MAX(b.[Person No]) AS [Person No] " + NL +
                                    "	,MAX(b.[Employee No]) AS [Employee No] " + NL +
                                    "FROM [TM GET History] a WITH(READUNCOMMITTED) " + NL +
                                    "	LEFT JOIN [TM Profile Workshift] b ON a.[User ID]=b.[User ID] " + NL + temp +
                                    "GROUP BY a.[User ID] " + NL +
                                    "	,a.[Check Date]) " + NL +
                                    "MERGE INTO [TM GET Summary] AS t " + NL +
                                    "USING s ON (s.[User ID]=t.[User ID] AND s.[Check Date]=t.[Check Date]) " + NL +
                                    "WHEN MATCHED THEN " + NL +
                                    "UPDATE SET t.[Check In]=s.[Check In] " + NL +
                                    ",t.[Check Out]=s.[Check Out] " + NL +
                                    ",t.[WShift Morning In]=s.[WShift Morning In] " + NL +
                                    ",t.[WShift Morning Out]=s.[WShift Morning Out] " + NL +
                                    ",t.[WShift Noon In]=s.[WShift Noon In] " + NL +
                                    ",t.[WShift Noon Out]=s.[WShift Noon Out] " + NL +
                                    ",t.[Computed Date]=s.[Computed Date] " + NL +
                                    ",t.[Hours Gross]=s.[Hours Gross] " + NL +
                                    ",t.[Hours by WShift]=s.[Hours by WShift] " + NL +
                                    ",t.[Person No]=s.[Person No] " + NL +
                                    ",t.[Employee No]=s.[Employee No] " + NL +
                                    //"WHEN NOT MATCHED BY SOURCE THEN DELETE " + NL +
                                    "WHEN NOT MATCHED BY TARGET THEN " + NL +
                                    "INSERT( " + NL +
                                    "   [User ID],[Check Date],[Check In],[Check Out],[WShift Morning In],[WShift Morning Out],[WShift Noon In], " + NL +
                                    "   [WShift Noon Out],[Computed Date],[Hours Gross],[Hours by WShift],[Person No],[Employee No]) " + NL +
                                    "VALUES( " + NL +
                                    "   s.[User ID],s.[Check Date],s.[Check In],s.[Check Out],s.[WShift Morning In],s.[WShift Morning Out],s.[WShift Noon In], " + NL +
                                    "   s.[WShift Noon Out],@now,s.[Hours Gross],s.[Hours by WShift],s.[Person No],s.[Employee No]);";

                                newupdate = cmd.ExecuteNonQuery();
                            }
                            until = DateTime.Now;
                            watch = until - begin;
                            Program.LogDb(DeviceCode, DeviceID, EventLevel.Debug,
                                string.Format("Done merge to dbo.[TM GET Summary] within {0} secs (new update {1} record{2})",
                                watch.TotalSeconds.ToString(N_FORMAT), newupdate.ToString(N_FORMAT), newupdate > 1 ? "s" : ""));
                        }
                        catch (Exception ex)
                        {
                            string txt = "Error merging to dbo.[TM GET Summary]: " + ex.Message;
                            Program.LogDb(DeviceCode, DeviceID, EventLevel.Warning, txt);
                            throw new Exception(txt, ex);
                        }

                        Program.LogDb(DeviceCode, DeviceID, EventLevel.Info,
                            string.Format("Successfully GET {0} record{1} ({2} bytes) within {3} secs (new update {4} record{5})",
                            tempRows.ToString(N_FORMAT), tempRows > 1 ? "s" : "", bytes.ToString(N_FORMAT),
                            networkspeed.ToString(N_FORMAT), newupdate.ToString(N_FORMAT), newupdate > 1 ? "s" : ""));

                        if (_LogID > 0)
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandType = CommandType.Text;
                            cmd.CommandText = "update [TM Log GET] set [Status Progress]='COMPLETE' where [Log ID]=" + _LogID;
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch(Exception e)
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
                            cmd.CommandText = "update [TM Log GET] set [Status Progress]='FAIL' where [Log ID]=" + _LogID;
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
