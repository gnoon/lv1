using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FingerScan
{
    public interface IFingerprintDataSource : IDisposable
    {
        /// <summary>
        /// Fingerprint will not operate at frontend interface in datalink mode but backend via LAN interface.
        /// </summary>
        bool DatalinkMode { get; set; }
        /// <summary>
        /// Get last error code
        /// </summary>
        int LastError { get; }
        /// <summary>
        /// Get the next attendance record, or null if no more
        /// </summary>
        RawRecord Next { get; }
        /// <summary>
        /// Read all the attendance records to the memory
        /// </summary>
        bool ReadToEnd();
    }
}
