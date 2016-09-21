using System;
using System.Collections.Generic;

namespace LeaveCore
{
    /// <summary>
    /// Exception ที่ใช้ใน Business Process ของ Leave system
    /// </summary>
    public class LeaveException : Exception
    {
        private Int64 _RequestID;
        public Int64 RequestID { get { return _RequestID; } }

        private string _PersonNo;
        public string PersonNo { get { return _PersonNo; } }

        private string _Message;
        public new string Message { get { return _Message ?? RequestID.ToString(); } }

        public LeaveException(string Message, Exception innerException)
            : this(null, 0, Message, innerException)
        {
        }

        public LeaveException(string PersonNo, Int64 RequestID)
            : this(PersonNo, RequestID, null, null)
        {
        }

        public LeaveException(string PersonNo, Int64 RequestID, string Message, Exception innerException)
            : base(Message, innerException)
        {
            this._RequestID = RequestID;
            this._PersonNo = PersonNo;
            this._Message = Message;
        }
    }

    /// <summary>
    /// Exception ที่ใช้ในฟังก์ชัน NewLeaveRequest
    /// </summary>
    public class LeaveRequestException : LeaveException
    {
        public LeaveRequestException(string PersonNo, Int64 RequestID)
            : base(PersonNo, RequestID)
        {
        }

        public LeaveRequestException(string PersonNo, Int64 RequestID, Exception innerException)
            : base(PersonNo, RequestID, null, innerException)
        {
        }
    }
    

    /// <summary>
    /// Exception ที่ใช้ในฟังก์ชัน Grant
    /// </summary>
    public class LeaveGrantException : LeaveException
    {
        private int _NewStatusID;
        public int NewStatusID { get { return _NewStatusID; } }

        private string _Message;
        public new string Message { get { return _Message; } }

        public LeaveGrantException(string HeadPersonNo, Int64 RequestID, int NewStatusID, string Message)
            : base(HeadPersonNo, RequestID)
        {
            this._NewStatusID = NewStatusID;
            this._Message = Message;
        }

        public LeaveGrantException(string HeadPersonNo, Int64 RequestID, int NewStatusID, string Message, Exception innerException)
            : base(HeadPersonNo, RequestID, null, innerException)
        {
            this._NewStatusID = NewStatusID;
            this._Message = Message;
        }
    }

    /// <summary>
    /// Exception ที่ใช้ในฟังก์ชัน SetLeaveParam
    /// </summary>
    public class LeaveParameterException : LeaveException
    {
        private string _ParameterName;
        public string ParameterName { get { return _ParameterName; } }

        private string _ParameterInputValue;
        public string ParameterInputValue { get { return _ParameterInputValue; } }

        public LeaveParameterException(string PersonNo, string ParameterName, string ParameterInputValue)
            : base(PersonNo, 0)
        {
            this._ParameterName = ParameterName;
            this._ParameterInputValue = ParameterInputValue;
        }
    }

    /// <summary>
    /// Exception ที่ใช้ในฟังก์ชัน CheckDuplication
    /// </summary>
    public class LeaveRequestDuplicationException : LeaveRequestException
    {
        private List<LeaveRecord> _List;
        public List<LeaveRecord> DuplicationList { get { return _List; } }

        public LeaveRequestDuplicationException(string PersonNo, List<LeaveRecord> DuplicationList)
            : base(PersonNo, 0)
        {
            this._List = DuplicationList;
        }
    }

    public class LeaveRequestQuotaExceedException : LeaveRequestException
    {
        private int _LeaveSubType;
        public int LeaveSubType { get { return _LeaveSubType; } }

        private int _Year;
        public int Year { get { return _Year; } }

        private int _Quota;
        public int Quota { get { return _Quota; } }

        private int _Request;
        public int Request { get { return _Request; } }

        public LeaveRequestQuotaExceedException(string PersonNo, int LeaveSubType, int Year, int Quota, int Request)
            : base(PersonNo, 0)
        {
            this._LeaveSubType = LeaveSubType;
            this._Year = Year;
            this._Quota = Quota;
            this._Request = Request;
        }
    }

    public class LeaveRequestPreDateException : LeaveRequestException
    {
        private int _LeaveSubType;
        public int LeaveSubType { get { return _LeaveSubType; } }

        private DateTime _LeaveBeginDate;
        public DateTime LeaveBeginDate { get { return _LeaveBeginDate; } }

        private DateTime _ApplyDate;
        public DateTime ApplyDate { get { return _ApplyDate; } }

        /// <summary>
        /// วันที่ที่สามารถเริ่มลาประเภทนี้ได้
        /// </summary>
        public DateTime AllowingLeaveBeginDate
        {
            get
            {
                return _ApplyDate.AddDays(_DaysToAllow);
            }
        }

        private int _DaysToAllow;
        public int DaysToAllow { get { return _DaysToAllow; } }

        public LeaveRequestPreDateException(string PersonNo, int LeaveSubType,
            DateTime ApplyDate, DateTime LeaveBeginDate, int DaysToAllow)
            : base(PersonNo, 0)
        {
            this._LeaveSubType = LeaveSubType;
            this._LeaveBeginDate = LeaveBeginDate;
            this._ApplyDate = ApplyDate;
            this._DaysToAllow = DaysToAllow;
        }
    }

    public class LeaveRequestEffectiveVacationException : LeaveRequestException
    {
        private DateTime _BaseDate;
        public DateTime BaseDate { get { return _BaseDate; } }

        /// <summary>
        /// วันที่ที่สามารถเริ่มลาประเภทนี้ได้
        /// </summary>
        public DateTime EffectiveDate
        {
            get
            {
                return new DateTime(_BaseDate.Year, _BaseDate.Month, _BaseDate.Day).AddMonths(_MonthsToAllow);
            }
        }

        private int _MonthsToAllow;
        public int MonthsToAllow { get { return _MonthsToAllow; } }

        public LeaveRequestEffectiveVacationException(string PersonNo, DateTime BaseDate, int MonthsToAllow)
            : base(PersonNo, 0)
        {
            this._BaseDate = BaseDate;
            this._MonthsToAllow = MonthsToAllow;
        }
    }

    public class LeaveRequestMedicalCertificateException : LeaveRequestException
    {
        private DateTime _LeaveBeginDate;
        public DateTime LeaveBeginDate { get { return _LeaveBeginDate; } }

        private DateTime _LeaveUntilDate;
        public DateTime LeaveUntilDate { get { return _LeaveUntilDate; } }

        private int _DaysRequireCer;
        public int DaysRequireCer { get { return _DaysRequireCer; } }

        public LeaveRequestMedicalCertificateException(string PersonNo, DateTime LeaveBeginDate, DateTime LeaveUntilDate, int DaysRequireCer)
            : base(PersonNo, 0)
        {
			this._LeaveBeginDate = LeaveBeginDate;
			this._LeaveUntilDate = LeaveUntilDate;
			this._DaysRequireCer = DaysRequireCer;
        }
    }
}
