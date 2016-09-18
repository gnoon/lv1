using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.OleDb;

namespace FingerScan.Interface
{
    public class FingerprintMDB : IFingerprintDataSource
    {
        /// <summary>
        /// Connection to the mdb file
        /// </summary>
        protected OleDbConnection _oConn;
        protected OleDbCommand _oCmd;
        protected OleDbDataReader _oRs;
        /// <summary>
        /// Starting date of time attendance to read
        /// </summary>
        public DateTime StartingDate { get; set; }
        /// <summary>
        /// Until date of time attendance to read
        /// </summary>
        public DateTime UntilDate { get; set; }
        /// <summary>
        /// The boolean value identifies whether the device is connected
        /// </summary>
        private bool _IsConnected;
        /// <summary>
        /// Data Source Name of the mdb file (att2000.mdb)
        /// </summary>
        public string ConnectionString { get { return _ConnectionString; } set { _ConnectionString = value; } }
        private string _ConnectionString;
        /// <summary>
        /// Device Code of fingerprint scanner (see [TM Devices] table)
        /// </summary>
        public string DeviceCode { get; set; }
        /// <summary>
        /// All possible errors while operating with mdb, the key is LastError.
        /// </summary>
        public Dictionary<int, string> Errors { get { return _Errors; } }
        private Dictionary<int, string> _Errors = new Dictionary<int,string>();
        /// <summary>
        /// Datalink mode represents data reading period
        /// </summary>
        public bool DatalinkMode
        {
            set { _DatalinkMode = value; }
            get { return _DatalinkMode; }
        }
        private bool _DatalinkMode;
        /// <summary>
        /// Get last error code
        /// </summary>
        public int LastError { get { return _LastError; } }
        private int _LastError;

        public FingerprintMDB(string ConnectionString, DateTime StartingDate, DateTime UntilDate)
        {
            _ConnectionString = ConnectionString;
            this.StartingDate = StartingDate;
            this.UntilDate = UntilDate;
        }

        public void Connect()
        {
            if (_IsConnected) return;
            try
            {
                _oConn = new OleDbConnection(_ConnectionString);
                _oConn.Open();
                _IsConnected = true;
            }
            catch (Exception e)
            {
                _LastError = 0;
                if (_Errors.ContainsKey(_LastError)) _Errors[_LastError] = e.Message;
                else _Errors.Add(_LastError, e.Message);
            }

            if (!_IsConnected)
                throw new FScannerException(LastError.ToString());
        }

        public FingerprintReader CreateReader()
        {
            if (!_IsConnected)
                throw new ApplicationException("disconnected");
            return new FingerprintReader(this);
        }

        /// <summary>
        /// Read all the attendance records to the memory
        /// </summary>
        /// <returns></returns>
        public bool ReadToEnd()
        {
            if (!_IsConnected)
                throw new ApplicationException("disconnected");
            if (!_DatalinkMode)
                throw new ApplicationException("invalid state");
            try
            {
                _oCmd = _oConn.CreateCommand();
                _oCmd.CommandType = CommandType.Text;
                _oCmd.CommandText = "select t.*,u.Badgenumber from CHECKINOUT AS t,USERINFO AS u where t.USERID=u.USERID and t.CHECKTIME>=@1 and t.CHECKTIME<=@2";
                _oCmd.Parameters.Add("@1", OleDbType.Date).Value = StartingDate.Date;
                _oCmd.Parameters.Add("@2", OleDbType.Date).Value = UntilDate.Date;
                _oRs = _oCmd.ExecuteReader();
                _LoadInMemory = true;
            }
            catch (Exception e)
            {
                _LastError = 0;
                if (_Errors.ContainsKey(_LastError)) _Errors[_LastError] = e.Message;
                else _Errors.Add(_LastError, e.Message);
            }
            return _LoadInMemory;
        }
        private bool _LoadInMemory;

        /// <summary>
        /// Get the next attendance record, or null if no more
        /// </summary>
        public RawRecord Next
        {
            get
            {
                if (!_IsConnected)
                    throw new ApplicationException("disconnected");
                if (!_DatalinkMode)
                    throw new ApplicationException("invalid state");
                if (!_LoadInMemory)
                    throw new ApplicationException("invalid state");

                if (!_oRs.Read())
                    return null;

                RawRecord r = new RawRecord();
                r.EnrollNumber = _oRs.GetValue<int>("Badgenumber").ToString();
                r.VerifyMode = _oRs.GetValue<int>("VERIFYCODE");
                r.InOutMode = "I".Equals(_oRs.GetValue<string>("SENSORID"), StringComparison.InvariantCultureIgnoreCase) ? 0 : 1;
                DateTime checkTime = _oRs.GetValue<DateTime>("CHECKTIME");
                r.Year = checkTime.Year;
                r.Month = checkTime.Month;
                r.Day = checkTime.Day;
                r.Hour = checkTime.Hour;
                r.Minute = checkTime.Minute;
                r.Second = checkTime.Second;
                r.Workcode = _oRs.GetValue<int>("WorkCode");
                r.SensorID = _oRs.GetValue<int>("SENSORID");
                r.DeviceCode = this.DeviceCode;

                return r;
            }
        }

        private void _Close()
        {
            if (!_IsConnected) return;
            if (_LoadInMemory) _oRs.Close();
            if (_DatalinkMode) _oCmd.Dispose();
            _oConn.Close();
            _IsConnected = false;
        }

        #region IDisposable Members

        public void Dispose()
        {
            _Close();
            _oRs = null;
            _oCmd = null;
            _oConn = null;
        }

        #endregion
    }
}
