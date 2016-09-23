
using System;
using System.Collections.Generic;

namespace LeaveCore
{
	#region Leave

    public class QuotaRecord
    {
        public int PeriodID { get; set; }
        public string PersonNo { get; set; }
        public TypeRecord LeaveType { get; set; }
        public decimal QuotaAmount { get; set; }
        public decimal QuotaPrevAmount { get; set; }
        public int TakenMinutes { get; set; }
        //public decimal TakenAmount { get { var d = TakenHours / Const.DEFAULT_WORKHOURS_OF_DAY; return d; } }
        public int ApproveMinutes { get; set; }
        //public decimal ApproveAmount { get { var d = (ApproveMinutes / 60m) / Const.DEFAULT_WORKHOURS_OF_DAY; return d; } }
        public int Days30Minutes { get; set; }
        //public decimal Days30Amount { get { var d = (Days30Minutes / 60m) / Const.DEFAULT_WORKHOURS_OF_DAY; return d; } }
        public DateTime? ModifyDate { get; set; }
        public string ModifyPerson { get; set; }

        /// <summary>
        /// สำหรับมุมมองพนักงาน - BalanceMinutes คือ ยอดที่ลาได้อีก
        /// </summary>
		public int BalanceMinutes
		{
			get
			{
                return Convert.ToInt32(QuotaAmount * 60 * Const.DEFAULT_WORKHOURS_OF_DAY) +
                       Convert.ToInt32(QuotaPrevAmount * 60 * Const.DEFAULT_WORKHOURS_OF_DAY) -
                       TakenMinutes;
			}
		}

        /// <summary>
        /// สำหรับมุมมองหัวหน้า - RemainAmount คือ ยอดที่เหลือจากอนุมัติแล้ว
        /// </summary>
        public int RemainMinutes
        {
            get
            {
                return Convert.ToInt32(QuotaAmount * 60 * Const.DEFAULT_WORKHOURS_OF_DAY) +
                       Convert.ToInt32(QuotaPrevAmount * 60 * Const.DEFAULT_WORKHOURS_OF_DAY) -
                       ApproveMinutes;
            }
        }
    }

    public class TypeRecord
    {
        public string TypeNo { get; set; }
        public int TypeSubID { get; set; }
        public string NameTH { get; set; }
        public string NameEN { get; set; }
        public string Category { get; set; }
    }

    public class PersonRecord
    {
        public string PersonNo { get; set; }
        public string PrefixTH { get; set; }
        public string PrefixEN { get; set; }
        public string NameFirstTH { get; set; }
        public string NameFirstEN { get; set; }
        public string NameLastTH { get; set; }
        public string NameLastEN { get; set; }
		public string Email { get; set; }
		public string Mobile { get; set; }
		public string Gender { get; set; }
		public string Password { get; set; }
		public string ADAccount { get; set; }
		public int UsePasswordDefault { get; set; }
		public EmployeeRecord Employee { get; set; }
	}

	public class EmployeeRecord
    {
        public string EmployeeNo { get; set; }
        public DateTime? StartingDate { get; set; }
        public DateTime? UntilDate { get; set; }
        public string CompanyCode { get; set; }
        public string Department { get; set; }
        public string Section { get; set; }
		public string PositionTH { get; set; }
		public string PositionEN { get; set; }

		public string DisplaySection
		{
			get 
			{
                if (Department != null && Department.Equals(Section))
					return Department;
                return string.Format("{0}/{1}", Department ?? "-", Section ?? "-");
			}
		}
    }

    public class HolidayRecord
    {
        public string PersonNo { get; set; }
        public DateTime? Date { get; set; }
        public string NameTH { get; set; }
        public string NameEN { get; set; }
        public string Remark { get; set; }
        public DateTime? ModifyDate { get; set; }
        public string ModifyPerson { get; set; }
    }

    public class WeekendRecord
    {
        public string PersonNo { get; set; }
        public DayOfWeek? DayOfWeek { get; set; }
        public int StartingWeekOfMonth { get; set; }
        public int EveryNWeekOfMonth { get; set; }
        public int ExcludeWeekOfMonth { get; set; }
        public string NameTH { get; set; }
        public string NameEN { get; set; }
        public string Remark { get; set; }
        public DateTime? ModifyDate { get; set; }
        public string ModifyPerson { get; set; }
    }

    public class WorkshiftRecord
    {
        public string PersonNo { get; set; }
        public DateTime? TimeBegin { get; set; }
        public DateTime? TimeUntil { get; set; }
		public int Type { get; set; }
        public string Remark { get; set; }
        public DateTime? ModifyDate { get; set; }
        public string ModifyPerson { get; set; }
    }

    public class GrantorRecord
	{
		public string PersonNo { get; set; }
		public string HeadPersonNo { get; set; }
		public string HeadPrefixEN { get; set; }
		public string HeadPrefixTH { get; set; }
		public string HeadNameFirstEN { get; set; }
		public string HeadNameFirstTH { get; set; }
		public string HeadNameLastEN { get; set; }
		public string HeadNameLastTH { get; set; }
		public int Priority { get; set; }
		public string HeadEmail { get; set; }
		public string HeadMobile { get; set; }
		public DateTime? ModifyDate { get; set; }
		public string ModifyPerson { get; set; }

		public EmployeeRecord Employee { get; set; }
	}

    //public class VetoesRecord
    //{
    //    public string PersonNo { get; set; }
    //    public string HeadPersonNo { get; set; }
    //    public string HeadPrefixEN { get; set; }
    //    public string HeadPrefixTH { get; set; }
    //    public string HeadNameFirstEN { get; set; }
    //    public string HeadNameFirstTH { get; set; }
    //    public string HeadNameLastEN { get; set; }
    //    public string HeadNameLastTH { get; set; }
    //    public string HeadEmail { get; set; }
    //    public string HeadMobile { get; set; }
    //    public DateTime? ModifyDate { get; set; }
    //    public string ModifyPerson { get; set; }
    //}

	public class RequestRecord
	{
		public Int64 RequestID { get; set; }
		public int TypeSubID { get; set; }
		public string TypeSubName { get; set; }
		public PersonRecord Person { get; set; }
		public string Reason { get; set; }
		public DateTime? Since { get; set; }
		public DateTime? Until { get; set; }
		public string Contact { get; set; }
		public DateTime? ApplyDate { get; set; }
		public int ApplyByHR { get; set; }

        //public decimal TotalLeaveDays { get { return (TotalLeaveMinutes / 60m) / Const.DEFAULT_WORKHOURS_OF_DAY; } }
        public int TotalLeaveMinutes { get; set; }
        public int TotalApproveMinutes { get; set; }
        //public decimal TotalApproveDays { get { var d = (TotalApproveMinutes / 60m) / Const.DEFAULT_WORKHOURS_OF_DAY; return d; } }
        public string DisplayPeriod
        {
            get
            {
                if (!Since.HasValue || !Until.HasValue)
                {
                    var ds = new TimeSpan(0, TotalLeaveMinutes, 0);
                    var str = ds.TotalHours.ToString();
                    if (ds.Minutes > 0) str = Math.Floor(ds.TotalHours) + ds.ToString("':'mm");
                    return (TotalLeaveMinutes < 0 ? "-" : "") + str + "h";
                }
                return string.Format("{0} - {1}", Since.Value.ToString("HH:mm"), Until.Value.ToString("HH:mm"));
            }
        }
        public string DisplayTotalDays
        {
            get
            {
                return Tool.ConvertMinutesToString(TotalLeaveMinutes);
                //return string.Format("{0} ({1}h)", TotalLeaveDays.ToString("0.#####"), TotalLeaveHours.ToString("0.#####"));
            }
        }
        public string DisplayTotalHours
        {
            get
            {
                var ds = new TimeSpan(0, TotalLeaveMinutes, 0);
                return (TotalLeaveMinutes < 0 ? "-" : "") + Math.Floor(ds.TotalHours) + ds.ToString("':'mm");
                //var s = Tool.ConvertHoursToString(TotalLeaveHours);
                //return s;
            }
        }
        public string DisplayStatusName { get; set; }
        public int StatusID { get; set; }
        public string AttachedFilepath { get; set; }
        public string AttachedVirtualpath { get; set; }
	}

	public class LeaveRecord
	{
		public Int64 LeaveID { get; set; }
		public int StatusID { get; set; }
		public int TypeSubID { get; set; }
		public RequestRecord LeaveRequested { get; set; }
		public DateTime? LeaveDate { get; set; }
		public DateTime? BeginTime { get; set; }
        public DateTime? UntilTime { get; set; }
        public int TotalMinutes { get; set; }
        //public decimal TotalHours { get { var h = (TotalMinutes / 60m); return h; } }
        //public decimal TotalDays { get { var d = TotalHours / Const.DEFAULT_WORKHOURS_OF_DAY; return d; } }
        public int ApproveMinutes { get; set; }
        //public decimal ApproveDays { get { var d = (ApproveMinutes / 60m) / Const.DEFAULT_WORKHOURS_OF_DAY; return d; } }
        public decimal HoursPerDay { get; set; }
		public string Comment { get; set; }
		public List<GrantRecord> Approvals { get; set; }
		public List<VetoRecord> Vetoes { get; set; }

        public string DisplayExactlyDays { get; set; }
        public string DisplayStatusName { get; set; }
        public string DisplayPeriod
        {
            get
            {
                if (!BeginTime.HasValue || !UntilTime.HasValue)
                {
                    var ds = new TimeSpan(0, TotalMinutes, 0);
                    var str = ds.TotalHours.ToString();
                    if (ds.Minutes > 0) str = Math.Floor(ds.TotalHours) + ds.ToString("':'mm");
                    return (TotalMinutes < 0 ? "-" : "") + str + "h";
                }
                return string.Format("{0} - {1}", BeginTime.Value.ToString("HH:mm"), UntilTime.Value.ToString("HH:mm"));
            }
        }
        public string DisplayTotalDays
        {
            get
            {
                return Tool.ConvertMinutesToString(TotalMinutes, HoursPerDay);
                //return string.Format("{0} ({1}h)", TotalDays.ToString("0.#####"), TotalHours.ToString("0.#####"));
            }
        }
        public string DisplayTotalHours
        {
            get
            {
                var ds = new TimeSpan(0, TotalMinutes, 0);
                return (TotalMinutes < 0 ? "-" : "") + Math.Floor(ds.TotalHours) + ds.ToString("':'mm");
                //var s = Tool.ConvertHoursToString(TotalHours);
                //return s;
            }
        }
	}

	public class GrantRecord
	{
		public Int64 LeaveID { get; set; }
		public Int64 RequestID { get; set; }
		public int StatusID { get; set; }
		public int Priority { get; set; }
		public string HeadPersonNo { get; set; }
		public int GrantStepID { get; set; }
		public DateTime? GrantDate { get; set; }
		public string GrantComment { get; set; }
		public int? CancelStatusID { get; set; }
		public int? CancelGrantStepID { get; set; }
		public DateTime? CancelGrantDate { get; set; }
		public string CancelGrantComment { get; set; }
	}

	public class VetoRecord
	{
		public Int64 RequestID { get; set; }
		public string Digest { get; set; }
        public string HeadPersonNo { get; set; }
        public PersonRecord Person { get; set; }
		public string NotifyToEmail { get; set; }
		public int ActionStatus { get; set; }
		public DateTime? ActionDate { get; set; }
		public string Reason { get; set; }
	}

    public class EmailLogRecord
    {
        public string ID { get; set; }
        public DateTime SendTime { get; set; }
        public string To { get; set; }
        public string ToPersonNo { get; set; }
        public string ToName { get; set; }
        public string Cc { get; set; }
        public string Bcc { get; set; }
        public string Subject { get; set; }
        public string Type { get; set; }
        public string SendResult { get; set; }
        public string Error { get; set; }
        public Int64? RequestID { get; set; }
        public string Message { get; set; }
    }

	#endregion

	#region Time Attendance

	public class AttDashBoardRecord
	{
		public int Month { get; set; }
		public string PersonNo { get; set; }
		public decimal LeaveSick { get; set; }
        public decimal LeaveBusiness { get; set; }
        public decimal LeaveVocation { get; set; }
        public decimal LeaveOther { get; set; }

		public int LateCount { get; set; }
		public decimal LateMinutes { get; set; }
		public decimal AbsenceMinutes { get; set; }
	}

	public class LogGetRecord
	{
		public Int64 LogID { get; set; }
		public DateTime? BeginDate { get; set; }
		public DateTime? UntilDate { get; set; }
		public int TotalRecord { get; set; }
		public string StatusProgress { get; set; }
		public DateTime? LogTime { get; set; }
		public string Source { get; set; }
		public PersonRecord Person { get; set; }
	}

	public class LogConsoRecord
	{
		public Int64 LogID { get; set; }
		public DateTime? BeginDate { get; set; }
		public DateTime? UntilDate { get; set; }
		public int TotalRecord { get; set; }
		public int ProgressRecord { get; set; }
		public DateTime? LogTime { get; set; }
		public int Locked { get; set; }
		public PersonRecord Person { get; set; }
	}

	public class LogMailRecord
	{
		public Int64 LogID { get; set; }
		public DateTime? ScheduleDate { get; set; }
		public int TotalRecord { get; set; }
		public DateTime? LogTime { get; set; }
		public int Sent { get; set; }
		public DateTime? SendTime { get; set; }
		public string Error { get; set; }
		public PersonRecord Person { get; set; }
	}

	public class SummaryRecord
	{
		public Int64 UserID { get; set; }
		public DateTime? CheckDate { get; set; }
		public DateTime? CheckIn { get; set; }
		public DateTime? CheckOut { get; set; }
		public DateTime? HoursGross { get; set; }
        public DateTime? MorningIn { get; set; }
        public DateTime? MorningOut { get; set; }
        public DateTime? AfternoonIn { get; set; }
        public DateTime? AfternoonOut { get; set; }
        public DateTime? HoursByShift { get; set; }
	}

	public class WorktimeRecord
	{
		public string PersonNo { get; set; }
		public DateTime? WorkDate { get; set; }
		public string IsWorkingDay { get; set; }
		public decimal WorkMinutes { get; set; }
		public decimal RegularMinutes { get; set; }
		public decimal LeaveSick { get; set; }
        public decimal LeaveBusiness { get; set; }
        public decimal LeaveVocation { get; set; }
        public decimal LeaveOther { get; set; }
        public DateTime? LeaveBegin { get; set; }
        public DateTime? LeaveUntil { get; set; }
        public DateTime? MorningIn { get; set; }
        public DateTime? MorningOut { get; set; }
        public DateTime? AfternoonIn { get; set; }
        public DateTime? AfternoonOut { get; set; }
        public DateTime? FScanIn { get; set; }
        public DateTime? FScanOut { get; set; }
		//public decimal OT1Minutes { get; set; }
		//public decimal OT2Minutes { get; set; }
		//public decimal OT3Minutes { get; set; }
		public decimal LateMinutes { get; set; }
		public decimal AbsenceMinutes { get; set; }
	}

	#endregion

}
