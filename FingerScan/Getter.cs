using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using System.Runtime.Serialization.Formatters.Binary;
using FingerScan.Interface;
using System.Security.Cryptography;
using System.Data.SqlClient;
using System.Threading;

namespace FingerScan
{
    public class Getter : IRunnable
    {
        DeviceRecord _Device;
        string _ConnString;
        string _ParamDeviceCode;
        int? _ParamYear;
        int? _ParamMonth;
        const string N_FORMAT = "#,##0";

        public Getter(DeviceRecord Device, string ConnString, string ParamDeviceCode, int? ParamYear, int? ParamMonth)
        {
            _Device = Device;
            _ConnString = ConnString;
            _ParamDeviceCode = ParamDeviceCode;
            _ParamYear = ParamYear;
            _ParamMonth = ParamMonth;
        }

        protected DataTable MemoryTempTable
        {
            get
            {
                DataTable dt = new DataTable("TM GET Temp");
                dt.Columns.Add("Hash Code");
                dt.Columns.Add("Log Time");
                dt.Columns.Add("Device Code");
                dt.Columns.Add("Check Date");
                dt.Columns.Add("Check Time");
                dt.Columns.Add("Check Type");
                dt.Columns.Add("User ID");
                dt.Columns.Add("Verify Code");
                dt.Columns.Add("Sensor ID");
                dt.Columns.Add("Work Code");
                return dt;
            }
        }

        #region IRunnable Members

        public void Run()
        {
            bool OnlyDevice = !string.IsNullOrEmpty(_ParamDeviceCode);
            bool OnlyYearMonth = _ParamMonth.HasValue && _ParamMonth.Value != 0 && _ParamYear.HasValue && _ParamYear.Value != 0;

            string CodeName = Thread.CurrentThread.Name;

            DateTime begin, until;
            TimeSpan watch;
            RawRecord r;
            DataRow row;
            string temp;
            DateTime now = DateTime.Now;
            int tempRows, totalNewUpdate = 0;
            double networkspeed;
            ulong bytes;
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream mem;
            try
            {
                DataTable dt = MemoryTempTable;

                using (MD5 md5 = MD5CryptoServiceProvider.Create())
                using (FingerprintScanner fscan = new FingerprintScanner(_Device.Ip, _Device.Port))
                {
                    try
                    {
                        fscan.Connect();
                        Program.LogDb(CodeName, _Device.DeviceCode, _Device.Ip + ":" + _Device.Port, EventLevel.Debug,
                            "Done connected");
                    }
                    catch (Exception ex)
                    {
                        Program.LogDb(CodeName, _Device.DeviceCode, _Device.Ip + ":" + _Device.Port, EventLevel.Info,
                            "Error connect device: " + ex.Message);
                        throw ex;
                    }
                    fscan.DeviceCode = _Device.DeviceCode;
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

                            if (OnlyYearMonth && r.Year != _ParamYear && r.Month != _ParamMonth)
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
                        totalNewUpdate += tempRows;

                        watch = until - begin;
                        networkspeed = watch.TotalSeconds;
                        Program.LogDb(CodeName, _Device.DeviceCode, _Device.Ip + ":" + _Device.Port, EventLevel.Debug,
                            string.Format("Done read {0} record{1} ({2} bytes) into memory within {3} secs",
                            dt.Rows.Count.ToString(N_FORMAT), dt.Rows.Count > 1 ? "s" : "", bytes.ToString(N_FORMAT),
                            networkspeed.ToString(N_FORMAT)));
                    }
                }

                Program.LogDb(CodeName, _Device.DeviceCode, _Device.Ip + ":" + _Device.Port, EventLevel.Debug,
                            "Done disconnected");

                if (tempRows > 0)
                {
                    using (SqlConnection conn = new SqlConnection(_ConnString))
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
                            Program.LogDb(CodeName, _Device.DeviceCode, _Device.Ip + ":" + _Device.Port, EventLevel.Debug,
                                string.Format("Done save to dbo.[TM GET Temp] within {0} secs",
                                watch.TotalSeconds.ToString(N_FORMAT)));
                            dt.Clear();
                        }
                        catch (Exception ex)
                        {
                            Program.LogDb(CodeName, _Device.DeviceCode, _Device.Ip + ":" + _Device.Port, EventLevel.Warning,
                               "Error saving to dbo.[TM GET Temp]: " + ex.Message);
                            throw ex;
                        }

                        Program.LogDb(CodeName, _Device.DeviceCode, _Device.Ip + ":" + _Device.Port, EventLevel.Info,
                            string.Format("Successfully GET {0} record{1} ({2} bytes) within {3} secs",
                            tempRows.ToString(N_FORMAT), tempRows > 1 ? "s" : "", bytes.ToString(N_FORMAT),
                            networkspeed.ToString(N_FORMAT)));
                    }
                }
                else
                {
                    Program.LogDb(CodeName, _Device.DeviceCode, _Device.Ip + ":" + _Device.Port, EventLevel.Info, "Successfully GET 0 record");
                }
            }
            catch (Exception e)
            {
                Program.ErrorLine("Error: " + e.Message);
            }
        }

        #endregion
    }
}
