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
    public class ActionGET : ActionBASE
    {
        public ActionGET(string ConnectionString) : base(ConnectionString)
        {
        }

        public override void Run(string DeviceCode, int? Month, int? Year)
        {
            ///
            /// DEBUG INFO
            ///
            DateTime begin, until;
            TimeSpan watch;
            ///
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
                Program.LogDb(null, null, EventLevel.Critical,
                    "Error clearing table dbo.[TM GET Temp]: " + e.Message);
                throw;
            }

            bool OnlyDevice = !string.IsNullOrEmpty(DeviceCode);
            bool OnlyYearMonth = Month.HasValue && Month.Value != 0 && Year.HasValue && Year.Value != 0;

            List<DeviceRecord> list = null;
            try
            {
                list = GETDevices();
                Program.LogDb(null, null, EventLevel.Debug,
                    string.Format("Done {0} device{1} {2} listed for GET", list.Count, list.Count > 1 ? "s" : "", list.Count > 1 ? "are" : "is"));
            }
            catch (Exception e)
            {
                Program.LogDb(null, null, EventLevel.Critical,
                    "Error get devices: " + e.Message);
                throw;
            }

            //List<WorkshiftRecord> wshift = null;
            //try
            //{
            //    wshift = WorkshiftProfiles();
            //    Program.LogDb(null, null, EventLevel.Debug,
            //        string.Format("Done {0} employee{1} {2} listed for GET", wshift.Count, wshift.Count > 1 ? "s" : "", wshift.Count > 1 ? "are" : "is"));
            //}
            //catch (Exception e)
            //{
            //    Program.LogDb(null, null, EventLevel.Critical,
            //        "Error get employee workshift: " + e.Message);
            //    throw;
            //}

            RawRecord r;
            DataRow row;
            string temp;
            DateTime now = DateTime.Now;
            int newupdate, tempRows;
            double networkspeed;
            ulong bytes;
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream mem;

            using(MD5 md5 = MD5CryptoServiceProvider.Create())
            foreach (DeviceRecord rec in list)
            {
                if (OnlyDevice && rec.DeviceCode != DeviceCode)
                {
                    Program.LogDb(rec.DeviceCode, rec.Ip + ":" + rec.Port, EventLevel.Debug,
                        "Skipped due to it is not in the GET list");
                    continue;
                }
                try
                {
                    DataTable dt = MemoryTempTable;

                    using (FingerprintScanner fscan = new FingerprintScanner(rec.Ip, rec.Port))
                    {
                        try
                        {
                            fscan.Connect();
                            Program.LogDb(rec.DeviceCode, rec.Ip + ":" + rec.Port, EventLevel.Debug,
                                "Done connected");
                        }
                        catch (Exception ex)
                        {
                            Program.LogDb(rec.DeviceCode, rec.Ip + ":" + rec.Port, EventLevel.Info,
                                "Error connect device: " + ex.Message);
                            throw;
                        }
                        fscan.DeviceCode = rec.DeviceCode;
                        try
                        {
                            using (FingerprintReader rs = fscan.CreateReader())
                            {
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

                                    if (OnlyYearMonth && r.Year != Year && r.Month != Month)
                                        continue;

                                    temp = string.Format("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}{11}",
                                        r.DeviceCode, r.Year, r.Month, r.Day, r.Hour, r.Minute, r.Second,
                                        r.InOutMode, r.EnrollNumber, r.VerifyMode, r.SensorID, r.Workcode);

                                    row = dt.NewRow();
                                    row["Hash Code"] = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(temp))).Replace("-", "");
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
                                }
                                until = DateTime.Now;
                                dt.EndLoadData();
                                tempRows = dt.Rows.Count;

                                watch = until - begin;
                                networkspeed = watch.TotalSeconds;
                                Program.LogDb(rec.DeviceCode, rec.Ip + ":" + rec.Port, EventLevel.Debug,
                                    string.Format("Done read {0} record{1} ({2} bytes) into memory within {3} secs",
                                    dt.Rows.Count.ToString(N_FORMAT), dt.Rows.Count > 1 ? "s" : "", bytes.ToString(N_FORMAT),
                                    networkspeed.ToString(N_FORMAT)));
                            }
                        }
                        catch (Exception ex)
                        {
                            Program.LogDb(rec.DeviceCode, rec.Ip + ":" + rec.Port, EventLevel.Info,
                                "Read device interrupted: " + ex.Message);
                            throw;
                        }
                    }

                    Program.LogDb(rec.DeviceCode, rec.Ip + ":" + rec.Port, EventLevel.Debug,
                                "Done disconnected");

                    if (tempRows > 0)
                    {
                        using (SqlConnection conn = GetConnection())
                        {
                            conn.Open();

                            try
                            {
                                begin = DateTime.Now;
                                SqlBulkCopy dbWriter = new SqlBulkCopy(conn);
                                dbWriter.DestinationTableName = "dbo.[TM GET Temp]";
                                dbWriter.WriteToServer(dt);
                                until = DateTime.Now;
                                watch = until - begin;
                                Program.LogDb(rec.DeviceCode, rec.Ip + ":" + rec.Port, EventLevel.Debug,
                                    string.Format("Done save to dbo.[TM GET Temp] within {0} secs",
                                    watch.TotalSeconds.ToString(N_FORMAT)));
                                dt.Clear();
                            }
                            catch (Exception ex)
                            {
                                Program.LogDb(rec.DeviceCode, rec.Ip + ":" + rec.Port, EventLevel.Warning,
                                   "Error saving to dbo.[TM GET Temp]: " + ex.Message);
                                throw;
                            }

                            try
                            {
                                newupdate = 0;
                                begin = DateTime.Now;
                                using (SqlCommand cmd = conn.CreateCommand())
                                {
                                    cmd.CommandType = CommandType.Text;

                                    temp = null;
                                    if (OnlyDevice) temp += " AND [Device Code]=@dv";
                                    if (OnlyYearMonth) temp += " AND YEAR([Check Date])=@y AND MONTH([Check Date])=@m";
                                    if (temp != null) temp = "WHERE 1=1" + temp;

                                    cmd.Parameters.Add("@dv", SqlDbType.VarChar).Value = OnlyDevice ? DeviceCode : (object)DBNull.Value;
                                    cmd.Parameters.Add("@y", SqlDbType.Int).Value = OnlyYearMonth ? Year : (object)DBNull.Value;
                                    cmd.Parameters.Add("@m", SqlDbType.Int).Value = OnlyYearMonth ? Month : (object)DBNull.Value;

                                    cmd.CommandText =
                                        "WITH s AS (SELECT " +
                                        "[Hash Code],[Log Time],[Device Code],[Check Date],[Check Time], " +
                                        "[Check Type],[User ID],[Verify Code],[Sensor ID],[Work Code] " +
                                        "FROM [TM GET Temp] WITH(READUNCOMMITTED) " + temp + ") " +
                                        "MERGE INTO [TM GET History] AS t " +
                                        "USING s ON (s.[Hash Code]=t.[Hash Code]) " +
                                        "WHEN NOT MATCHED BY TARGET THEN " +
                                        "INSERT( " +
                                        "    [Hash Code],[Log Time],[Device Code],[Check Date],[Check Time], " +
                                        "    [Check Type],[User ID],[Verify Code],[Sensor ID],[Work Code]) " +
                                        "VALUES( " +
                                        "    s.[Hash Code],s.[Log Time],s.[Device Code],s.[Check Date],s.[Check Time], " +
                                        "    s.[Check Type],s.[User ID],s.[Verify Code],s.[Sensor ID],s.[Work Code]);";

                                    newupdate = cmd.ExecuteNonQuery();
                                }
                                until = DateTime.Now;
                                watch = until - begin;
                                Program.LogDb(rec.DeviceCode, rec.Ip + ":" + rec.Port, EventLevel.Debug,
                                    string.Format("Done merge to dbo.[TM GET History] within {0} secs (new update {1} record{2})",
                                    watch.TotalSeconds.ToString(N_FORMAT), newupdate.ToString(N_FORMAT), newupdate > 1 ? "s" : ""));
                            }
                            catch (Exception ex)
                            {
                                Program.LogDb(rec.DeviceCode, rec.Ip + ":" + rec.Port, EventLevel.Warning,
                                   "Error merging to dbo.[TM GET History]: " + ex.Message);
                                throw;
                            }

                            try
                            {
                                begin = DateTime.Now;
                                using (SqlCommand cmd = conn.CreateCommand())
                                {
                                    cmd.CommandType = CommandType.Text;

                                    temp = "WHERE a.[Check Date] BETWEEN DATEADD(MONTH,DATEDIFF(MONTH,0,GETDATE()),0) AND DATEADD(MONTH,DATEDIFF(MONTH,0,GETDATE())+1,-1) ";
                                    if (OnlyYearMonth) temp = "WHERE a.[Check Date] BETWEEN DATEADD(MONTH,DATEDIFF(MONTH,0,@dt),0) AND DATEADD(MONTH,DATEDIFF(MONTH,0,@dt)+1,-1) ";

                                    cmd.Parameters.Add("@dt", SqlDbType.Date).Value = OnlyYearMonth ? new DateTime(Year.Value, Month.Value, 1) : (object)DBNull.Value;
                                    cmd.Parameters.Add("@now", SqlDbType.DateTime).Value = now;

                                    cmd.CommandText =
                                        "WITH s AS (SELECT a.[User ID] " +
                                        "	,a.[Check Date] " +
                                        "    ,MIN(a.[Check Time]) AS [Check In] " +
                                        "    ,MAX(a.[Check Time]) AS [Check Out] " +
                                        "	,MAX(a.[Check Time])-MIN(a.[Check Time]) AS [Hours Gross] " +
                                        "	,CASE WHEN MAX(b.[Break Time]) IS NOT NULL THEN (CASE " +
                                        "	   WHEN MAX(a.[Check Time])<=MAX(b.[WShift Morning In]) THEN CAST('1900-01-01' AS DATETIME) " +
                                        "	   WHEN MIN(a.[Check Time])>=MAX(b.[WShift Noon Out]) THEN CAST('1900-01-01' AS DATETIME) " +
                                        "	   WHEN MAX(a.[Check Time])<=MAX(b.[WShift Noon In]) THEN ( " +
                                        "	     (CASE WHEN MAX(a.[Check Time])<MAX(b.[WShift Morning Out]) THEN MAX(a.[Check Time]) ELSE MAX(b.[WShift Morning Out]) END) " +
                                        "		-(CASE WHEN MIN(a.[Check Time])>MAX(b.[WShift Morning In]) THEN MIN(a.[Check Time]) ELSE MAX(b.[WShift Morning In]) END) " +
                                        "	   ) " +
                                        "	   WHEN MIN(a.[Check Time])>=MAX(b.[WShift Morning Out]) THEN ( " +
                                        "	     (CASE WHEN MAX(a.[Check Time])<MAX(b.[WShift Noon Out]) THEN MAX(a.[Check Time]) ELSE MAX(b.[WShift Noon Out]) END) " +
                                        "		-(CASE WHEN MIN(a.[Check Time])>MAX(b.[WShift Noon In]) THEN MIN(a.[Check Time]) ELSE MAX(b.[WShift Noon In]) END) " +
                                        "	   ) " +
                                        "	   ELSE ( " +
                                        "	     (CASE WHEN MAX(a.[Check Time])<MAX(b.[WShift Noon Out]) THEN MAX(a.[Check Time]) ELSE MAX(b.[WShift Noon Out]) END) " +
                                        "		-(CASE WHEN MIN(a.[Check Time])>MAX(b.[WShift Morning In]) THEN MIN(a.[Check Time]) ELSE MAX(b.[WShift Morning In]) END) " +
                                        "		-MAX(b.[Break Time]) " +
                                        "	   ) " +
                                        "	   END " +
                                        "	 ) " +
                                        "	 END AS [Hours by WShift] " +
                                        "    ,MAX(b.[WShift Morning In]) AS [WShift Morning In] " +
                                        "    ,MAX(b.[WShift Morning Out]) AS [WShift Morning Out] " +
                                        "    ,MAX(b.[WShift Noon In]) AS [WShift Noon In] " +
                                        "    ,MAX(b.[WShift Noon Out]) AS [WShift Noon Out] " +
                                        "	,GETDATE() AS [Computed Date] " +
                                        "	,MAX(b.[Person No]) AS [Person No] " +
                                        "	,MAX(b.[Employee No]) AS [Employee No] " +
                                        "FROM [TM GET History] a WITH(READUNCOMMITTED) " +
                                        "	LEFT JOIN [TM Profile Workshift] b ON a.[User ID]=b.[User ID] " + temp +
                                        "GROUP BY a.[User ID] " +
                                        "	,a.[Check Date]) " +
                                        "MERGE INTO [TM GET Summary] AS t " +
                                        "USING s ON (s.[User ID]=t.[User ID] AND s.[Check Date]=t.[Check Date]) " +
                                        "WHEN MATCHED THEN " +
                                        "UPDATE SET t.[Check In]=s.[Check In] " +
                                        ",t.[Check Out]=s.[Check Out] " +
                                        ",t.[WShift Morning In]=s.[WShift Morning In] " +
                                        ",t.[WShift Morning Out]=s.[WShift Morning Out] " +
                                        ",t.[WShift Noon In]=s.[WShift Noon In] " +
                                        ",t.[WShift Noon Out]=s.[WShift Noon Out] " +
                                        ",t.[Computed Date]=s.[Computed Date] " +
                                        ",t.[Hours Gross]=s.[Hours Gross] " +
                                        ",t.[Hours by WShift]=s.[Hours by WShift] " +
                                        ",t.[Person No]=s.[Person No] " +
                                        ",t.[Employee No]=s.[Employee No] " +
                                        "WHEN NOT MATCHED BY TARGET THEN " +
                                        "INSERT( " +
                                        "   [User ID],[Check Date],[Check In],[Check Out],[WShift Morning In],[WShift Morning Out],[WShift Noon In], " +
                                        "   [WShift Noon Out],[Computed Date],[Hours Gross],[Hours by WShift],[Person No],[Employee No]) " +
                                        "VALUES( " +
                                        "   s.[User ID],s.[Check Date],s.[Check In],s.[Check Out],s.[WShift Morning In],s.[WShift Morning Out],s.[WShift Noon In], " +
                                        "   s.[WShift Noon Out],@now,s.[Hours Gross],s.[Hours by WShift],s.[Person No],s.[Employee No]);";

                                    newupdate = cmd.ExecuteNonQuery();
                                }
                                until = DateTime.Now;
                                watch = until - begin;
                                Program.LogDb(rec.DeviceCode, rec.Ip + ":" + rec.Port, EventLevel.Debug,
                                    string.Format("Done merge to dbo.[TM GET Summary] within {0} secs (new update {1} record{2})",
                                    watch.TotalSeconds.ToString(N_FORMAT), newupdate.ToString(N_FORMAT), newupdate > 1 ? "s" : ""));
                            }
                            catch (Exception ex)
                            {
                                Program.LogDb(rec.DeviceCode, rec.Ip + ":" + rec.Port, EventLevel.Warning,
                                   "Error merging to dbo.[TM GET Summary]: " + ex.Message);
                                throw;
                            }

                            Program.LogDb(rec.DeviceCode, rec.Ip + ":" + rec.Port, EventLevel.Info,
                                string.Format("Successfully GET {0} record{1} ({2} bytes) within {3} secs (new update {4} record{5})",
                                tempRows.ToString(N_FORMAT), tempRows > 1 ? "s" : "", bytes.ToString(N_FORMAT),
                                networkspeed.ToString(N_FORMAT), newupdate.ToString(N_FORMAT), newupdate > 1 ? "s" : ""));
                        }
                    }
                }
                catch(Exception e)
                {
                    Program.ErrorLine("Error: " + e.Message);
                }
            } // foreach fingerprint device
        }
    }
}
