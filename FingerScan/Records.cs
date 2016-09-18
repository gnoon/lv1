using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FingerScan
{
    public class DeviceRecord
    {
        internal string DeviceCode { get; set; }
        internal string Ip { get; set; }
        internal int Port { get; set; }
        internal string Branch { get; set; }
        internal string Company { get; set; }
        internal string Admin { get; set; }
        internal string Tel { get; set; }
        internal string Email { get; set; }
        internal DateTime? LastModified { get; set; }
    }
    
    [Serializable]
    public class RawRecord
    {
        internal string EnrollNumber { get; set; }
        internal int VerifyMode { get; set; }
        internal int InOutMode { get; set; }
        internal int Year { get; set; }
        internal int Month { get; set; }
        internal int Day { get; set; }
        internal int Hour { get; set; }
        internal int Minute { get; set; }
        internal int Second { get; set; }
        internal int Workcode { get; set; }
        internal int SensorID { get; set; }
        internal string DeviceCode { get; set; }
    }

    public class LogRecord
    {
        internal DateTime Date { get; set; }
        internal DateTime Time { get; set; }
        internal string Activity { get; set; }
        internal string DeviceCode { get; set; }
        internal string DeviceIP { get; set; }
        internal EventLevel Level { get; set; }
        internal string Source { get; set; }
        internal string Version { get; set; }
        internal string Message { get; set; }
    }

    public class WorkshiftRecord
    {
        internal string PersonNo { get; set; }
        internal string EmployeeNo { get; set; }
        internal int UserID { get; set; }
        internal TimeSpan? MorningIn { get; set; }
        internal TimeSpan? MorningOut { get; set; }
        internal TimeSpan? AfternoonIn { get; set; }
        internal TimeSpan? AfternoonOut { get; set; }
        internal TimeSpan? BreakTime { get; set; }
    }
}
