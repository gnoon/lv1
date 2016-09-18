using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zkemkeeper;

namespace FingerScan.Interface
{
    public class FingerprintScanner : IFingerprintDataSource
    {
        /// <summary>
        /// Standalone SDK class dynamicly.
        /// </summary>
        private CZKEMClass _axCZKEM1;
        /// <summary>
        /// The boolean value identifies whether the device is connected
        /// </summary>
        private bool _IsConnected;
        /// <summary>
        /// The serial number of the device. After connecting the device ,this value will be changed.
        /// In fact, when you are using the TCP/IP communication, this parameter will be ignored. That is any integer will all right. Here we use 1.
        /// </summary>
        private int MachineNumber = 1;
        /// <summary>
        /// Here you can register the realtime events that you want to be triggered(the parameters 65535 means registering all)
        /// </summary>
        private int EventMask = 65535;
        /// <summary>
        /// IP of fingerprint scanner
        /// </summary>
        public string Ip { get { return _Ip; } set { _Ip = value; } }
        private string _Ip;
        /// <summary>
        /// Port of service running on fingerprint scanner. Default is 4370.
        /// </summary>
        public int Port { get { return _Port; } set { _Port = value; } }
        private int _Port = 4370;
        /// <summary>
        /// Device Code of fingerprint scanner (see [TM Devices] table)
        /// </summary>
        public string DeviceCode { get; set; }
        /// <summary>
        /// Fingerprint will not operate at frontend interface in datalink mode but backend via LAN interface.
        /// </summary>
        public bool DatalinkMode
        {
            set
            {
                if (_IsConnected)
                    _axCZKEM1.EnableDevice(MachineNumber, !value);
                _DatalinkMode = value;
            }
            get { return _DatalinkMode; }
        }
        private bool _DatalinkMode;
        /// <summary>
        /// Get last error code
        /// </summary>
        public int LastError
        {
            get
            {
                int idwErrorCode = 0;
                _axCZKEM1.GetLastError(ref idwErrorCode);
                return idwErrorCode;
            }
        }

        public FingerprintScanner(string Ip, int Port)
        {
            _axCZKEM1 = new CZKEMClass();
            _Ip = Ip;
            _Port = Port;
        }

        public void Connect()
        {
            if (_IsConnected) return;
            _IsConnected = _axCZKEM1.Connect_Net(Ip, Port);

            if (!_IsConnected)
                throw new FScannerException(LastError.ToString());

            _axCZKEM1.RegEvent(MachineNumber, EventMask);
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
            _LoadInMemory = _axCZKEM1.ReadGeneralLogData(MachineNumber);
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

                string sdwEnrollNumber = "";
                int idwVerifyMode = 0;
                int idwInOutMode = 0;
                int idwYear = 0;
                int idwMonth = 0;
                int idwDay = 0;
                int idwHour = 0;
                int idwMinute = 0;
                int idwSecond = 0;
                int idwWorkcode = 0;

                bool any = _axCZKEM1.SSR_GetGeneralLogData(MachineNumber, out sdwEnrollNumber, out idwVerifyMode,
                               out idwInOutMode, out idwYear, out idwMonth, out idwDay, out idwHour,
                               out idwMinute, out idwSecond, ref idwWorkcode);
                if (!any)
                    return null;

                RawRecord r = new RawRecord();
                r.EnrollNumber = sdwEnrollNumber;
                r.VerifyMode = idwVerifyMode;
                r.InOutMode = idwInOutMode;
                r.Year = idwYear;
                r.Month = idwMonth;
                r.Day = idwDay;
                r.Hour = idwHour;
                r.Minute = idwMinute;
                r.Second = idwSecond;
                r.Workcode = idwWorkcode;
                r.SensorID = this.MachineNumber;
                r.DeviceCode = this.DeviceCode;

                return r;
            }
        }

        private void _Close()
        {
            if (!_IsConnected) return;
            _axCZKEM1.Disconnect();
            _IsConnected = false;
        }

        #region IDisposable Members

        public void Dispose()
        {
            _Close();
            _axCZKEM1 = null;
        }

        #endregion
    }
}
