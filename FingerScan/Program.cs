using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NAttrArgs;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Data;
using System.Data.SqlClient;
using System.Data.OleDb;

namespace FingerScan
{
    internal enum EventLevel { Critical = 1, Error = 2, Info = 3, Warning = 4, Debug = 5 }
    internal class Cmd { public const string
        Help = "?", Get = "get", Clean = "clean", Conso = "conso", Build = "build", Sync = "sync", Check = "check";
    }
    internal class Program
    {
        string _CmdDescription = Environment.NewLine + Environment.NewLine
            + "?        - display this help message" + Environment.NewLine
            + "check    - check if configuration in file FingerScan.exe.config is working" + Environment.NewLine
            + "get      - get transactions from fingerprint scanners, and save in datacenter" + Environment.NewLine
            //+ "clean    - clean transactions in fingerprint scanners" + Environment.NewLine
            + "conso    - consolidate transactions in datacenter by last month as default" + Environment.NewLine
            //+ "build    - build a list of users from a master fingerprint scanner" + Environment.NewLine
            //+ "sync     - sync the list of users to fingerprint scanners" + Environment.NewLine
            + Environment.NewLine
            + "For example" + Environment.NewLine
            + Environment.NewLine
            + "fingerscan.exe get" + Environment.NewLine
            + "fingerscan.exe -ym 201309 get";

        public static void OutLine(string s)
        {
            Console.Out.WriteLine(s);
        }
        public static void Out(string s)
        {
            Console.Out.Write(s);
        }
        public static void ErrorLine(string s)
        {
            Console.Error.WriteLine(s);
        }
        public static void Error(string s)
        {
            Console.Error.Write(s);
        }
        /// <summary>
        /// Save log message to memory
        /// </summary>
        /// <param name="DateTime"></param>
        /// <param name="DeviceCode"></param>
        /// <param name="DeviceIP"></param>
        /// <param name="Level"></param>
        /// <param name="Message"></param>
        public static void LogDb(string DeviceCode, string DeviceIP, EventLevel Level, string Message)
        {
            if (_Log == null)
            {
                string _ConnString = FingerScan.Properties.Settings.Default.TIMEATT;
                _Log = new ActivityLog(_ConnString);
            }
            _Log.Add(_LogTime, DeviceCode, DeviceIP, Level, Message);
        }
        /// <summary>
        /// Save log messages to database
        /// </summary>
        static void _LogCommit()
        {
            if (_Log != null)
            {
                try
                {
                    _Log.Commit();
                    _Log = null;
                }
                catch
                {
                    _SaveLogFile();
                }
            }
        }
        static void _SaveLogFile()
        {
            // TODO
        }
        static ActivityLog _LoadLogFile()
        {
            // TODO
            return null;
        }

        /// <summary>
        /// Session id of this running instance
        /// </summary>
        static string _Session;
        /// <summary>
        /// Message log of this session, must set to null when committed to db
        /// </summary>
        static ActivityLog _Log;
        /// <summary>
        /// When this session was started
        /// </summary>
        static DateTime _LogTime;
        /// <summary>
        /// File name to save uncommit ActivityLog
        /// </summary>
        const string _LogFileName = "eventlog.obj";
        /// <summary>
        /// Activity of this session
        /// </summary>
        public static string Activity { get { return _Activity; } }
        static string _Activity;
        /// <summary>
        /// Command line of this session
        /// </summary>
        public static string Source { get { return _Source; } }
        static string _Source;
        /// <summary>
        /// Version of current assembly
        /// </summary>
        public static string ThisVersion { get { return _Version; } }
        static string _Version;
        const string _App = "fingerscan.exe";
        /// <summary>
        /// Startup here
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            int m = 8000;
            var ds = new TimeSpan(0, m, 0);
            var str = ds.ToString("d':'hh':'mm");
            Console.WriteLine(str);

            string s = "surapon" + "e02f9fa754ae2c8448ce47ca5cdc7090a5276e788e528e8f96fb99e26aad644c";
            byte[] b = System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.ASCII.GetBytes(s));
            StringBuilder sPassword = new StringBuilder();

            for (int i = 0; i < b.Length; i++)
            {
                sPassword.Append(b[i].ToString("x2"));
            }

            string ss = sPassword.ToString();
            var q = 1;
            Console.WriteLine(q);

            return;

            _Session = Guid.NewGuid().ToString("P").ToUpper();
            _LogTime = DateTime.Now;
            _Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            try
            {
                Program prog = new Program();
                prog.Run(args);
            }
            catch (NArgException e)
            {
                ErrorLine(e.Message);
            }
            catch (Exception e)
            {
                ErrorLine(e.Message);
            }
        }

        void Run(string[] args)
        {
            // DEBUG
            //args = new string[] { "-from", "20140201", "-to", "20140210", Cmd.Conso };

            ArgParser<Program> parser = new ArgParser<Program>(_App);
            parser.Parse(this, args);

            _Source = string.Format("{0} {1}", _App, string.Join(" ", args));
            _Log = _LoadLogFile();

            if (Cmd.Get.Equals(_Cmd) || Cmd.Conso.Equals(_Cmd))
            {
                string m = string.Format("Starting session {0}", _Session, _Source);
                Program.OutLine(m);

                try
                {
                    ValidateParams();

                    string _ConnString = FingerScan.Properties.Settings.Default.TIMEATT;
                    string _MDBConnString = FingerScan.Properties.Settings.Default.ATT2000;
                    int _OffsetDays = FingerScan.Properties.Settings.Default.ATT2000_StartingReadDays;

                    DateTime _UntilDate = oYmd2.HasValue ? oYmd2.Value : DateTime.Now.Date;
                    DateTime _StartingDate = oYmd1.HasValue ? oYmd1.Value : _UntilDate.AddDays(_OffsetDays > 0 ? -366 * 3 : _OffsetDays);

                    string s = string.Format("Parameters: {0} - {1} (ID:{2})", _StartingDate.ToString("dd/MM/yyyy"), _UntilDate.ToString("dd/MM/yyyy"), iPK);
                    Program.OutLine(s);

                    if (Cmd.Get.Equals(_Cmd))
                    {
                        _Activity = Cmd.Get;

                        // Must be called after the Activity property is already assigned
                        Program.LogDb(null, null, EventLevel.Info, s);
                        Program.LogDb(null, null, EventLevel.Info, m);

                        new ActionGETMDB(_ConnString, _MDBConnString, _StartingDate, _UntilDate, iPK).Run(null, null, null);
                    }
                    else if (Cmd.Conso.Equals(_Cmd))
                    {
                        _Activity = Cmd.Conso;

                        // Must be called after the Activity property is already assigned
                        Program.LogDb(null, null, EventLevel.Info, s);
                        Program.LogDb(null, null, EventLevel.Info, m);

                        new ActionCONSO(_ConnString, _StartingDate, _UntilDate, iPK).Run(null, null, null);
                    }
                }
                catch (Exception ex)
                {
                    // TODO: send mail
                    throw;
                }
                finally
                {
                    m = "Finished session " + _Session;
                    Program.OutLine(m);
                    Program.LogDb(null, null, EventLevel.Info, m);
                    _LogCommit();
                }
            }
            else if (Cmd.Check.Equals(_Cmd))
            {
                Check();
            }
            else
            {
                OutLine(parser.GetUsage() + _CmdDescription);
            }
        }

        private void Check()
        {
            try
            {
                string _ConnString = FingerScan.Properties.Settings.Default.TIMEATT;
                Program.Out("Checking 'TIMEATT' connection string ... ");
                using (SqlConnection conn = new SqlConnection(_ConnString))
                    conn.Open();
                Program.OutLine("Success");
            }
            catch(Exception e)
            {
                Program.OutLine("Failed" + Environment.NewLine + ">>> " + e.Message);
            }
            try
            {
                string _ConnString = FingerScan.Properties.Settings.Default.LEAVE;
                Program.Out("Checking 'LEAVE' connection string ... ");
                using (SqlConnection conn = new SqlConnection(_ConnString))
                    conn.Open();
                Program.OutLine("Success");
            }
            catch (Exception e)
            {
                Program.OutLine("Failed" + Environment.NewLine + ">>> " + e.Message);
            }
            try
            {
                string _ConnString = FingerScan.Properties.Settings.Default.ATT2000;
                Program.Out("Checking 'ATT2000' connection string ... ");
                using (OleDbConnection conn = new OleDbConnection(_ConnString))
                    conn.Open();
                Program.OutLine("Success");
            }
            catch (Exception e)
            {
                Program.OutLine("Failed" + Environment.NewLine + ">>> " + e.Message);
            }
        }

        private bool ValidateParams()
        {
            if (_Ymd1 != null || _Ymd2 != null)
            {
                DateTime dt = DateTime.MinValue;
                CultureInfo c = new CultureInfo("en-US");
                if (_Ymd1 != null && !DateTime.TryParseExact(_Ymd1, "yyyyMMdd",c , DateTimeStyles.None, out dt))
                    throw new Exception(_Ymd1);
                oYmd1 = dt == DateTime.MinValue ? (DateTime?)null : dt;

                dt = DateTime.MinValue;
                if (_Ymd2 != null && !DateTime.TryParseExact(_Ymd2, "yyyyMMdd",c , DateTimeStyles.None, out dt))
                    throw new Exception(_Ymd2);
                oYmd2 = dt == DateTime.MinValue ? (DateTime?)null : dt;
            }
            if (Cmd.Get.Equals(_Cmd, StringComparison.InvariantCultureIgnoreCase) ||
               Cmd.Conso.Equals(_Cmd, StringComparison.InvariantCultureIgnoreCase))
            {
                long id;
                if (_Pk != null && !long.TryParse(_Pk, out id))
                    throw new Exception(_Pk);
                if(_Pk != null)
                    iPK = long.Parse(_Pk);
            }
            return true;
        }

        [NArg(AllowedValues = new string[] { Cmd.Help, Cmd.Get, Cmd.Conso, Cmd.Check }, Rank = 1)]
        string _Cmd;

        [NArg(IsOptional = true, OptionalArgName = "yyyyMMdd", AltName = "from")]
        string _Ymd1;
        DateTime? oYmd1;

        [NArg(IsOptional = true, OptionalArgName = "yyyyMMdd", AltName = "to")]
        string _Ymd2;
        DateTime? oYmd2;

        [NArg(IsOptional = true, OptionalArgName = "Number", AltName = "logid")]
        string _Pk;
        long iPK;

        //[NArg(IsOptional = true, OptionalArgName = "devicecode", AltName = "device")]
        string _DeviceCode;
    }
}
