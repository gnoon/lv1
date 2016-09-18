using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Data;
using System.Data.SqlClient;

namespace FingerScan
{
    [DataContract]
    public class ActivityLog
    {
        [IgnoreDataMember]
        private string _ConnectionString;

        [DataMember]
        private List<LogRecord> List;

        public ActivityLog(string ConnectionString)
        {
            _ConnectionString = ConnectionString;
            List = new List<LogRecord>();
        }

        internal void Add(DateTime DateTime, string DeviceCode, string DeviceIP, EventLevel Level, string Message)
        {
            TimeSpan ts = DateTime.TimeOfDay;
            LogRecord l = new LogRecord();
            l.Activity = Program.Activity;
            l.Date = DateTime.Date;
            l.Time = new DateTime(1900, 1, 1, ts.Hours, ts.Minutes, ts.Seconds);
            l.DeviceCode = DeviceCode;
            l.DeviceIP = DeviceIP;
            l.Level = Level;
            l.Message = Message;
            l.Source = Program.Source;
            l.Version = Program.ThisVersion;
            List.Add(l);
        }

        internal void Add(LogRecord l)
        {
            List.Add(l);
        }

        private DataTable MemoryTable
        {
            get
            {
                DataTable dt = new DataTable("TM Log");
                dt.Columns.Add("Date");
                dt.Columns.Add("Time");
                dt.Columns.Add("Activity");
                dt.Columns.Add("Device Code");
                dt.Columns.Add("Device IP");
                dt.Columns.Add("Level");
                dt.Columns.Add("Source");
                dt.Columns.Add("Version");
                dt.Columns.Add("Message");
                return dt;
            }
        }

        internal void Commit()
        {
            DataRow row;
            DataTable dt = MemoryTable;
            int C = List.Count;
            for (int i = 0; i < C; i++)
            {
                row = dt.NewRow();
                row["Date"] = List[i].Date;
                row["Time"] = List[i].Time;
                row["Activity"] = List[i].Activity;
                row["Device Code"] = List[i].DeviceCode;
                row["Device IP"] = List[i].DeviceIP;
                row["Level"] = (int)List[i].Level;
                row["Source"] = List[i].Source;
                row["Version"] = List[i].Version;
                row["Message"] = List[i].Message;
                dt.Rows.Add(row);
            }

            using (SqlConnection conn = new SqlConnection(_ConnectionString))
            {
                conn.Open();
                SqlBulkCopy dbWriter = new SqlBulkCopy(conn);
                dbWriter.DestinationTableName = "dbo.[TM Log]";
                dbWriter.WriteToServer(dt);
            }

            dt.Clear();
            List.Clear();
        }
    }
}
