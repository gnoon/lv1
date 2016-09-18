using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data;

namespace FingerScan
{
    public abstract class ActionBASE
    {
        private string _ConnectionString;
        protected const string N_FORMAT = "#,##0";

        public ActionBASE(string ConnectionString)
        {
            this._ConnectionString = ConnectionString;
        }

        protected SqlConnection GetConnection()
        {
            SqlConnection objConn = new SqlConnection(_ConnectionString);
            return objConn;
        }

        /// <summary>
        /// Abstract method
        /// </summary>
        /// <param name="DeviceCode"></param>
        /// <param name="Month"></param>
        /// <param name="Year"></param>
        public abstract void Run(string DeviceCode, int? Month, int? Year);

        protected List<DeviceRecord> AllDevices()
        {
            return QueryDevice("SELECT *,NULL AS [Last Modified] FROM [TM Devices] WITH(READUNCOMMITTED)" ,null);
        }

        protected List<DeviceRecord> GETDevices()
        {
            return QueryDevice(
                "SELECT a.*,b.[Last Modified] FROM [TM Devices] a WITH(READUNCOMMITTED) INNER JOIN [TM GET Devices] b WITH(READUNCOMMITTED) "+
                "ON a.[Device Code] = b.[Device Code] WHERE b.[Enabled] = 1"
                , null);
        }

        protected List<DeviceRecord> CLEANDevices()
        {
            return QueryDevice(
                "SELECT a.*,b.[Last Modified] FROM [TM Devices] a WITH(READUNCOMMITTED) INNER JOIN [TM CLEAN Devices] b WITH(READUNCOMMITTED) " +
                "ON a.[Device Code] = b.[Device Code] WHERE b.[Enabled] = 1"
                , null);
        }

        protected List<DeviceRecord> MASTERDevices()
        {
            return QueryDevice(
                "SELECT a.*,b.[Last Modified] FROM [TM Devices] a WITH(READUNCOMMITTED) INNER JOIN [TM MASTER Devices] b WITH(READUNCOMMITTED) " +
                "ON a.[Device Code] = b.[Device Code] WHERE b.[Enabled] = 1"
                , null);
        }

        protected List<DeviceRecord> SYNCDevices()
        {
            return QueryDevice(
                "SELECT a.*,b.[Last Modified] FROM [TM Devices] a WITH(READUNCOMMITTED) INNER JOIN [TM SYNCPERSON Devices] b WITH(READUNCOMMITTED) " +
                "ON a.[Device Code] = b.[Device Code] WHERE b.[Enabled] = 1"
                , null);
        }

        private List<DeviceRecord> QueryDevice(string Query, List<SqlParameter> Params)
        {
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandText = Query;
                    if (Params != null)
                        cmd.Parameters.AddRange(Params.ToArray());
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        List<DeviceRecord> list = new List<DeviceRecord>();
                        DeviceRecord rec;
                        while(rs.Read())
                        {
                            rec = new DeviceRecord();
                            rec.DeviceCode = rs.GetValue<string>("Device Code");
                            rec.Ip = rs.GetValue<string>("IP");
                            rec.Port = rs.GetValue<int>("Port");
                            rec.Branch = rs.GetValue<string>("Branch");
                            rec.Company = rs.GetValue<string>("Company");
                            rec.Admin = rs.GetValue<string>("Admin");
                            rec.Tel = rs.GetValue<string>("Tel");
                            rec.Email = rs.GetValue<string>("Email");
                            rec.LastModified = rs.GetValue<DateTime?>("Last Modified");
                            list.Add(rec);
                        }
                        return list;
                    }
                }
            }
        }

        protected List<WorkshiftRecord> WorkshiftProfiles()
        {
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandText = "SELECT * FROM [TM Profile Workshift]";
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        List<WorkshiftRecord> list = new List<WorkshiftRecord>();
                        WorkshiftRecord rec;
                        while (rs.Read())
                        {
                            rec = new WorkshiftRecord();
                            rec.PersonNo = rs.GetValue<string>("Person No");
                            rec.EmployeeNo = rs.GetValue<string>("Employee No");
                            rec.UserID = rs.GetValue<int>("User ID");
                            rec.MorningIn = rs.GetValue<TimeSpan?>("WShift Morning In");
                            rec.MorningOut = rs.GetValue<TimeSpan?>("WShift Morning Out");
                            rec.AfternoonIn = rs.GetValue<TimeSpan?>("WShift Noon In");
                            rec.AfternoonOut = rs.GetValue<TimeSpan?>("WShift Noon Out");
                            rec.BreakTime = rs.GetValue<TimeSpan?>("Break Time");
                            list.Add(rec);
                        }
                        return list;
                    }
                }
            }
        }

        protected int CleanTempTable()
        {
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandText = "TRUNCATE TABLE [TM GET Temp]";
                    return cmd.ExecuteNonQuery();
                }
            }
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
    }
}
