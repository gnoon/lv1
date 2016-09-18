using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Security.Principal;
using System.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Threading;
using System.IO;

namespace LeaveCore
{
	public class Leave : BaseDB
	{
        protected string PersonNo;
		protected string TypeNo;
		protected int TypeSubID;
		protected string TypeSubName;
		protected string TypeCase;
		protected DateTime Since;
		protected DateTime Until;
		protected string Reason;
		protected string Contact;
		protected DateTime ApplyDate;
		protected HttpPostedFileBase AttachFiles;
        protected string AttachFilePath;

		protected Person Person;
		protected RequestRecord Header;
		protected List<LeaveRecord> Lines;
		protected bool AutoGranted;
        protected bool IsLoaded;

		internal static readonly string SaltTextGrant = "@Grant";
        internal static readonly string SaltTextVeto = "@Veto";

		internal static readonly string StatusLeaveZero = "ยกเว้น";

        public Leave(IPrincipal User, string PersonNo)
            : base(User)
        {
			this.PersonNo = PersonNo;
        }

        /// <summary>
        /// รับค่าจากหน้าเว็บเพื่อยื่นใบลา
        /// </summary>
        /// <param name="PersonNo">คนที่จะลา</param>
        /// <param name="TypeSubID">ประเภทการลา</param>
        /// <param name="BeginDate">จากวันที่ เวลา</param>
        /// <param name="UntilDate">ถึงวันที่ เวลา</param>
        /// <param name="Reason">เหตุผลการลา</param>
        /// <param name="Contact">เบอร์ติดต่อ</param>
        /// <param name="AttachFiles">ไฟล์แนบ กรณีที่มี POST เข้าระบบ</param>
        /// <param name="AttachFilePath">Path ที่เก็บไฟล์แนบเอาไว้บน Server</param>
        /// <returns>Leave class</returns>
		public Leave SetLeaveParams(
            string PersonNo,
            int TypeSubID,
			string TypeCase,
			DateTime Since,
			DateTime Until,
            string Reason,
            string Contact,
			HttpPostedFileBase AttachFiles,
            string AttachFilePath)
		{
			this.TypeSubID = TypeSubID;
			this.TypeCase = TypeCase;
			this.Since = Since;
			this.Until = Until;
			this.Reason = Reason;
			this.Contact = Contact;
			this.ApplyDate = DateTime.Now;
			this.AttachFiles = AttachFiles;
            this.AttachFilePath = AttachFilePath;
			//if (AttachFiles.ContentLength > 0) this.AttachFiles = AttachFiles;

			// ตัวกำหนด role อยู่ที่ PublicController
			// true ถ้าต้องการให้สถานะใบลาเป็นอนุมัติแล้ว
			this.LoadProfiles();
			this.AutoGranted = User.IsInRole(Const.ROLE_IMPERSONATE);

			int fIndex = this.Person.Quota.OfLeaveType.FindIndex(q => q.LeaveType.TypeSubID == TypeSubID);
			if (fIndex > -1)
			{
			    this.TypeNo = this.Person.Quota.OfLeaveType[fIndex].LeaveType.TypeNo;
			    this.TypeSubName = this.Person.Quota.OfLeaveType[fIndex].LeaveType.NameTH;
			}

			if (TypeSubID == 0)
				throw new LeaveParameterException(this.PersonNo, "TypeSubID", "TypeSubID");
			if(this.Person.Grantors == null || !this.Person.Grantors.Any(f => new[] { 1, 2 }.Contains(f.Priority)))
				throw new LeaveParameterException(this.PersonNo, "AUTHORITY_MISSING", null);

            return this;
		}

		public void SerializeLeave()
		{
			this.Header = this.SerializeLeaveHeader();
			this.Lines = this.SerializeLeaveLines();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns>record ของตาราง Request</returns>
		public RequestRecord SerializeLeaveHeader()
		{
			RequestRecord rec = new RequestRecord();
			rec.TypeSubID = this.TypeSubID;
			rec.TypeSubName = this.TypeSubName;
			rec.Person = new PersonRecord();
			rec.Person.PersonNo = this.PersonNo;
			rec.Since = this.Since;
			rec.Until = this.Until;
			rec.Reason = this.Reason;
			rec.Contact = this.Contact;
			rec.ApplyDate = this.ApplyDate;
			rec.ApplyByHR = this.AutoGranted ? 1 : 0;
			return rec;
		}

        /// <summary>
        /// คำนวนวันที่ Since และ Until ที่ User ระบุทางเว็บ เพื่อให้ได้ช่วงวันที่ที่อยู่ในกะทำงานเท่านั้น
        /// </summary>
        /// <param name="WorkshiftMorning"></param>
        /// <param name="WorkshiftAfternoon"></param>
        /// <param name="UserSince"></param>
        /// <param name="UserUntil"></param>
        /// <param name="RoundedSince"></param>
        /// <param name="RoundedUntil"></param>
        /// <returns></returns>
        protected bool RoundsLeaveDateRange(WorkshiftRecord WorkshiftMorning, WorkshiftRecord WorkshiftAfternoon,
            DateTime UserSince, DateTime UserUntil,
            out DateTime RoundedSince, out DateTime RoundedUntil)
        {
            RoundedSince = UserSince;
            RoundedUntil = UserUntil;

            bool HasChanged = false;
            if (this.Since.TimeOfDay < WorkshiftMorning.TimeBegin.Value.TimeOfDay)
            {
                HasChanged = true;
                // ถ้าระบุ"เวลาเริ่ม"จากหน้าเว็บน้อยกว่ากะเข้างาน ให้ใช้เวลากะเข้างาน
                RoundedSince = new DateTime(UserSince.Year, UserSince.Month, UserSince.Day,
                    WorkshiftMorning.TimeBegin.Value.Hour, WorkshiftMorning.TimeBegin.Value.Minute, 0);
            }
            else if (UserSince.TimeOfDay >= WorkshiftMorning.TimeUntil.Value.TimeOfDay
                && UserSince.TimeOfDay <= WorkshiftAfternoon.TimeBegin.Value.TimeOfDay)
            {
                HasChanged = true;
                // ถ้าระบุ"เวลาเริ่ม"อยู่ในช่วงพักเที่ยง ให้ใช้เวลากะเข้างานบ่าย
                RoundedSince = new DateTime(UserSince.Year, UserSince.Month, UserSince.Day,
                    WorkshiftAfternoon.TimeBegin.Value.Hour, WorkshiftAfternoon.TimeBegin.Value.Minute, 0);
            }
            else if (UserSince.TimeOfDay > WorkshiftAfternoon.TimeUntil.Value.TimeOfDay)
            {
                HasChanged = true;
                // ถ้าระบุ"เวลาเริ่ม"จากหน้าเว็บเกินกะออกงาน ให้ใช้เวลากะออกงาน
                RoundedSince = new DateTime(UserSince.Year, UserSince.Month, UserSince.Day,
                    WorkshiftAfternoon.TimeUntil.Value.Hour, WorkshiftAfternoon.TimeUntil.Value.Minute, 0);
            }

            if (UserUntil.TimeOfDay < WorkshiftMorning.TimeBegin.Value.TimeOfDay)
            {
                HasChanged = true;
                // ถ้าระบุ"เวลาถึง"จากหน้าเว็บน้อยกว่ากะเข้างาน ให้ใช้เวลากะเข้างาน
                RoundedUntil = new DateTime(UserUntil.Year, UserUntil.Month, UserUntil.Day,
                    WorkshiftMorning.TimeBegin.Value.Hour, WorkshiftMorning.TimeBegin.Value.Minute, 0);
            }
            else if (UserUntil.TimeOfDay >= WorkshiftMorning.TimeUntil.Value.TimeOfDay
                && UserUntil.TimeOfDay <= WorkshiftAfternoon.TimeBegin.Value.TimeOfDay)
            {
                HasChanged = true;
                // ถ้าระบุ"เวลาถึง"อยู่ในช่วงพักเที่ยง ให้ยึดตาม "เวลาเริ่ม" ถ้าเวลาเริ่มเช้า...ก็เช้า ถ้าเวลาเริ่มบ่าย...ก็บ่าย
                if (RoundedSince.TimeOfDay <= WorkshiftMorning.TimeUntil.Value.TimeOfDay || UserSince.Date != UserUntil.Date)
                {
                    RoundedUntil = new DateTime(UserUntil.Year, UserUntil.Month, UserUntil.Day,
                        WorkshiftMorning.TimeUntil.Value.Hour, WorkshiftMorning.TimeUntil.Value.Minute, 0);
                }
                else
                {
                    RoundedUntil = new DateTime(UserUntil.Year, UserUntil.Month, UserUntil.Day,
                        WorkshiftAfternoon.TimeBegin.Value.Hour, WorkshiftAfternoon.TimeBegin.Value.Minute, 0);
                }
            }
            else if (UserUntil.TimeOfDay > WorkshiftAfternoon.TimeUntil.Value.TimeOfDay)
            {
                HasChanged = true;
                // ถ้าระบุ"เวลาถึง"จากหน้าเว็บเกินกะออกงาน ให้ใช้เวลากะออกงาน
                RoundedUntil = new DateTime(UserUntil.Year, UserUntil.Month, UserUntil.Day,
                    WorkshiftAfternoon.TimeUntil.Value.Hour, WorkshiftAfternoon.TimeUntil.Value.Minute, 0);
            }
            return HasChanged;
        }

		/// <summary>
		/// 
		/// </summary>
		/// <returns>record ของตาราง Leave (แยกรายการตามวัน)</returns>
		public List<LeaveRecord> SerializeLeaveLines()
		{
            string Error;
            return SerializeLeaveLines(out Error);
        }
        /// <summary>
		/// 
		/// </summary>
		/// <returns>record ของตาราง Leave (แยกรายการตามวัน)</returns>
		public List<LeaveRecord> SerializeLeaveLines(out string ErrorMessage)
		{
            ErrorMessage = null;

			LeaveRecord obj;
			List<LeaveRecord> list = new List<LeaveRecord>();
			List<DateTime> range = new List<DateTime>(Tool.GetDateRange(this.Since, this.Until));
			WorkshiftRecord[] shifts = this.Person.Workshifts.GetMinMax();

            bool HasWorkShift = shifts.Length == 2;
            DateTime RoundedSince, RoundedUntil;

            RoundedSince = this.Since;
            RoundedUntil = this.Until;
            if (HasWorkShift)
            {
                // ช่วงการลาต้องอยู่ในเวลาทำงานเท่านั้น
                if (!RoundsLeaveDateRange(shifts[0], shifts[1], this.Since, this.Until, out RoundedSince, out RoundedUntil))
                {
                    RoundedSince = this.Since;
                    RoundedUntil = this.Until;
                }
            }

            DateTime DaySince, DayUntil;
            bool IsSinceDay, IsUntilDay;
			foreach (var rec in range)
			{
				obj = new LeaveRecord();
				obj.StatusID = this.AutoGranted ? Const.STATUS_LEAVE_APPROVED : Const.STATUS_LEAVE_PENDING_APPROVAL;
				obj.TypeSubID = this.TypeSubID;
				obj.Comment = this.Reason;
				obj.LeaveDate = new DateTime(rec.Year, rec.Month, rec.Day, 0, 0, 0);

                IsSinceDay = IsUntilDay = false;

				// BeginTime, UntilTime
                if (rec.Date == RoundedSince.Date)
				{
                    IsSinceDay = true;
					obj.BeginTime = new DateTime(
						rec.Year, rec.Month, rec.Day,
                        RoundedSince.Hour, RoundedSince.Minute, 0);
				}
				else
				{
					obj.BeginTime = new DateTime(
						rec.Year, rec.Month, rec.Day,
						shifts[0].TimeBegin.Value.Hour, shifts[0].TimeBegin.Value.Minute, 0);
				}
                if (rec.Date == RoundedUntil.Date)
				{
                    IsUntilDay = true;
					obj.UntilTime = new DateTime(
						rec.Year, rec.Month, rec.Day,
                        RoundedUntil.Hour, RoundedUntil.Minute, 0);
				}
				else
				{
					obj.UntilTime = new DateTime(
						rec.Year, rec.Month, rec.Day,
						shifts[1].TimeUntil.Value.Hour, shifts[1].TimeUntil.Value.Minute, shifts[1].TimeUntil.Value.Second
					);
				}
				// TotalHours, TotalDays, ApproveDays
				if (this.Person.Holidays.IsHoliday(obj.LeaveDate.Value) || this.Person.Weekends.IsWeekend(obj.LeaveDate.Value))
				{
					obj.TotalHours = 0;
					obj.DisplayExactlyDays = DisplayCaseExactlyToStopDay(obj.LeaveDate.Value);
				}
                // กรณีเป็นวันหยุดให้คำนวนชั่วโมงไปก่อน ไม่ต้องเซ็ตเป็น 0 จะดีกว่าเซ็ตเป็น 0
                // เพราะจะสามารถดำเนินการแก้ไขได้โดยหัวหน้าหรือพนักงานได้เลย เช่น ให้พนักงานยกเลิก หรือ ให้หัวหน้าอนุมัติ/ไม่อนุมัติไปเลย
                // โดยที่ไม่ต้องมีการโทรมาหา HR หรือ BD ให้แก้ไขชั่วโมงให้ถูกต้อง
                // เคสนี้จะเจอบ่อยกับ Cashier ที่ถึงแม้เป็นวันหยุด แต่ไม่หยุด และมาทำงานโดยเบิกเป็น OT
                // อย่างไรก็ตาม DisplayExactlyDays ก็ยังคงแสดงที่หน้าเว็บเหมือนเดิม
				// else
				{
                    // ถ้าการคำนวนตรงกับวันเริ่มลาหรือวันลาวันสุดท้าย ให้ใช้เวลาที่ระบุจากหน้าเว็บในการคำนวน (ที่ Rounded แล้ว)
                    DaySince = IsSinceDay ? RoundedSince : obj.BeginTime.Value;
                    DayUntil = IsUntilDay ? RoundedUntil : obj.UntilTime.Value;

                    obj.TotalHours = Convert.ToDecimal(Leave.CalcTotalLeaveMinutes(
                        shifts[0].TimeBegin,
                        shifts[0].TimeUntil,
                        shifts[1].TimeBegin,
                        shifts[1].TimeUntil,
                        ref DaySince, ref DayUntil, ref ErrorMessage) / 60d
                    );

                    if (ErrorMessage == null)
                    {
                        obj.BeginTime = DaySince;
                        obj.UntilTime = DayUntil;
                    }
                    else
                    {
                        obj.BeginTime = this.Since;
                        obj.UntilTime = this.Until;
                    }
				}
				obj.ApproveMinutes = 0;

				obj.LeaveRequested = this.Header == null ? this.SerializeLeaveHeader() : this.Header;
				obj.LeaveRequested.Person = new PersonRecord();
				obj.LeaveRequested.Person.PersonNo = this.PersonNo;

				list.Add(obj);
			}
			return list;
		}

		/// <summary>
		/// Load profile ของพนักงาน
		/// - โควต้าวันลา (Quota)
		/// - ผู้อนุมัติลา (Grantor)
		/// - วันหยุด (Holiday)
		/// - กะทำงาน (Workshift)
		/// - วันปัก/วันหยุดประจำ/เลื่อนวันหยุด (Weekend/Movement)
		/// - ผู้มีสิทธิ์ระงับการลา (Veto)
		/// </summary>
		public void LoadProfiles()
		{
			this.Person = new Person(User, this.PersonNo, false);
		}

		/// <summary>
		/// ตรวจสอบว่ามีการยื่นใบลาซ้ำหรือไม่
		/// </summary>
		protected void CheckDuplication()
		{
			bool hasLeave = false;
			List<LeaveRecord> DuplicationList = new List<LeaveRecord>();

			List<string> filters = new List<string>();
			string ACTIVE_STATUSES = string.Join(",", Array.ConvertAll<int, string>(Const.LEAVE_STATUS_ACTIVE(), Convert.ToString));

			#region Query
			// TODO: loop เพื่อ check หากมีซ้ำให้เก็บลง List แล้ว break เพื่อ throw error
			foreach (var rec in this.Lines)
			{
				string leave_date = rec.LeaveDate.Value.ToString("yyyyMMdd", Thread.CurrentThread.CurrentCulture);
				string begin_time = rec.BeginTime.Value.ToString("HH:mm:ss", Thread.CurrentThread.CurrentCulture);
				string until_time = rec.UntilTime.Value.ToString("HH:mm:ss", Thread.CurrentThread.CurrentCulture);
				filters.Add(""
					// collapse dates
					+ "CONVERT(VARCHAR, a.[Leave Date], 112) = CONVERT(VARCHAR, '"+leave_date+"', 112) "
					// and collapse time
					+ "AND ( "
					+ "   (CONVERT(VARCHAR, a.[Begin Time], 108) < CONVERT(VARCHAR, '"+until_time+"', 108) AND "
					+ "	   CONVERT(VARCHAR, a.[Begin Time], 108) BETWEEN CONVERT(VARCHAR, '"+begin_time+"', 108) "
					+ "		   AND CONVERT(VARCHAR, '"+until_time+"', 108) "
					+ "	   ) "
					+ "	   OR ( "
					+ "		   CONVERT(VARCHAR, a.[Until Time], 108) > CONVERT(VARCHAR, '"+begin_time+"', 108) AND "
					+ "		   CONVERT(VARCHAR, a.[Until Time], 108) BETWEEN CONVERT(VARCHAR, '"+begin_time+"', 108) "
					+ "			   AND CONVERT(VARCHAR, '"+until_time+"', 108) "
					+ "		  ) "
					+ "    OR ( "
					+ "		   CONVERT(VARCHAR, '"+begin_time+"', 108) >= CONVERT(VARCHAR, a.[Begin Time], 108) AND "
					+ "		   CONVERT(VARCHAR, a.[Until Time], 108) >= CONVERT(VARCHAR, '"+until_time+"', 108) "
					+ "		  ) "
					+ "	   ) "
				 );
			}

			#region Old Query
			//string Query = "SELECT	{0},b.[Request ID], b.[Type Sub ID], c.[TH Name] AS [Type Sub Name], " // remove c.[Type No]
			//            + "			b.[Reason], a.[Person No], b.[Contact], "
			//            + "			b.[Since], "
			//            + "			b.[Until], "
			//            + "			b.[Apply Date], "
			//            + "			b.[Apply By HR], "
			//            + "			SUM(a.[Total Days]) AS [Total Days], "
			//            + "			SUM(a.[Total Hours]) AS [Total Hours], "
			//            + "			MAX(a.[Status ID]) AS [Status ID], "
			//            + "			COUNT(DISTINCT a.[Status ID]) AS [Status Count] "
			//            + "FROM		[LV Leave] a INNER JOIN "
			//            + "			[LV Request] b ON a.[Request ID] = b.[Request ID] INNER JOIN "
			//            + "			[LV Type Sub] c ON b.[Type Sub ID] = c.[Type Sub ID] "
			//            + "WHERE	a.[Person No] = @perno AND "
			//            + "			( (" + string.Join(") OR (", filters) + ") ) AND "
			//            //+ "			DATEPART(w,a.[Leave Date]) NOT IN ('.implode(',', $weekends).') " // but not a weekend
			//            //+ "			(empty($holidays) ? '' AND "
			//            //+ "			AND a.leave_date NOT IN ('.implode(',', $holidays).') ') AND "    // and not a holiday
			//            + "			a.[Status ID] IN (" + ACTIVE_STATUSES + ") "
			//            + "GROUP BY b.[Request ID], b.[Type Sub ID], c.[TH Name], a.[Person No], " // remove c.[Type No]
			//            + "			b.[Reason], b.[Contact], "
			//            + "			b.[Since], "
			//            + "			b.[Until], "
			//            + "			b.[Apply Date], "
			//            + "			b.[Apply By HR]";
			#endregion
            string Query = "SELECT	{0},b.[Request ID], b.[Type Sub ID], c.[TH Name] AS [Type Sub Name], "
                        + "			b.[Reason], b.[Contact], b.[Since], b.[Until], b.[Apply Date], b.[Apply By HR], "
                        + "			a.[Leave ID], a.[Status ID], a.[Person No], a.[Leave Date], a.[Begin Time], "
                        + "			a.[Until Time], a.[Total Hours], a.[Total Days], a.[Approve Minutes], a.[Comment] "
                        + "FROM		[LV Leave] a INNER JOIN "
                        + "			[LV Request] b ON a.[Request ID] = b.[Request ID] INNER JOIN "
                        + "			[LV Type Sub] c ON b.[Type Sub ID] = c.[Type Sub ID] "
                        + "WHERE	a.[Person No] = @perno AND "
                        + "			( (" + string.Join(") OR (", filters) + ") ) AND "
                //+ "			DATEPART(w,a.[Leave Date]) NOT IN ('.implode(',', $weekends).') " // but not a weekend
                //+ "			(empty($holidays) ? '' AND "
                //+ "			AND a.leave_date NOT IN ('.implode(',', $holidays).') ') AND "    // and not a holiday
                        + "			a.[Status ID] IN (" + ACTIVE_STATUSES + ") ";
			#endregion
			List<SqlParameter> Params = new List<SqlParameter>();
            Params.Add(new SqlParameter("@perno", SqlDbType.VarChar));
            Params[0].Value = this.PersonNo == null ? DBNull.Value : (object)this.PersonNo;

            DuplicationList = this.QueryLines(Query, "a.[Leave Date]", Params, 1, 0, false, false);
            hasLeave = DuplicationList.Count > 0;

            if (hasLeave)
                throw new LeaveRequestDuplicationException(PersonNo, DuplicationList);
		}

        /// <summary>
        /// ตรวจสอบสิทธิพนักงาน
        /// </summary>
        /// <returns>false หากไม่ผ่าน</returns>
        protected void CheckPolicy()
        {
            // TODO:
            // 0.9 ลา 0 ชั่วโมง (LeaveRequestException)
            // 1. สิทธิ์การลา {นับ 1 ปีสำหรับพักผ่อน} (LeaveRequestEffectiveVacationException)
			// 2. วันลาคงเหลือ (LeaveRequestQuotaExceedException)
            // 3. การยื่นล่วงหน้า (LeaveRequestPreDateException)
            // 4. ป่วยห้ามลาล่วงหน้า (LeaveRequestPreDateException)
			// 5. ป่วยติดกัน 3 วันต้องมีใบรับรองแพทย์ (LeaveRequestMedicalCertificateException)

			#region Process this

            // 0.9
            foreach (var rec in this.Lines)
            {
				bool SkipCalHours = this.Person.Holidays.IsHoliday(rec.LeaveDate.Value) || 
									this.Person.Weekends.IsWeekend(rec.LeaveDate.Value);
				//if (rec.TotalHours == 0)
				if (rec.TotalHours == 0 && !SkipCalHours)
                {
                    throw new LeaveRequestException(this.PersonNo, 0, new LeaveException("Leave 0 hour", null));
                }
            }

			// 1.
			if (this.TypeNo == Const.TYPE_NO_VACATION)
			{
                var exc = new LeaveRequestEffectiveVacationException(PersonNo, (DateTime)this.Person.Record.Employee.StartingDate, Const.QUOTA_MINYEARS4VACATION);
                if (!this.Person.Record.Employee.StartingDate.HasValue || this.ApplyDate < exc.EffectiveDate)
					throw exc;
			}

			// 2.
			Dictionary<int, decimal> DaysOfLeave = new Dictionary<int, decimal>();
			foreach (var rec in this.Lines)
			{
				if (rec.TypeSubID == this.TypeSubID)
				{
					int Year = rec.LeaveDate.Value.Year;
					if (DaysOfLeave.ContainsKey(Year))
						DaysOfLeave[Year] += rec.TotalDays;
					else
						DaysOfLeave.Add(Year, rec.TotalDays);
				}
			}
            // ถ้าเป็นลากิจแบบพิเศษให้ลาได้ไม่ต้องเช็คโควต้า
            bool SpecialBusinessLeave = (!string.IsNullOrEmpty(this.TypeCase)) && (this.TypeNo == Const.TYPE_NO_BUSINESS);
            if (DaysOfLeave.Count > 0 && !SpecialBusinessLeave)
			{
				//(var rec in DaysOfLeave.Keys.ToList())
				foreach (var rec in DaysOfLeave)
				{
					if (this.Person.Quota.IsQuotaExceeded(this.TypeNo, rec.Key, rec.Value))
						throw new LeaveRequestQuotaExceedException(
							this.PersonNo,
							this.TypeSubID,
							rec.Key,
							Convert.ToInt32(this.Person.Quota.GetValue(this.TypeNo, "BalanceAmount")),
							Convert.ToInt32(rec.Value));
				}
			}

			// 3.
			//if(!User.InPolicyExceptions)
			int Daysdiff = 0;
			int Multiplier = 1;
			DateTime Days1 = this.ApplyDate;
			DateTime Days2 = this.Header.Since.Value;
			if (this.ApplyDate.Date > this.Header.Since.Value.Date)
			{
				Multiplier = -1;
				Days2 = this.ApplyDate;
				Days1 = this.Header.Since.Value;
			}
			for (var Days = Days1.Date; Days < Days2.Date; Days = Days.AddDays(1))
			{
				Daysdiff++;
			}
			Daysdiff = Daysdiff * Multiplier;
			bool SkipPreDate = !string.IsNullOrEmpty(this.TypeCase);
			if(this.TypeNo == Const.TYPE_NO_BUSINESS && Daysdiff < Const.REQUEST_BUSINESS_PREDATE && !SkipPreDate)
				if(!this.AutoGranted)
					throw new LeaveRequestPreDateException(
						this.PersonNo, this.TypeSubID, this.ApplyDate,
						this.Header.Since.Value, Const.REQUEST_BUSINESS_PREDATE
					);
			if(this.TypeNo == Const.TYPE_NO_VACATION && Daysdiff < Const.REQUEST_VACATION_PREDATE)
				if(!this.AutoGranted)
					throw new LeaveRequestPreDateException(
						this.PersonNo, this.TypeSubID, this.ApplyDate,
						this.Header.Since.Value, Const.REQUEST_VACATION_PREDATE
					);
			if(this.TypeNo == Const.TYPE_NO_MATERNITY && Daysdiff < Const.REQUEST_MATERNITY_PREDATE)
				if(!this.AutoGranted)
					throw new LeaveRequestPreDateException(
						this.PersonNo, this.TypeSubID, this.ApplyDate,
						this.Header.Since.Value, Const.REQUEST_MATERNITY_PREDATE
					);
			if(this.TypeNo == Const.TYPE_NO_INITIATION && Daysdiff < Const.REQUEST_INITIATION_PREDATE)
				if(!this.AutoGranted)
					throw new LeaveRequestPreDateException(
						this.PersonNo, this.TypeSubID, this.ApplyDate,
						this.Header.Since.Value, Const.REQUEST_INITIATION_PREDATE
					);
			if(this.TypeNo == Const.TYPE_NO_CELEBRATEHULLVALLEY && Daysdiff < Const.REQUEST_CELEBRATEHULLVALLEY_PREDATE)
				if(!this.AutoGranted)
					throw new LeaveRequestPreDateException(
						this.PersonNo, this.TypeSubID, this.ApplyDate,
						this.Header.Since.Value, Const.REQUEST_CELEBRATEHULLVALLEY_PREDATE
					);
			if(this.TypeNo == Const.TYPE_NO_EDUCATION && Daysdiff < Const.REQUEST_EDUCATION_PREDATE)
				if(!this.AutoGranted)
					throw new LeaveRequestPreDateException(
						this.PersonNo, this.TypeSubID, this.ApplyDate,
						this.Header.Since.Value, Const.REQUEST_EDUCATION_PREDATE
					);
			if(this.TypeNo == Const.TYPE_NO_STERILIZATION && Daysdiff < Const.REQUEST_STERILIZATION_PREDATE)
				if(!this.AutoGranted)
					throw new LeaveRequestPreDateException(
						this.PersonNo, this.TypeSubID, this.ApplyDate,
						this.Header.Since.Value, Const.REQUEST_STERILIZATION_PREDATE
					);
			if(this.TypeNo == Const.TYPE_NO_WEDDING && Daysdiff < Const.REQUEST_WEDDING_PREDATE)
				if(!this.AutoGranted)
					throw new LeaveRequestPreDateException(
						this.PersonNo, this.TypeSubID, this.ApplyDate,
						this.Header.Since.Value, Const.REQUEST_WEDDING_PREDATE
					);

			// 4.
			if (this.TypeNo == Const.TYPE_NO_SICK || this.TypeNo == Const.TYPE_NO_SICKATWORK)
			{
				if (this.Header.Since.Value.Date > this.ApplyDate.Date)
					throw new LeaveRequestPreDateException(this.PersonNo, this.TypeSubID, this.ApplyDate, this.Header.Since.Value, -1); 
			}

			// 5.
			if (this.TypeNo == Const.TYPE_NO_SICK || this.TypeNo == Const.TYPE_NO_SICKATWORK)
			{
				if (!this.AutoGranted && this.Lines.Sum(d => d.TotalHours) >= Const.REQUEST_SICK_CONTINUALLY_HOURS)
				{
					#region Continually
					//bool Continually = true;
					//for (var Days = this.Header.Since.Value.Date; Days <= this.Header.Until.Value; Days = Days.AddDays(1))
					//{
					//    if (!this.Person.Holidays.IsHoliday(Days) && !this.Person.Weekends.IsWeekend(Days))
					//    {
					//        bool IsLeaveDate = false;
					//        foreach (var rec in this.Lines)
					//        {
					//            if (rec.LeaveDate == Days) 
					//            {
					//                IsLeaveDate = true;
					//                break;
					//            }
					//        }
					//        if (!IsLeaveDate)
					//        {
					//            Continually = false;
					//            break;
					//        }
					//    }
					//}
					#endregion
					//if (Continually & this.AttachFiles.ContentLength <= 0)
					if (this.AttachFiles == null)
						throw new LeaveRequestMedicalCertificateException(
							this.PersonNo,
							this.Header.Since.Value,
							this.Header.Until.Value,
							Convert.ToInt32(Const.REQUEST_SICK_CONTINUALLY_HOURS / Const.DEFAULT_WORKHOURS_OF_DAY)
						);
				}
			}
			#endregion
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns>Running number ของตาราง Request</returns>
		public Int64 NewLeaveRequest()
		{
			Int64 RequestID = 0;
			try
			{
				this.SerializeLeave();
				this.CheckDuplication();
				this.CheckPolicy();

				RequestID = this.CreateLeaveHeader();
				Int64[] LeaveIDs = this.CreateLeaveLines(RequestID);

				this.CreateLeaveAttachments(RequestID, LeaveIDs);
				this.CreateLeaveGrants(RequestID, LeaveIDs);
				this.CreateLeaveVetos(RequestID, LeaveIDs);

				return RequestID;
			}
			catch (Exception e)
			{
                this.RollbackLeaveRequest(RequestID);
                RequestID = 0;
				throw e;
			}
		}

		public string DisplayCaseExactlyToStopDay(DateTime Date)
        {
            //Holidays Holidays = new Holidays(User, PersonNo);
            //Weekends Weekends = new Weekends(User, PersonNo);
            //DateTime beginningOfMonth = new DateTime(Date.Year, Date.Month, 1);
            //int WeekNumber = (Date.Subtract(beginningOfMonth).Days / 7) + 1;
            //if (Holidays.IsHoliday(Date))
            //    return "Exactly to stop day., Is " + Date.DayOfWeek + ".";
            //if (Weekends.IsWeekend(Date))
            //    return "Exactly to holiday., Is " + Holidays.GetValue(Date, "NameEN") + ".";
            var holiday = this.Person.Holidays;
            if (holiday.IsHoliday(Date))
            {
                return string.Format("วันหยุดประจำปี: {0}", holiday.GetValue(Date, "NameTH"));
            }
            var weekend = this.Person.Weekends;
            if (weekend.IsWeekend(Date))
            {
                return string.Format("วัน{0}", Date.ToString("dddd", new CultureInfo("th-TH")));
            }
            return null;
        }

		#region Execute Section

        /// <summary>
        /// สร้างรายการในตาราง Request
        /// </summary>
        protected Int64 CreateLeaveHeader()
        {
			Int64 RequestID = Unique.GetLastID("LV Request", "Request ID");
			using (SqlConnection conn = GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText =
						  "INSERT INTO [LV Request] ( "
						+ "		[Request ID], [Type Sub ID], [Type Sub Name], "
						+ "		[Person No], [Reason], [Since], [Until], [Contact], [Apply Date], [Apply By HR] "
						+ ") VALUES ( "
						+ "		@RequestID, @TypeSubID, @TypeSubName, "
						+ "		@PersonNo, @Reason, @Since, @Until, @Contact, @ApplyDate, @ApplyByHR "
						+ ")";
					cmd.Parameters.Add("@RequestID", SqlDbType.BigInt).Value = RequestID;
					cmd.Parameters.Add("@TypeSubID", SqlDbType.Int).Value = this.Header.TypeSubID;
					cmd.Parameters.Add("@TypeSubName", SqlDbType.VarChar).Value = this.Header.TypeSubName;
					cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = this.Header.Person.PersonNo;
					cmd.Parameters.Add("@Reason", SqlDbType.VarChar).Value = this.Header.Reason ?? string.Empty;
					cmd.Parameters.Add("@Contact", SqlDbType.VarChar).Value = this.Header.Contact ?? string.Empty;
					cmd.Parameters.Add("@Since", SqlDbType.DateTime).Value = this.Header.Since;
					cmd.Parameters.Add("@Until", SqlDbType.DateTime).Value = this.Header.Until;
					cmd.Parameters.Add("@ApplyDate", SqlDbType.DateTime).Value = this.Header.ApplyDate;
					cmd.Parameters.Add("@ApplyByHR", SqlDbType.Int).Value = this.Header.ApplyByHR;
					cmd.ExecuteNonQuery();
				}
			}
			return RequestID;
        }

        /// <summary>
        /// สร้างรายการในตาราง Leave
        /// </summary>
        /// <param name="AutoGranted">true ถ้าต้องการให้สถานะใบลาเป็นอนุมัติแล้ว</param>
        /// <returns>จำนวน record ที่สร้าง</returns>
        protected Int64[] CreateLeaveLines(Int64 RequestID)
        {
            List<Int64> IDs = new List<Int64>();
			using (SqlConnection conn = GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					foreach (var rec in this.Lines)
					{
						Int64 LeaveID = Unique.GetLastID("LV Leave", "Leave ID");
						IDs.Add(LeaveID);
						cmd.CommandText =
							  "INSERT INTO [LV Leave] ( "
                            + "		[Leave ID], [Request ID], [Status ID], [Type Sub ID], [Hours Per Day], "
							+ "		[Person No], [Leave Date], [Begin Time], [Until Time], "
							+ "		[Total Hours], [Total Days], [Approve Minutes], [Comment], [Modify Date], [Modify Person] "
							+ ") VALUES ( "
							+ "		@LeaveID, @RequestID, @StatusID, @TypeSubID, @HoursADay, "
							+ "		@PersonNo, @LeaveDate, @BeginTime, @UntilTime, "
                            + "		@TotalHours, @TotalDays, @ApproveMins, @Comment, @ModifyDate, @ModifyPerson "
							+ ")";
						cmd.Parameters.Clear();
						cmd.Parameters.Add("@LeaveID", SqlDbType.BigInt).Value = LeaveID;
						cmd.Parameters.Add("@RequestID", SqlDbType.BigInt).Value = RequestID;
						cmd.Parameters.Add("@StatusID", SqlDbType.Int).Value = rec.StatusID;
						cmd.Parameters.Add("@TypeSubID", SqlDbType.Int).Value = rec.TypeSubID;
						cmd.Parameters.Add("@PersonNo", SqlDbType.VarChar).Value = rec.LeaveRequested.Person.PersonNo;
						cmd.Parameters.Add("@LeaveDate", SqlDbType.DateTime).Value = rec.LeaveDate;
						cmd.Parameters.Add("@BeginTime", SqlDbType.DateTime).Value = rec.BeginTime;
						cmd.Parameters.Add("@UntilTime", SqlDbType.DateTime).Value = rec.UntilTime;
						cmd.Parameters.Add("@TotalHours", SqlDbType.Decimal).Value = rec.TotalHours;
						cmd.Parameters.Add("@TotalDays", SqlDbType.Decimal).Value = rec.TotalDays;
                        cmd.Parameters.Add("@ApproveMins", SqlDbType.Int).Value = rec.ApproveMinutes;
                        cmd.Parameters.Add("@HoursADay", SqlDbType.Decimal).Value = Const.DEFAULT_WORKHOURS_OF_DAY;
						cmd.Parameters.Add("@Comment", SqlDbType.VarChar).Value = rec.Comment;
						cmd.Parameters.Add("@ModifyDate", SqlDbType.DateTime).Value = DateTime.Now;
						cmd.Parameters.Add("@ModifyPerson", SqlDbType.VarChar).Value = User.Identity.Name;
						cmd.ExecuteNonQuery();
					}
				}
			}
			return IDs.ToArray();
        }

        /// <summary>
        /// สร้างรายการในตาราง File (หากมีการเซ็ตตัวแปล AttachFilepath)
        /// </summary>
        protected void CreateLeaveAttachments(Int64 RequestID, Int64[] LeaveIDs)
        {
            HttpPostedFileBase upfile = this.AttachFiles;
            if (upfile != null && upfile.ContentLength > 0)
            {
                string exts = Path.GetExtension(upfile.FileName);
                string name = string.Format("{0}_{1}{2}", this.Person.Record.NameFirstTH, RequestID, exts);
                foreach (char c in Path.GetInvalidFileNameChars())
                    name = name.Replace(c.ToString(), "");
                string fullpath = Path.Combine(this.AttachFilePath, name);
                upfile.SaveAs(fullpath);

                using (SqlConnection conn = GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText =
                              "INSERT INTO [LV File] ([Request ID], [Content Type], [Real Path], [Virtual Path]) "
                            + "VALUES (@RequestID, @CType, @FullPath, @VirtualPath)";
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add("@RequestID", SqlDbType.BigInt).Value = RequestID;
                        cmd.Parameters.Add("@CType", SqlDbType.VarChar, 50).Value = upfile.ContentType;
                        cmd.Parameters.Add("@FullPath", SqlDbType.VarChar, 255).Value = fullpath;
                        cmd.Parameters.Add("@VirtualPath", SqlDbType.VarChar, 255).Value = "~/Leave/FileOf/" + RequestID;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// สร้างรายการในตาราง Grant (based on ตาราง Profile Grantor)
        /// </summary>
		protected void CreateLeaveGrants(Int64 RequestID, Int64[] LeaveIDs)
        {
			using (SqlConnection conn = GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					foreach (var LeaveID in LeaveIDs)
					{
						foreach (var rec in this.Person.Grantors)
						{
							cmd.CommandText =
								  "INSERT INTO [LV Grant] ( "
								+ "		[Request ID], [Leave ID], [Priority], "
								+ "		[Head Person No], [Grant Step ID], [Status ID], [Grant Date] "
								+ ") VALUES ( "
								+ "		@RequestID, @LeaveID, @Priority, "
								+ "		@HeadPersonNo, @GrantStepID, @StatusID, @GrantDate "
								+ ")";
							cmd.Parameters.Clear();
							cmd.Parameters.Add("@RequestID", SqlDbType.BigInt).Value = RequestID;
							cmd.Parameters.Add("@LeaveID", SqlDbType.BigInt).Value = LeaveID;
							cmd.Parameters.Add("@Priority", SqlDbType.Int).Value = rec.Priority;
							cmd.Parameters.Add("@HeadPersonNo", SqlDbType.VarChar).Value = rec.HeadPersonNo;
							cmd.Parameters.Add("@GrantStepID", SqlDbType.Int).Value
								= this.AutoGranted ? Const.STATUS_GRANTSTEP_APPROVED : Const.STATUS_GRANTSTEP_PENDING_APPROVAL;
							cmd.Parameters.Add("@StatusID", SqlDbType.Int).Value
								= this.AutoGranted ? Const.STATUS_LEAVE_APPROVED : Const.STATUS_LEAVE_PENDING_APPROVAL;
							cmd.Parameters.Add("@GrantDate", SqlDbType.DateTime).Value
								= this.AutoGranted ? this.ApplyDate : (object)null ?? DBNull.Value;
							cmd.ExecuteNonQuery();
						}
					}
				}
			}
		}

        /// <summary>
        /// สร้างรายการในตาราง Veto (based on ตาราง Profile Veto)
        /// </summary>
        protected void CreateLeaveVetos(Int64 RequestID, Int64[] LeaveIDs)
        {
			using (SqlConnection conn = GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					foreach (var LeaveID in LeaveIDs)
					{
						foreach (var veto in this.Person.Vetoes)
						{
							cmd.CommandText =
								  "INSERT INTO [LV Veto] ( "
								+ "		[Request ID], [Leave ID], [Digest], "
								+ "		[Head Person No], [Head E-Mail], [Action Status], [Action Date], [Reason] "
								+ ") VALUES ( "
								+ "		@RequestID, @LeaveID, @Digest, "
								+ "		@HeadPersonNo, @HeadEmail, @ActionStatus, @ActionDate, @Reason "
								+ ")";
							cmd.Parameters.Clear();
							cmd.Parameters.Add("@RequestID", SqlDbType.BigInt).Value = RequestID;
							cmd.Parameters.Add("@LeaveID", SqlDbType.BigInt).Value = LeaveID;
							cmd.Parameters.Add("@Digest", SqlDbType.VarChar).Value = DigestVeto(veto.PersonNo, RequestID);
							cmd.Parameters.Add("@HeadPersonNo", SqlDbType.VarChar).Value = veto.PersonNo;
							cmd.Parameters.Add("@HeadEmail", SqlDbType.VarChar).Value = veto.Email;
							cmd.Parameters.Add("@ActionStatus", SqlDbType.Int).Value = Const.VETO_PROCEEDING;
							cmd.Parameters.Add("@ActionDate", SqlDbType.DateTime).Value = DateTime.Now;
							cmd.Parameters.Add("@Reason", SqlDbType.VarChar).Value = (object)null ?? string.Empty;
							cmd.ExecuteNonQuery();
						}
					}
				}
			}
		}

        /// <summary>
        /// ลบรายการออกจากตาราง Request, File, Leave, Veto, Grant
        /// </summary>
        /// <param name="RequestID"></param>
        protected void RollbackLeaveRequest(Int64 RequestID)
        {
			using (SqlConnection conn = GetConnection())
			{
				conn.Open();
				using (SqlCommand cmd = conn.CreateCommand())
				{
					cmd.CommandText = "DELETE FROM [LV File] WHERE [Request ID] = @RequestID";
					cmd.Parameters.Clear();
					cmd.Parameters.Add("@RequestID", SqlDbType.BigInt).Value = RequestID;
					cmd.ExecuteNonQuery();

					cmd.CommandText = "DELETE FROM [LV Veto] WHERE [Request ID] = @RequestID";
					cmd.Parameters.Clear();
					cmd.Parameters.Add("@RequestID", SqlDbType.BigInt).Value = RequestID;
					cmd.ExecuteNonQuery();

					cmd.CommandText = "DELETE FROM [LV Grant] WHERE [Request ID] = @RequestID";
					cmd.Parameters.Clear();
					cmd.Parameters.Add("@RequestID", SqlDbType.BigInt).Value = RequestID;
					cmd.ExecuteNonQuery();

					cmd.CommandText = "DELETE FROM [LV Leave] WHERE [Request ID] = @RequestID";
					cmd.Parameters.Clear();
					cmd.Parameters.Add("@RequestID", SqlDbType.BigInt).Value = RequestID;
					cmd.ExecuteNonQuery();

					cmd.CommandText = "DELETE FROM [LV Request] WHERE [Request ID] = @RequestID";
					cmd.Parameters.Clear();
					cmd.Parameters.Add("@RequestID", SqlDbType.BigInt).Value = RequestID;
					cmd.ExecuteNonQuery();
				}
			}
		}

		#endregion

		#region Query Section

		/// <summary>
        /// โหลด Header, Lines ภายใน instance เพื่อเตรียมข้อมูล
        /// </summary>
        /// <param name="RequestID"></param>
        /// <returns>this instance</returns>
        public Leave Load(Int64 RequestID)
        {
            return this.Load(RequestID, 0);
        }

        /// <summary>
        /// โหลด Header, Lines ภายใน instance เพื่อเตรียมข้อมูล
        /// </summary>
        /// <param name="RequestID"></param>
        /// <param name="LeaveID"></param>
        /// <returns>this instance</returns>
        public Leave Load(Int64 RequestID, Int64 LeaveID)
        {
            this.IsLoaded = false;
            this.Header = null;
            this.Lines = Leave.List(User, RequestID, LeaveID);
            if (this.Lines.Count > 0)
                this.Header = this.Lines[0].LeaveRequested;
            this.IsLoaded = (this.Header != null);
            return this;
        }

        /// <summary>
        /// Query database แล้ว gen list ของ LeaveRecord (ใช้คำสั่งที่มีใน SQL2005 or later)
        /// </summary>
        /// <param name="SqlInnerQuery">ต้องมี {0} เป็น field แรก เช่น select {0},a,b,c from table เป็นต้น</param>
        /// <param name="OrderFieldsQuery">fields ที่ต่อจากคำว่า order by ไว้ใช้กับคำสั่ง row_number()</param>
        /// <param name="Parameters">Parameters สำหรับ query, หรือ null ถ้าไม่มี</param>
        /// <param name="Page">เริ่มจาก 1</param>
        /// <param name="PageSize">เริ่มจาก 1</param>
        /// <param name="GetOnlyFirstRecord">true ถ้าต้องการแค่ 1 รายการแรกเพื่อประหยัด Memory</param>
        /// <param name="GetOnlyLastRecord">true ถ้าต้องการแค่ 1 รายการสุดท้ายเพื่อประหยัด Memory</param>
        /// <returns>List&lt;LeaveRecord&gt; ที่ไม่ใช่ null</returns>
        protected List<LeaveRecord> QueryLines(string SqlInnerQuery, string OrderFieldsQuery, List<SqlParameter> Parameters,
            int Page, int PageSize, bool GetOnlyFirstRecord, bool GetOnlyLastRecord)
        {
            if (!User.Identity.IsAuthenticated)
                throw new LeaveException("Access is denied.", null);

            string TopQuery = (Page * PageSize == 0) ? "TOP 100 PERCENT" : "TOP " + (Page * PageSize);
            string Query = "WITH LeavePaging AS(" +
                string.Format(SqlInnerQuery, TopQuery + " ROW_NUMBER() OVER (ORDER BY " + OrderFieldsQuery + ") AS ROWNUM") +
                ") SELECT * FROM LeavePaging WITH(READUNCOMMITTED) WHERE ROWNUM > " + ((Page - 1) * PageSize) +
                " ORDER BY ROWNUM";
            using (SqlConnection conn = this.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = Query;
                    if (Parameters != null && Parameters.Count > 0)
                        cmd.Parameters.AddRange(Parameters.ToArray());
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        // อ่าน output column ก่อน
                        Dictionary<string, string> cols = Tool.ReadColumns(rs.GetSchemaTable());

                        LeaveRecord line = null;
                        List<LeaveRecord> list = new List<LeaveRecord>();
                        while (rs.Read())
                        {
                            line = new LeaveRecord();
                            #region Assign its properties
                            if (cols.ContainsKey("LEAVEID")) line.LeaveID = rs.GetValue<Int64>(cols["LEAVEID"]);
                            if (cols.ContainsKey("STATUSID")) line.StatusID = rs.GetValue<Int32>(cols["STATUSID"]);
                            if (cols.ContainsKey("TYPESUBID")) line.TypeSubID = rs.GetValue<Int32>(cols["TYPESUBID"]);
                            if (cols.ContainsKey("LEAVEDATE")) line.LeaveDate = rs.GetValue<DateTime?>(cols["LEAVEDATE"]);
                            if (cols.ContainsKey("BEGINTIME")) line.BeginTime = rs.GetValue<DateTime?>(cols["BEGINTIME"]);
                            if (cols.ContainsKey("UNTILTIME")) line.UntilTime = rs.GetValue<DateTime?>(cols["UNTILTIME"]);
                            if (cols.ContainsKey("TOTALHOURS")) line.TotalHours = rs.GetValue<decimal>(cols["TOTALHOURS"]);
                            if (cols.ContainsKey("APPROVEMINUTES")) line.ApproveMinutes = rs.GetValue<int>(cols["APPROVEMINUTES"]);
                            if (cols.ContainsKey("COMMENT")) line.Comment = rs.GetValue<string>(cols["COMMENT"]);

							if (cols.ContainsKey("DISPLAYSTATUSNAME"))
								line.DisplayStatusName = rs.GetValue<string>(cols["DISPLAYSTATUSNAME"]);

                            if (cols.ContainsKey("REQUESTID"))
                            {
                                line.LeaveRequested = new RequestRecord();
                                line.LeaveRequested.RequestID = rs.GetValue<Int64>(cols["REQUESTID"]);
                                if (cols.ContainsKey("TYPESUBID")) line.LeaveRequested.TypeSubID = line.TypeSubID;
                                if (cols.ContainsKey("TYPESUBNAME")) line.LeaveRequested.TypeSubName = rs.GetValue<string>(cols["TYPESUBNAME"]);
                                if (cols.ContainsKey("REASON")) line.LeaveRequested.Reason = rs.GetValue<string>(cols["REASON"]);
                                else line.LeaveRequested.Reason = line.Comment;
                                if (cols.ContainsKey("SINCE")) line.LeaveRequested.Since = rs.GetValue<DateTime?>(cols["SINCE"]);
                                if (cols.ContainsKey("UNTIL")) line.LeaveRequested.Until = rs.GetValue<DateTime?>(cols["UNTIL"]);
                                if (cols.ContainsKey("CONTACT")) line.LeaveRequested.Contact = rs.GetValue<string>(cols["CONTACT"]);
                                if (cols.ContainsKey("APPLYDATE")) line.LeaveRequested.ApplyDate = rs.GetValue<DateTime?>(cols["APPLYDATE"]);
                                if (cols.ContainsKey("APPLYBYHR")) line.LeaveRequested.ApplyByHR = rs.GetValue<Int32>(cols["APPLYBYHR"]);

								if (cols.ContainsKey("TOTALAPPROVEMINUTES"))
                                    line.LeaveRequested.TotalApproveMinutes = rs.GetValue<int>(cols["TOTALAPPROVEMINUTES"]);
                                if (cols.ContainsKey("TOTALLEAVEHOURS"))
                                    line.LeaveRequested.TotalLeaveHours = rs.GetValue<decimal>(cols["TOTALLEAVEHOURS"]);
								if (cols.ContainsKey("DISPLAYSTATUSNAME"))
									line.LeaveRequested.DisplayStatusName = rs.GetValue<string>(cols["DISPLAYSTATUSNAME"]);
                                line.LeaveRequested.StatusID = line.StatusID;
							}
                            if (cols.ContainsKey("PERSONNO"))
                            {
                                if (line.LeaveRequested == null) line.LeaveRequested = new RequestRecord();
                                line.LeaveRequested.Person = new PersonRecord();
                                line.LeaveRequested.Person.PersonNo = rs.GetValue<string>(cols["PERSONNO"]);
                                if (cols.ContainsKey("THPREFIX")) line.LeaveRequested.Person.PrefixTH = rs.GetValue<string>(cols["THPREFIX"]);
                                if (cols.ContainsKey("ENPREFIX")) line.LeaveRequested.Person.PrefixEN = rs.GetValue<string>(cols["ENPREFIX"]);
                                if (cols.ContainsKey("THFIRSTNAME")) line.LeaveRequested.Person.NameFirstTH = rs.GetValue<string>(cols["THFIRSTNAME"]);
                                if (cols.ContainsKey("ENFIRSTNAME")) line.LeaveRequested.Person.NameFirstEN = rs.GetValue<string>(cols["ENFIRSTNAME"]);
                                if (cols.ContainsKey("THLASTNAME")) line.LeaveRequested.Person.NameLastTH = rs.GetValue<string>(cols["THLASTNAME"]);
                                if (cols.ContainsKey("ENLASTNAME")) line.LeaveRequested.Person.NameLastEN = rs.GetValue<string>(cols["ENLASTNAME"]);
                                if (cols.ContainsKey("E-MAIL")) line.LeaveRequested.Person.Email = rs.GetValue<string>(cols["E-MAIL"]);
                                if (cols.ContainsKey("MOBILENO")) line.LeaveRequested.Person.Mobile = rs.GetValue<string>(cols["MOBILENO"]);
                            }
							if (cols.ContainsKey("EMPLOYEENO"))
							{
								line.LeaveRequested.Person.Employee = new EmployeeRecord();
								line.LeaveRequested.Person.Employee.EmployeeNo = rs.GetValue<string>(cols["EMPLOYEENO"]);
								if (cols.ContainsKey("STARTINGDATE"))
									line.LeaveRequested.Person.Employee.StartingDate = rs.GetValue<DateTime?>(cols["STARTINGDATE"]);
								if (cols.ContainsKey("UNTILDATE"))
									line.LeaveRequested.Person.Employee.UntilDate = rs.GetValue<DateTime?>(cols["UNTILDATE"]);
								if (cols.ContainsKey("COMPANYCODE"))
									line.LeaveRequested.Person.Employee.CompanyCode = rs.GetValue<string>(cols["COMPANYCODE"]);
								if (cols.ContainsKey("DEPARTMENT"))
									line.LeaveRequested.Person.Employee.Department = rs.GetValue<string>(cols["DEPARTMENT"]);
								if (cols.ContainsKey("SECTION"))
									line.LeaveRequested.Person.Employee.Section = rs.GetValue<string>(cols["SECTION"]);
								if (cols.ContainsKey("THPOSITION"))
									line.LeaveRequested.Person.Employee.PositionTH = rs.GetValue<string>(cols["THPOSITION"]);
								if (cols.ContainsKey("ENPOSITION"))
									line.LeaveRequested.Person.Employee.PositionEN = rs.GetValue<string>(cols["ENPOSITION"]);
							}
							if (cols.ContainsKey("STATUSCOUNT"))
                            {
                                if (rs.GetValue<Int32>(cols["STATUSCOUNT"]) > 1)
                                {
                                    line.StatusID = Const.STATUS_MULTIPLE_STATUSES;
                                }
                            }
                            #endregion
                            if (GetOnlyFirstRecord && list.Count == 0)
                            {
                                list.Add(line);
                            }
                            else if (!GetOnlyLastRecord)
                            {
                                list.Add(line);
                            }
                        }
                        if (line != null)
                        {
                            if (GetOnlyLastRecord && !list.Exists(x => x.LeaveID == line.LeaveID))
                                list.Add(line);
                        }
                        return list;
                    }
                }
            }
        }

		/// <summary>
		/// Get Header โดยใช้ InnerQuery
		/// Is 'protected' Access Modifiers
		/// </summary>
		/// <param name="SqlInnerQuery"></param>
		/// <param name="OrderFieldsQuery"></param>
		/// <param name="Parameters"></param>
		/// <param name="Page"></param>
        /// <param name="PageSize"></param>
        /// <param name="GetOnlyFirstRecord">true ถ้าต้องการแค่ 1 รายการแรกเพื่อประหยัด Memory</param>
        /// <param name="GetOnlyLastRecord">true ถ้าต้องการแค่ 1 รายการสุดท้ายเพื่อประหยัด Memory</param>
		/// <returns></returns>
        protected List<RequestRecord> QueryHeaders(string SqlInnerQuery, string OrderFieldsQuery, List<SqlParameter> Parameters,
            int Page, int PageSize, bool GetOnlyFirstRecord, bool GetOnlyLastRecord)
        {
            if (!User.Identity.IsAuthenticated)
                throw new LeaveException("Access is denied.", null);

            string TopQuery = (Page * PageSize == 0) ? "TOP 100 PERCENT" : "TOP " + (Page * PageSize);
            string Query = "WITH LeavePaging AS(" +
                string.Format(SqlInnerQuery, TopQuery + " ROW_NUMBER() OVER (ORDER BY " + OrderFieldsQuery + ") AS ROWNUM") +
                ") SELECT * FROM LeavePaging WITH(READUNCOMMITTED) WHERE ROWNUM > " + ((Page - 1) * PageSize) +
                " ORDER BY ROWNUM";
            using (SqlConnection conn = this.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = Query;
                    if (Parameters != null && Parameters.Count > 0)
                        cmd.Parameters.AddRange(Parameters.ToArray());
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        // อ่าน output column ก่อน
                        Dictionary<string, string> cols = Tool.ReadColumns(rs.GetSchemaTable());

                        RequestRecord head = null;
                        List<RequestRecord> list = new List<RequestRecord>();
                        while (rs.Read())
                        {
                            head = new RequestRecord();
                            #region Assign its properties
                            if (cols.ContainsKey("REQUESTID")) head.RequestID = rs.GetValue<Int64>(cols["REQUESTID"]);
                            if (cols.ContainsKey("TYPESUBID")) head.TypeSubID = rs.GetValue<Int32>(cols["TYPESUBID"]);
                            if (cols.ContainsKey("TYPESUBNAME")) head.TypeSubName = rs.GetValue<string>(cols["TYPESUBNAME"]);
                            if (cols.ContainsKey("REASON")) head.Reason = rs.GetValue<string>(cols["REASON"]);
                            if (cols.ContainsKey("SINCE")) head.Since = rs.GetValue<DateTime?>(cols["SINCE"]);
                            if (cols.ContainsKey("UNTIL")) head.Until = rs.GetValue<DateTime?>(cols["UNTIL"]);
                            if (cols.ContainsKey("CONTACT")) head.Contact = rs.GetValue<string>(cols["CONTACT"]);
                            if (cols.ContainsKey("APPLYDATE")) head.ApplyDate = rs.GetValue<DateTime?>(cols["APPLYDATE"]);
                            if (cols.ContainsKey("APPLYBYHR")) head.ApplyByHR = rs.GetValue<Int32>(cols["APPLYBYHR"]);
                            if (cols.ContainsKey("REALPATH")) head.AttachedFilepath = rs.GetValue<string>(cols["REALPATH"]);
                            if (cols.ContainsKey("VIRTUALPATH")) head.AttachedVirtualpath = rs.GetValue<string>(cols["VIRTUALPATH"]);

                            if (cols.ContainsKey("TOTALAPPROVEMINUTES"))
                                head.TotalApproveMinutes = rs.GetValue<int>(cols["TOTALAPPROVEMINUTES"]);
                            if (cols.ContainsKey("TOTALLEAVEHOURS"))
                                head.TotalLeaveHours = rs.GetValue<decimal>(cols["TOTALLEAVEHOURS"]);
							if (cols.ContainsKey("DISPLAYSTATUSNAME"))
                                head.DisplayStatusName = rs.GetValue<string>(cols["DISPLAYSTATUSNAME"]);
                            if (cols.ContainsKey("STATUSID"))
                                head.StatusID = rs.GetValue<int>(cols["STATUSID"]);

							if (cols.ContainsKey("PERSONNO"))
                            {
                                head.Person = new PersonRecord();
                                head.Person.PersonNo = rs.GetValue<string>(cols["PERSONNO"]);
                                if (cols.ContainsKey("THPREFIX")) head.Person.PrefixTH = rs.GetValue<string>(cols["THPREFIX"]);
                                if (cols.ContainsKey("ENPREFIX")) head.Person.PrefixEN = rs.GetValue<string>(cols["ENPREFIX"]);
                                if (cols.ContainsKey("THFIRSTNAME")) head.Person.NameFirstTH = rs.GetValue<string>(cols["THFIRSTNAME"]);
                                if (cols.ContainsKey("ENFIRSTNAME")) head.Person.NameFirstEN = rs.GetValue<string>(cols["ENFIRSTNAME"]);
                                if (cols.ContainsKey("THLASTNAME")) head.Person.NameLastTH = rs.GetValue<string>(cols["THLASTNAME"]);
                                if (cols.ContainsKey("ENLASTNAME")) head.Person.NameLastEN = rs.GetValue<string>(cols["ENLASTNAME"]);
                                if (cols.ContainsKey("E-MAIL")) head.Person.Email = rs.GetValue<string>(cols["E-MAIL"]);
                                if (cols.ContainsKey("MOBILENO")) head.Person.Mobile = rs.GetValue<string>(cols["MOBILENO"]);
                            }
							if (cols.ContainsKey("EMPLOYEENO"))
							{
								head.Person.Employee = new EmployeeRecord();
								head.Person.Employee.EmployeeNo = rs.GetValue<string>(cols["EMPLOYEENO"]);
								if (cols.ContainsKey("STARTINGDATE"))
									head.Person.Employee.StartingDate = rs.GetValue<DateTime?>(cols["STARTINGDATE"]);
								if (cols.ContainsKey("UNTILDATE"))
									head.Person.Employee.UntilDate = rs.GetValue<DateTime?>(cols["UNTILDATE"]);
								if (cols.ContainsKey("COMPANYCODE"))
									head.Person.Employee.CompanyCode = rs.GetValue<string>(cols["COMPANYCODE"]);
								if (cols.ContainsKey("DEPARTMENT"))
									head.Person.Employee.Department = rs.GetValue<string>(cols["DEPARTMENT"]);
								if (cols.ContainsKey("SECTION"))
									head.Person.Employee.Section = rs.GetValue<string>(cols["SECTION"]);
								if (cols.ContainsKey("THPOSITION"))
									head.Person.Employee.PositionTH = rs.GetValue<string>(cols["THPOSITION"]);
								if (cols.ContainsKey("ENPOSITION"))
									head.Person.Employee.PositionEN = rs.GetValue<string>(cols["ENPOSITION"]);
							}
                            #endregion
                            if (GetOnlyFirstRecord && list.Count == 0)
                            {
                                list.Add(head);
                            }
                            else if (!GetOnlyLastRecord)
                            {
                                list.Add(head);
                            }
                        }
                        if (head != null)
                        {
                            if (GetOnlyLastRecord && !list.Exists(x => x.RequestID == head.RequestID))
                                list.Add(head);
                        }
                        return list;
                    }
                }
            }
        }

        #endregion

        #region Change Status Section

        /// <summary>
        /// หัวหน้างานระงับ/ไม่ระงับใบลา
        /// </summary>
        /// <param name="HeadPersonNo"></param>
        /// <param name="Digest"></param>
        /// <param name="NewVetoStatus"></param>
        /// <param name="Reason"></param>
        /// <returns>จำนวน Line ที่ถูกระงับ/ไม่ระงับ</returns>
        public int Veto(string HeadPersonNo, string Digest, int NewVetoStatus, string Reason)
        {
            if (User == null || !User.Identity.IsAuthenticated)
                throw new LeaveGrantException(HeadPersonNo, 0, NewVetoStatus, "UNAUTHORIZED_PERMISSION");

            if (NewVetoStatus != Const.VETO_INTERRUPTED && NewVetoStatus != Const.VETO_PROCEEDING)
                throw new LeaveGrantException(HeadPersonNo, 0, NewVetoStatus, "INVALID_STATUS_STATE_DETECTED");

            // ควรจะมี this.Lines แล้ว
            if (!this.IsLoaded)
                throw new LeaveGrantException(HeadPersonNo, 0, NewVetoStatus, "HEADER_OR_LINE_NOT_INITIALIZED");

            // this.Lines ควรจะเป็นใบลาเดียวกัน
            Int64 aRequestID = this.Lines[0].LeaveRequested.RequestID;
            foreach (LeaveRecord line in this.Lines)
            {
                // ตรวจสอบว่าทุกๆ Lines มี Head ตัวเดียวกันหรือเปล่า
                if (aRequestID != line.LeaveRequested.RequestID)
                    throw new LeaveException("MULTIPLE_LEAVE_REQUEST_DETECTED", null);

                // ถ้าใบลาถูกยกเลิกแล้ว ไม่สามารถแก้ไขใดๆได้
                if (line.StatusID == Const.STATUS_LEAVE_CANCELLED)
                    throw new LeaveException("READONLY_LEAVE_HISTORY_DETECTED", null);

                // ถ้าสถานะปัจจุบันของใบลาเป็น Rejected อยู่ ... จะมี HR หรือหัวหน้าเท่านั้นที่แก้ไขได้
                if (line.StatusID == Const.STATUS_LEAVE_REJECTED)
                    throw new LeaveException("UNAUTHORIZED_PERMISSION", null);
            }

            SqlTransaction t = null;
            using (SqlConnection conn = this.GetConnection())
            {
                conn.Open();
                t = conn.BeginTransaction();
                try
                {
                    int lineCount = 0;
                    foreach (LeaveRecord line in this.Lines)
                    {
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = t;
                            #region Perform DB Execution Preparation
                            cmd.CommandType = CommandType.Text;
                            cmd.CommandText = "UPDATE [LV Veto] SET [Action Status]=@status,[Action Date]=@now,[Reason]=@reason WHERE [Leave ID]=@id AND Digest=@digest";
                            cmd.Parameters.Add("@status", SqlDbType.Int).Value = NewVetoStatus;
                            cmd.Parameters.Add("@now", SqlDbType.DateTime).Value = DateTime.Now;
                            cmd.Parameters.Add("@reason", SqlDbType.VarChar, 255).Value = Reason ?? string.Empty;
                            cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = line.LeaveID;
                            cmd.Parameters.Add("@digest", SqlDbType.VarChar).Value = Digest ?? (object)DBNull.Value;
                            #endregion
                            lineCount += cmd.ExecuteNonQuery();
                        }
                    }
                    t.Commit();
                    return lineCount;
                }
                catch (Exception e)
                {
                    try
                    {
                        if (t != null)
                            t.Rollback();
                    }
                    catch
                    {
                    }
                    throw e;
                }
            }
        }

        /// <summary>
        /// หัวหน้าอนุมัติใบลาให้พนักงาน
        /// </summary>
        /// <param name="HeadPersonNo"></param>
        /// <param name="NewStatusID"></param>
        /// <param name="HeadComment"></param>
        /// <returns>จำนวน Line ที่มีการเปลี่ยนสถานะ ซึ่งจะเกิดกรณีผู้อนุมัติคนสุดท้าย</returns>
        public int Grant(string HeadPersonNo, int NewStatusID, string HeadComment)
        {
            // ควรจะมี this.Lines แล้ว
            if (!this.IsLoaded)
                throw new LeaveGrantException(HeadPersonNo, 0, NewStatusID, "HEADER_OR_LINE_NOT_INITIALIZED");

            int[] LEAVE_STATUS_ACTIVES = Const.LEAVE_STATUS_ACTIVE();

            // this.Lines ควรจะเป็นใบลาเดียวกัน
            Int64 aRequestID = this.Lines[0].LeaveRequested.RequestID;
            foreach (LeaveRecord line in this.Lines)
            {
                // ตรวจสอบว่าทุกๆ Lines มี Head ตัวเดียวกันหรือเปล่า
                if (aRequestID != line.LeaveRequested.RequestID)
                    throw new LeaveGrantException(HeadPersonNo, line.LeaveRequested.RequestID, NewStatusID, "MULTIPLE_LEAVE_REQUEST_DETECTED");

                // ถ้าใบลาถูกยกเลิกแล้ว ไม่สามารถแก้ไขใดๆได้
                if (line.StatusID == Const.STATUS_LEAVE_CANCELLED)
                    throw new LeaveGrantException(HeadPersonNo, line.LeaveRequested.RequestID, NewStatusID, "READONLY_LEAVE_HISTORY_DETECTED");

                // โหลดการอนุมัติทั้งหมดของใบลา
                line.Approvals = this.GetApprovals(line.LeaveID);
                if (line.Approvals == null)
                    throw new LeaveGrantException(HeadPersonNo, line.LeaveRequested.RequestID, NewStatusID, "MISSING_APPROVALS");

                if(NewStatusID == Const.STATUS_LEAVE_APPROVED || NewStatusID == Const.STATUS_LEAVE_CANCELAPPROVED)
                {
                    foreach (var app in line.Approvals)
                    {
                        if (NewStatusID == Const.STATUS_LEAVE_APPROVED)
                        {
                            if (app.StatusID == Const.STATUS_LEAVE_REJECTED)
                                throw new LeaveGrantException(HeadPersonNo, line.LeaveRequested.RequestID, NewStatusID, "STATUS_CONFLICT_WITH_OTHER_APPROVAL");
                        }
                        else if (NewStatusID == Const.STATUS_LEAVE_CANCELAPPROVED)
                        {
                            if (app.CancelStatusID == Const.STATUS_LEAVE_CANCELREJECTED)
                                throw new LeaveGrantException(HeadPersonNo, line.LeaveRequested.RequestID, NewStatusID, "STATUS_CONFLICT_WITH_OTHER_APPROVAL");
                        }
                    }
                }

                // มีชื่ออยู่ในลิสต์ผู้อนุมัติหรือเปล่า
                if (this.CurrentApproval(line.Approvals, HeadPersonNo) == null)
                    throw new LeaveGrantException(HeadPersonNo, line.LeaveRequested.RequestID, NewStatusID, "UNAUTHORIZED_PERMISSION");

                // ถ้าสถานะปัจจุบันของใบลาเป็น Inactive อยู่
                if (line.StatusID == Const.STATUS_LEAVE_REJECTED)
                {
                    // ตรวจสอบว่าสถานะเปลี่ยนไปเป็น Active มั้ย
                    // ถ้าเป็น ... ตรวจสอบว่า ถ้า Active แล้ว ใบลาจะไปชนกับใบลาอื่นมั้ย
                    if (LEAVE_STATUS_ACTIVES.Contains(NewStatusID))
                    {
                        if(this.WillConflictWithOthers(line))
                            throw new LeaveGrantException(HeadPersonNo, line.LeaveRequested.RequestID, NewStatusID, "DUPLICATE_AFTER_CHANGE_STATUS");
                    }
                }
            }

            //int i;
            bool grantComplete;
            GrantRecord g;
            SqlTransaction t = null;
            using (SqlConnection conn = this.GetConnection())
            {
                conn.Open();
                t = conn.BeginTransaction();
                try
                {
                    int lineCount = 0;
                    foreach (LeaveRecord line in this.Lines)
                    {
                        g = this.CurrentApproval(line.Approvals, HeadPersonNo);
                        //i = line.Approvals.IndexOf(g);

                        // ตาราง Grant
                        this.GrantNote(conn, t, line, HeadPersonNo, g.Priority, NewStatusID, HeadComment);

                        if (NewStatusID == Const.STATUS_LEAVE_APPROVED || NewStatusID == Const.STATUS_LEAVE_CANCELAPPROVED)
                        {
                            //if (i == line.Approvals.Count - 1)
                            //{
                            //    // 1. เป็นผู้อนุมัติลำดับสุดท้าย 2. ผู้อนุมัติก่อนหน้า Approved หมดแล้ว
                            //    if (NewStatusID == Const.STATUS_LEAVE_APPROVED)
                            //        grantComplete = line.Approvals.Last(a => a.StatusID != NewStatusID) == g;
                            //    else if (NewStatusID == Const.STATUS_LEAVE_CANCELAPPROVED)
                            //        grantComplete = line.Approvals.Last(a => a.CancelStatusID != NewStatusID) == g;
                            //    else
                            //        grantComplete = false;

                            grantComplete = true;
                            foreach(var _g in line.Approvals) // เช็คว่าผู้อนุมัติคนอื่นๆได้อนุมัติไปหมดหรือยัง
                            {
                                if (g == _g) continue; // ยกเว้นตัวเอง
                                if (NewStatusID == Const.STATUS_LEAVE_APPROVED && _g.StatusID != Const.STATUS_LEAVE_APPROVED)
                                {
                                    grantComplete = false;
                                    break;
                                }
                                else if (NewStatusID == Const.STATUS_LEAVE_CANCELAPPROVED && _g.CancelStatusID != Const.STATUS_LEAVE_CANCELAPPROVED)
                                {
                                    grantComplete = false;
                                    break;
                                }
                            }

                                if (grantComplete)
                                {
									if (NewStatusID == Const.STATUS_LEAVE_CANCELAPPROVED)
										NewStatusID = Const.STATUS_LEAVE_CANCELLED;
									// ตาราง Leave
                                    lineCount += this.ChangeStatus(conn, t, line, NewStatusID);

                                    using (SqlCommand cmd = conn.CreateCommand())
                                    {
                                        cmd.Transaction = t;
                                        cmd.CommandType = CommandType.Text;
                                        cmd.CommandText = "UPDATE [LV Leave] SET [Approve Minutes]=[Total Hours]*60 WHERE [Leave ID]=@leaveid";
                                        cmd.Parameters.Add("@leaveid", SqlDbType.BigInt).Value = line.LeaveID;
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            //}
                        }
                        else if (NewStatusID == Const.STATUS_LEAVE_REJECTED || NewStatusID == Const.STATUS_LEAVE_CANCELREJECTED)
                        {
                            if (NewStatusID == Const.STATUS_LEAVE_CANCELREJECTED)
                                NewStatusID = Const.STATUS_LEAVE_APPROVED;
                            // ตาราง Leave
                            lineCount += this.ChangeStatus(conn, t, line, NewStatusID);
                        }
                    }
                    t.Commit();

                    return lineCount;
                }
                catch (Exception e)
                {
                    try
                    {
                        if (t != null)
                            t.Rollback();
                    }
                    catch
                    {
                    }
                    throw e;
                }
            }
        }

        /// <summary>
        /// บันทึกผลการอนุมัติของผู้อนุมัติคนใดคนหนึ่งของใบลา (ตาราง Grant)
        /// </summary>
        /// <param name="Conn"></param>
        /// <param name="Trans"></param>
        /// <param name="Line"></param>
        /// <param name="HeadPersonNo"></param>
        /// <param name="Priority"></param>
        /// <param name="NewStatusID"></param>
        /// <param name="HeadComment"></param>
        /// <returns>จำนวนรายการที่มีการ update ใน database</returns>
        private int GrantNote(SqlConnection Conn, SqlTransaction Trans, LeaveRecord Line, string HeadPersonNo, int Priority, int NewStatusID, string HeadComment)
        {
            if (User == null || !User.Identity.IsAuthenticated)
                throw new LeaveGrantException(HeadPersonNo, Line.LeaveRequested.RequestID, NewStatusID, "UNAUTHORIZED_PERMISSION");

            bool isValidStatus = false;
            switch (NewStatusID)
            {
                // ตาราง Grant จะมีสถานะเหล่านี้เท่านั้น
                case Const.STATUS_LEAVE_PENDING_APPROVAL:
                case Const.STATUS_LEAVE_AWAITING:
                case Const.STATUS_LEAVE_REJECTED:
                case Const.STATUS_LEAVE_APPROVED:
                case Const.STATUS_LEAVE_CANCELREQUEST:
                case Const.STATUS_LEAVE_CANCELAPPROVED:
                case Const.STATUS_LEAVE_CANCELREJECTED:
                    isValidStatus = true;
                    break;
            }
            if (!isValidStatus)
                throw new LeaveGrantException(HeadPersonNo, Line.LeaveRequested.RequestID, NewStatusID, "INVALID_STATUS_STATE_DETECTED");

            // ตรวจสอบสถานะใหม่ว่าต่อเนื่องจากสถานะปัจจุบันหรือไม่
            if (!Leave.IsValidStatusPath(Line.StatusID, NewStatusID))
                throw new LeaveGrantException(HeadPersonNo, Line.LeaveRequested.RequestID, NewStatusID, "INVALID_APPROVE_PATH_DETECTED");
            
            using(SqlCommand cmd = Conn.CreateCommand())
            {
                cmd.Transaction = Trans;
                #region Perform DB Execution Preparation
                cmd.CommandType = CommandType.Text;
                if (NewStatusID == Const.STATUS_LEAVE_CANCELREQUEST ||
                    NewStatusID == Const.STATUS_LEAVE_CANCELAPPROVED ||
                    NewStatusID == Const.STATUS_LEAVE_CANCELREJECTED)
                {
                    cmd.CommandText = "UPDATE [LV Grant] SET " +
                        " [Cancel Grant Step ID]=@step" +
                        ",[Cancel Status ID]=@status" +
                        ",[Cancel Grant Date]=@date" +
                        ",[Cancel Grant Comment]=@comment " +
                        "WHERE [Leave ID]=@id AND [Priority]=@priority AND [Head Person No]=@head";
                    cmd.Parameters.Add("@step", SqlDbType.Int).Value = Grantors.GetGrantCancelStep(Line.StatusID, NewStatusID) ?? (object)DBNull.Value;
                    cmd.Parameters.Add("@date", SqlDbType.DateTime).Value = Grantors.GetGrantCancelDate(Line.StatusID, NewStatusID) ?? (object)DBNull.Value;
                }
                else
                {
                    cmd.CommandText = "UPDATE [LV Grant] SET " +
                        " [Grant Step ID]=@step" +
                        ",[Status ID]=@status" +
                        ",[Grant Date]=@date" +
                        ",[Grant Comment]=@comment " +
                        "WHERE [Leave ID]=@id AND [Priority]=@priority AND [Head Person No]=@head";
                    cmd.Parameters.Add("@step", SqlDbType.Int).Value = Grantors.GetGrantStep(Line.StatusID, NewStatusID);
                    cmd.Parameters.Add("@date", SqlDbType.DateTime).Value = Grantors.GetGrantDate(Line.StatusID, NewStatusID) ?? (object)DBNull.Value;
                }
                cmd.Parameters.Add("@comment", SqlDbType.VarChar).Value = 
                    DBNull.Value.Equals(cmd.Parameters["@date"]) || string.IsNullOrEmpty(HeadComment)
                    ? DBNull.Value : (object)HeadComment;
                cmd.Parameters.Add("@status", SqlDbType.Int).Value = NewStatusID;
                cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = Line.LeaveID;
                cmd.Parameters.Add("@priority", SqlDbType.Int).Value = Priority;
                cmd.Parameters.Add("@head", SqlDbType.VarChar).Value = HeadPersonNo;
                #endregion
                return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// พนักงานเปลี่ยนแปลงรายละเอียดการลาได้ 3 ส่วน คือ สถานะ หมายเหตุการลา และข้อมูลการติดต่อ
        /// </summary>
        /// <param name="NewStatusID">0 หากไม่ต้องการเปลี่ยนสถานะ (ตาราง Leave)</param>
        /// <param name="Remark">null หากไม่ต้องการเปลี่ยนหมายเหตุการลา (ตาราง Leave)</param>
        /// <param name="Contact">null หากไม่ต้องการเปลี่ยนข้อมูลการติดต่อ (ตาราง Request)</param>
        public void Change(int NewStatusID, string Remark, string Contact)
        {
            if (NewStatusID == 0 && Remark == null && Contact == null)
                throw new ArgumentException();

            if (User == null || !User.Identity.IsAuthenticated)
                throw new LeaveException("UNAUTHORIZED_PERMISSION", null);

            // ควรจะมี this.Lines แล้ว
            if (!this.IsLoaded)
                throw new LeaveException("HEADER_OR_LINE_NOT_INITIALIZED", null);

            int[] LEAVE_STATUS_ACTIVES = Const.LEAVE_STATUS_ACTIVE();

            // this.Lines ควรจะเป็นใบลาเดียวกัน
            Int64 aRequestID = this.Lines[0].LeaveRequested.RequestID;
            foreach (LeaveRecord line in this.Lines)
            {
                // ตรวจสอบว่าทุกๆ Lines มี Head ตัวเดียวกันหรือเปล่า
                if (aRequestID != line.LeaveRequested.RequestID)
                    throw new LeaveException("MULTIPLE_LEAVE_REQUEST_DETECTED", null);

                // ถ้าใบลาถูกยกเลิกแล้ว ไม่สามารถแก้ไขใดๆได้
                if (line.StatusID == Const.STATUS_LEAVE_CANCELLED)
                    throw new LeaveException("READONLY_LEAVE_HISTORY_DETECTED", null);

                // ถ้าสถานะปัจจุบันของใบลาเป็น Rejected อยู่ ... จะมี HR หรือหัวหน้าเท่านั้นที่แก้ไขได้
                if (line.StatusID == Const.STATUS_LEAVE_REJECTED && !User.IsInRole(Const.ROLE_HR))
                    throw new LeaveException("UNAUTHORIZED_PERMISSION", null);
            }

            SqlTransaction t = null;
            using (SqlConnection conn = this.GetConnection())
            {
                conn.Open();
                t = conn.BeginTransaction();
                try
                {
                    foreach (LeaveRecord line in this.Lines)
                    {
                        if (NewStatusID != 0 && NewStatusID != line.StatusID)
                            this.ChangeStatus(conn, t, line, NewStatusID);

                        if (Remark != null)
                        {
                            using (SqlCommand cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = t;
                                #region Perform DB Execution Preparation
                                cmd.CommandType = CommandType.Text;
                                cmd.CommandText = "UPDATE [LV Leave] SET [Comment]=@note,[Modify Date]=@now,[Modify Person]=@user WHERE [Leave ID]=@id";
                                cmd.Parameters.Add("@note", SqlDbType.VarChar, 255).Value = Remark;
                                cmd.Parameters.Add("@now", SqlDbType.DateTime).Value = DateTime.Now;
                                cmd.Parameters.Add("@user", SqlDbType.VarChar).Value = User.Identity.Name;
                                cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = line.LeaveID;
                                #endregion
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                    if (Contact != null)
                    {
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = t;
                            #region Perform DB Execution Preparation
                            cmd.CommandType = CommandType.Text;
                            cmd.CommandText = "UPDATE [LV Request] SET [Contact]=@contact WHERE [Request ID]=@id";
                            cmd.Parameters.Add("@contact", SqlDbType.VarChar, 255).Value = Contact;
                            cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = this.Header.RequestID;
                            #endregion
                            cmd.ExecuteNonQuery();
                        }
                    }
                    t.Commit();
                }
                catch (Exception e)
                {
                    try
                    {
                        if (t != null)
                            t.Rollback();
                    }
                    catch
                    {
                    }
                    throw e;
                }
            }
        }

        /// <summary>
        /// เปลี่ยนสถานะใบลาในตาราง Leave
        /// </summary>
        /// <param name="Conn"></param>
        /// <param name="Trans"></param>
        /// <param name="Line"></param>
        /// <param name="NewStatusID"></param>
        /// <returns>จำนวนรายการที่มีการ update ใน database</returns>
        private int ChangeStatus(SqlConnection Conn, SqlTransaction Trans, LeaveRecord Line, int NewStatusID)
        {
            if (User == null || !User.Identity.IsAuthenticated)
                throw new LeaveException(((Line.LeaveRequested ?? new RequestRecord()).Person ?? new PersonRecord()).PersonNo,
                    Line.LeaveRequested.RequestID, "UNAUTHORIZED_PERMISSION", null);

            bool isValidStatus = false;
            switch (NewStatusID)
            {
                // ตาราง Leave จะมีสถานะเหล่านี้เท่านั้น
                case Const.STATUS_LEAVE_PENDING_APPROVAL:
                case Const.STATUS_LEAVE_CANCELLED:
                case Const.STATUS_LEAVE_REJECTED:
                case Const.STATUS_LEAVE_APPROVED:
                case Const.STATUS_LEAVE_CANCELREQUEST:
                case Const.STATUS_LEAVE_CANCELREJECTED:
                    isValidStatus = true;
                    break;
            }
            if (!isValidStatus)
                throw new LeaveException(((Line.LeaveRequested ?? new RequestRecord()).Person ?? new PersonRecord()).PersonNo,
                    Line.LeaveRequested.RequestID, "INVALID_STATUS_STATE", null);

            // ถ้าใบลาถูกยกเลิกแล้ว ไม่สามารถแก้ไขใดๆได้
            if (Line.StatusID == Const.STATUS_LEAVE_CANCELLED)
                throw new LeaveException(((Line.LeaveRequested ?? new RequestRecord()).Person ?? new PersonRecord()).PersonNo,
                    Line.LeaveRequested.RequestID, "READONLY_LEAVE_HISTORY_DETECTED", null);

            // ถ้าสถานะปัจจุบันของใบลาเป็น Inactive อยู่
            if (Line.StatusID == Const.STATUS_LEAVE_REJECTED)
            {
                int[] LEAVE_STATUS_ACTIVES = Const.LEAVE_STATUS_ACTIVE();
                // ตรวจสอบว่าสถานะเปลี่ยนไปเป็น Active มั้ย
                // ถ้าเป็น ... ตรวจสอบว่า ถ้า Active แล้ว ใบลาจะไปชนกับใบลาอื่นมั้ย
                if (LEAVE_STATUS_ACTIVES.Contains(NewStatusID))
                {
                    if (this.WillConflictWithOthers(Line))
                        throw new LeaveException(((Line.LeaveRequested ?? new RequestRecord()).Person ?? new PersonRecord()).PersonNo,
                            Line.LeaveRequested.RequestID, "DUPLICATE_AFTER_CHANGE_STATUS", null);
                }
            }

            // ถ้าเป็นการขอยกเลิก ไป Update สถานะที่ตาราง Grant ด้วย
            if (NewStatusID == Const.STATUS_LEAVE_CANCELREQUEST)
            {
                List<GrantRecord> gt = GetApprovals(Line.LeaveID);
                foreach (GrantRecord g in gt)
                    GrantNote(Conn, Trans, Line, g.HeadPersonNo, g.Priority, NewStatusID, string.Empty);
            }

            // ตรวจสอบสถานะใหม่ว่าต่อเนื่องจากสถานะปัจจุบันหรือไม่
            if (!Leave.IsValidStatusPath(Line.StatusID, NewStatusID))
				// ตรอจสอบว่าเป็นการแก้ไขสถานะใบลาโดย HR หรือไม่ (กรณีนี้ สถานะไม่จำเป็นต้องต่อเนื่อง)
                if (!(NewStatusID == Const.STATUS_LEAVE_CANCELLED && User.IsInRole(Const.ROLE_HR)))
					throw new LeaveException(((Line.LeaveRequested ?? new RequestRecord()).Person ?? new PersonRecord()).PersonNo,
						Line.LeaveRequested.RequestID, "INVALID_STATUS_PATH", null);

            using (SqlCommand cmd = Conn.CreateCommand())
            {
                cmd.Transaction = Trans;
                #region Perform DB Execution Preparation
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "UPDATE [LV Leave] SET [Status ID]=@status,[Approve Minutes]=CASE @status WHEN @reject THEN 0 ELSE [Approve Minutes] END,[Modify Date]=@now,[Modify Person]=@user WHERE [Leave ID]=@id";
                cmd.Parameters.Add("@status", SqlDbType.Int).Value = NewStatusID;
                cmd.Parameters.Add("@reject", SqlDbType.Int).Value = Const.STATUS_LEAVE_REJECTED;
                cmd.Parameters.Add("@now", SqlDbType.DateTime).Value = DateTime.Now;
                cmd.Parameters.Add("@user", SqlDbType.VarChar).Value = User.Identity.Name;
                cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = Line.LeaveID;
                #endregion
                return cmd.ExecuteNonQuery();
            }
        }

		/// <summary>
		/// เปลี่ยนประเภทการลาในตาราง Leave, Request (โดยมีผลทั้งใบลา)
		/// </summary>
		/// <param name="NewTypeSubID"></param>
		/// <returns>จำนวน Line ที่ update</returns>
        public int ChangeType(int NewTypeSubID)
        {
            // HR เปลี่ยนได้คนเดียว
            if (User == null || !User.Identity.IsAuthenticated || !User.IsInRole(Const.ROLE_HR))
                throw new LeaveException("UNAUTHORIZED_PERMISSION", null);

            // ควรจะมี this.Lines แล้ว
            if (!this.IsLoaded)
                throw new LeaveException("HEADER_OR_LINE_NOT_INITIALIZED", null);

            RequestRecord req = this.Header;

            // this.Lines ควรจะเป็นใบลาเดียวกัน
            Int64 aRequestID = req.RequestID;
            foreach (LeaveRecord line in this.Lines)
            {
                // ตรวจสอบว่าทุกๆ Lines มี Head ตัวเดียวกันหรือเปล่า
                if (aRequestID != line.LeaveRequested.RequestID)
                    throw new LeaveException("MULTIPLE_LEAVE_REQUEST_DETECTED", null);

                // ถ้าใบลาถูกยกเลิกแล้ว ไม่สามารถแก้ไขใดๆได้
                if (line.StatusID == Const.STATUS_LEAVE_CANCELLED)
                    throw new LeaveException("READONLY_LEAVE_HISTORY_DETECTED", null);

                // ถ้าสถานะปัจจุบันของใบลาเป็น Rejected อยู่ ... จะมี HR หรือหัวหน้าเท่านั้นที่แก้ไขได้
                if (line.StatusID == Const.STATUS_LEAVE_REJECTED && !User.IsInRole(Const.ROLE_HR))
                    throw new LeaveException("UNAUTHORIZED_PERMISSION", null);
            }
            
            SqlTransaction t = null;
            using (SqlConnection conn = this.GetConnection())
            {
                conn.Open();
                t = conn.BeginTransaction();
                try
                {
                    int lineCount = 0;
                    foreach (LeaveRecord line in this.Lines)
                    {
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = t;
                            cmd.CommandType = CommandType.Text;
                            #region Perform DB Execution Preparation
                            cmd.CommandText = "UPDATE [LV Leave] SET [Type Sub ID]=@typeid,[Modify Date]=@now,[Modify Person]=@user WHERE [Leave ID]=@id";
                            cmd.Parameters.Add("@typeid", SqlDbType.Int).Value = NewTypeSubID;
							cmd.Parameters.Add("@now", SqlDbType.DateTime).Value = DateTime.Now;
                            cmd.Parameters.Add("@user", SqlDbType.VarChar).Value = User.Identity.Name;
                            cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = line.LeaveID;
                            #endregion
                            lineCount += cmd.ExecuteNonQuery();
                        }
                    }

					string TypeSubName = this.ExecuteScalar<string>("SELECT [TH Name] FROM [LV Type Sub] WHERE [Type Sub ID]="+NewTypeSubID, null);

                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = t;
                        cmd.CommandType = CommandType.Text;
                        #region Perform DB Execution Preparation
                        cmd.CommandText = "UPDATE [LV Request] SET [Type Sub ID]=@typeid,[Type Sub Name]=@typename WHERE [Request ID]=@id";
                        cmd.Parameters.Add("@typeid", SqlDbType.Int).Value = NewTypeSubID;
                        cmd.Parameters.Add("@typename", SqlDbType.VarChar).Value = TypeSubName;
						cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = req.RequestID;
                        #endregion
                        cmd.ExecuteNonQuery();
                    }

                    t.Commit();

                    return lineCount;
                }
                catch (Exception e)
                {
                    try
                    {
                        if (t != null)
                            t.Rollback();
                    }
                    catch
                    {
                    }
                    throw e;
                }
            }
        }

		/// <summary>
		/// เปลี่ยนวันที่ใบลาในตาราง Leave
		/// </summary>
		/// <param name="NewLeaveDate"></param>
		/// <returns>จำนวน Line ที่ update</returns>
        public int ChangeDate(DateTime NewLeaveDate)
        {
            // HR เปลี่ยนได้คนเดียว
            if (User == null || !User.Identity.IsAuthenticated || !User.IsInRole(Const.ROLE_HR))
                throw new LeaveException("UNAUTHORIZED_PERMISSION", null);

            // ควรจะมี this.Lines แล้ว
            if (!this.IsLoaded)
                throw new LeaveException("HEADER_OR_LINE_NOT_INITIALIZED", null);

            RequestRecord req = this.Header;
            // กรณีลาหลายวัน ... ไม่ update header
            if (req.Since.Value.Date != req.Until.Value.Date)
                throw new LeaveException("ONLY_ONE_DAY_LEAVE_SUPPORTED", null);

            // this.Lines ควรจะเป็นใบลาเดียวกัน
            Int64 aRequestID = req.RequestID;
            foreach (LeaveRecord line in this.Lines)
            {
                // ตรวจสอบว่าทุกๆ Lines มี Head ตัวเดียวกันหรือเปล่า
                if (aRequestID != line.LeaveRequested.RequestID)
                    throw new LeaveException("MULTIPLE_LEAVE_REQUEST_DETECTED", null);

                // ถ้าใบลาถูกยกเลิกแล้ว ไม่สามารถแก้ไขใดๆได้
                if (line.StatusID == Const.STATUS_LEAVE_CANCELLED)
                    throw new LeaveException("READONLY_LEAVE_HISTORY_DETECTED", null);

                // ถ้าสถานะปัจจุบันของใบลาเป็น Rejected อยู่ ... จะมี HR หรือหัวหน้าเท่านั้นที่แก้ไขได้
                if (line.StatusID == Const.STATUS_LEAVE_REJECTED && !User.IsInRole(Const.ROLE_HR))
                    throw new LeaveException("UNAUTHORIZED_PERMISSION", null);
            }
            
            SqlTransaction t = null;
            using (SqlConnection conn = this.GetConnection())
            {
                conn.Open();
                t = conn.BeginTransaction();
                try
                {
                    int lineCount = 0;
                    foreach (LeaveRecord line in this.Lines)
                    {
						DateTime BeginTime = new DateTime(NewLeaveDate.Year, NewLeaveDate.Month, NewLeaveDate.Day, line.BeginTime.Value.Hour, line.BeginTime.Value.Minute, line.BeginTime.Value.Second);
						DateTime UntilTime = new DateTime(NewLeaveDate.Year, NewLeaveDate.Month, NewLeaveDate.Day, line.UntilTime.Value.Hour, line.UntilTime.Value.Minute, line.UntilTime.Value.Second);
						using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = t;
                            cmd.CommandType = CommandType.Text;
                            #region Perform DB Execution Preparation
                            cmd.CommandText = "UPDATE [LV Leave] SET [Leave Date]=@date,[Begin Time]=@begin,[Until Time]=@until,[Modify Date]=@now,[Modify Person]=@user WHERE [Leave ID]=@id";
                            cmd.Parameters.Add("@date", SqlDbType.DateTime).Value = NewLeaveDate.Date;
                            cmd.Parameters.Add("@begin", SqlDbType.DateTime).Value = BeginTime;
                            cmd.Parameters.Add("@until", SqlDbType.DateTime).Value = UntilTime;
                            cmd.Parameters.Add("@now", SqlDbType.DateTime).Value = DateTime.Now;
                            cmd.Parameters.Add("@user", SqlDbType.VarChar).Value = User.Identity.Name;
                            cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = line.LeaveID;
                            #endregion
                            lineCount += cmd.ExecuteNonQuery();
                        }
                    }

                    DateTime since = new DateTime(NewLeaveDate.Year, NewLeaveDate.Month, NewLeaveDate.Day, req.Since.Value.Hour, req.Since.Value.Minute, req.Since.Value.Second);
                    DateTime until = new DateTime(NewLeaveDate.Year, NewLeaveDate.Month, NewLeaveDate.Day, req.Until.Value.Hour, req.Until.Value.Minute, req.Since.Value.Second);

                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = t;
                        cmd.CommandType = CommandType.Text;
                        #region Perform DB Execution Preparation
                        cmd.CommandText = "UPDATE [LV Request] SET [Since]=@since,[Until]=@until WHERE [Request ID]=@id";
                        cmd.Parameters.Add("@since", SqlDbType.DateTime).Value = since;
                        cmd.Parameters.Add("@until", SqlDbType.DateTime).Value = until;
                        cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = req.RequestID;
                        #endregion
                        cmd.ExecuteNonQuery();
                    }

                    t.Commit();

                    return lineCount;
                }
                catch (Exception e)
                {
                    try
                    {
                        if (t != null)
                            t.Rollback();
                    }
                    catch
                    {
                    }
                    throw e;
                }
            }
        }

		/// <summary>
		/// เปลี่ยนช่วงเวลาในตาราง Leave
		/// </summary>
		/// <param name="NewBeginTime">เวลาเริ่ม</param>
		/// <param name="NewUntilTime">จนถึงเวลา</param>
		/// <param name="BreakMinute">หักเวลาพักเที่ยง</param>
		/// <returns>จำนวน Line ที่ update</returns>
        public int ChangeTime(TimeSpan NewBeginTime, TimeSpan NewUntilTime/*, int BreakMinute*/)
        {
            // HR เปลี่ยนได้คนเดียว
            if (User == null || !User.Identity.IsAuthenticated || !User.IsInRole(Const.ROLE_HR))
                throw new LeaveException("UNAUTHORIZED_PERMISSION", null);

            // ควรจะมี this.Lines แล้ว
            if (!this.IsLoaded)
                throw new LeaveException("HEADER_OR_LINE_NOT_INITIALIZED", null);

            RequestRecord req = this.Header;

            // this.Lines ควรจะเป็นใบลาเดียวกัน
            Int64 aRequestID = req.RequestID;
            foreach (LeaveRecord line in this.Lines)
            {
                // ตรวจสอบว่าทุกๆ Lines มี Head ตัวเดียวกันหรือเปล่า
                if (aRequestID != line.LeaveRequested.RequestID)
                    throw new LeaveException("MULTIPLE_LEAVE_REQUEST_DETECTED", null);

                // ถ้าใบลาถูกยกเลิกแล้ว ไม่สามารถแก้ไขใดๆได้
                if (line.StatusID == Const.STATUS_LEAVE_CANCELLED)
                    throw new LeaveException("READONLY_LEAVE_HISTORY_DETECTED", null);

                // ถ้าสถานะปัจจุบันของใบลาเป็น Rejected อยู่ ... จะมี HR หรือหัวหน้าเท่านั้นที่แก้ไขได้
                if (line.StatusID == Const.STATUS_LEAVE_REJECTED && !User.IsInRole(Const.ROLE_HR))
                    throw new LeaveException("UNAUTHORIZED_PERMISSION", null);
            }

			//TimeSpan temp = NewUntilTime - NewBeginTime - new TimeSpan(0, BreakMinute, 0);
            
            SqlTransaction t = null;
            using (SqlConnection conn = this.GetConnection())
            {
                conn.Open();
                t = conn.BeginTransaction();
                try
                {
                    int lineCount = 0;
					bool IsBeginOrUntilDate = false;
					DateTime MinDate = this.ExecuteScalar<DateTime>("SELECT MIN([Leave Date]) FROM [LV Leave] WHERE [Request ID]="+req.RequestID, null);
					DateTime MaxDate = this.ExecuteScalar<DateTime>("SELECT MAX([Leave Date]) FROM [LV Leave] WHERE [Request ID]="+req.RequestID, null);
					DateTime Changed = MinDate;
                    foreach (LeaveRecord line in this.Lines)
                    {
						DateTime Begin = new DateTime(line.LeaveDate.Value.Year, line.LeaveDate.Value.Month, line.LeaveDate.Value.Day, NewBeginTime.Hours, NewBeginTime.Minutes, NewBeginTime.Seconds);
						DateTime Until = new DateTime(line.LeaveDate.Value.Year, line.LeaveDate.Value.Month, line.LeaveDate.Value.Day, NewUntilTime.Hours, NewUntilTime.Minutes, NewUntilTime.Seconds);
						if (!IsBeginOrUntilDate && (line.LeaveDate.Value.Date == MinDate.Date || line.LeaveDate.Value.Date == MaxDate.Date))
						{
							IsBeginOrUntilDate = true;
							if (line.LeaveDate.Value.Date == MinDate.Date)
								Changed = MinDate;
							if (line.LeaveDate.Value.Date == MaxDate.Date)
								Changed = MaxDate;
						}
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = t;
                            cmd.CommandType = CommandType.Text;
                            #region Perform DB Execution Preparation
                            //cmd.CommandText = "UPDATE [LV Leave] SET [Begin Time]=@begin,[Until Time]=@until,[Total Hours]=@hours,[Total Days]=@days,[Approve Days]=(CASE WHEN [Approve Days]=0 THEN 0 ELSE @appdays END),[Modify Date]=@now,[Modify Person]=@user WHERE [Leave ID]=@id";
                            cmd.CommandText = "UPDATE [LV Leave] SET [Begin Time]=@begin,[Until Time]=@until,[Modify Date]=@now,[Modify Person]=@user WHERE [Leave ID]=@id";
                            cmd.Parameters.Add("@begin", SqlDbType.DateTime).Value = Begin;
                            cmd.Parameters.Add("@until", SqlDbType.DateTime).Value = Until;
                            //cmd.Parameters.Add("@hours", SqlDbType.Decimal).Value = temp.TotalMinutes / 60.0;
                            //cmd.Parameters.Add("@days", SqlDbType.Decimal).Value = temp.TotalMinutes / 480.0;
                            //cmd.Parameters.Add("@appdays", SqlDbType.Decimal).Value = temp.TotalMinutes / 480.0;
							cmd.Parameters.Add("@now", SqlDbType.DateTime).Value = DateTime.Now;
                            cmd.Parameters.Add("@user", SqlDbType.VarChar).Value = User.Identity.Name;
                            cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = line.LeaveID;
                            #endregion
                            lineCount += cmd.ExecuteNonQuery();
                        }
                    }

					if (IsBeginOrUntilDate)
					{
						DateTime since = Changed.Date == req.Since.Value.Date
							? new DateTime(req.Since.Value.Year, req.Since.Value.Month, req.Since.Value.Day, NewBeginTime.Hours, NewBeginTime.Minutes, NewBeginTime.Seconds)
							: req.Since.Value;
						DateTime until = Changed.Date == req.Until.Value.Date 
							? new DateTime(req.Until.Value.Year, req.Until.Value.Month, req.Until.Value.Day, NewUntilTime.Hours, NewUntilTime.Minutes, NewUntilTime.Seconds)
							: req.Until.Value;
						using (SqlCommand cmd = conn.CreateCommand())
						{
							cmd.Transaction = t;
							cmd.CommandType = CommandType.Text;
							#region Perform DB Execution Preparation
							cmd.CommandText = "UPDATE [LV Request] SET [Since]=@since,[Until]=@until WHERE [Request ID]=@id";
							cmd.Parameters.Add("@since", SqlDbType.DateTime).Value = since;
							cmd.Parameters.Add("@until", SqlDbType.DateTime).Value = until;
							cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = req.RequestID;
							#endregion
							cmd.ExecuteNonQuery();
						}
					}

                    t.Commit();

                    return lineCount;
                }
                catch (Exception e)
                {
                    try
                    {
                        if (t != null)
                            t.Rollback();
                    }
                    catch
                    {
                    }
                    throw e;
                }
            }
        }

		/// <summary>
		/// เปลี่ยนชั่วโมงลาในตาราง Leave
		/// </summary>
		/// <param name="NewTotalHours"></param>
		/// <returns>จำนวน Line ที่ update</returns>
        public int ChangeAmount(decimal NewTotalHours)
        {
            // HR เปลี่ยนได้คนเดียว
            if (User == null || !User.Identity.IsAuthenticated || !User.IsInRole(Const.ROLE_HR))
                throw new LeaveException("UNAUTHORIZED_PERMISSION", null);

            // ควรจะมี this.Lines แล้ว
            if (!this.IsLoaded)
                throw new LeaveException("HEADER_OR_LINE_NOT_INITIALIZED", null);

            RequestRecord req = this.Header;

            // this.Lines ควรจะเป็นใบลาเดียวกัน
            Int64 aRequestID = req.RequestID;
            foreach (LeaveRecord line in this.Lines)
            {
                // ตรวจสอบว่าทุกๆ Lines มี Head ตัวเดียวกันหรือเปล่า
                if (aRequestID != line.LeaveRequested.RequestID)
                    throw new LeaveException("MULTIPLE_LEAVE_REQUEST_DETECTED", null);

                // ถ้าใบลาถูกยกเลิกแล้ว ไม่สามารถแก้ไขใดๆได้
                if (line.StatusID == Const.STATUS_LEAVE_CANCELLED)
                    throw new LeaveException("READONLY_LEAVE_HISTORY_DETECTED", null);

                // ถ้าสถานะปัจจุบันของใบลาเป็น Rejected อยู่ ... จะมี HR หรือหัวหน้าเท่านั้นที่แก้ไขได้
                if (line.StatusID == Const.STATUS_LEAVE_REJECTED && !User.IsInRole(Const.ROLE_HR))
                    throw new LeaveException("UNAUTHORIZED_PERMISSION", null);
            }
            
            SqlTransaction t = null;
            using (SqlConnection conn = this.GetConnection())
            {
                conn.Open();
                t = conn.BeginTransaction();
                try
                {
                    int lineCount = 0;
                    foreach (LeaveRecord line in this.Lines)
                    {
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = t;
                            cmd.CommandType = CommandType.Text;
                            #region Perform DB Execution Preparation
                            cmd.CommandText = "UPDATE [LV Leave] SET [Total Hours]=@hours,[Total Days]=@days,[Approve Minutes]=(CASE WHEN [Approve Minutes]=0 THEN 0 ELSE @appmins END),[Modify Date]=@now,[Modify Person]=@user WHERE [Leave ID]=@id";
                            cmd.Parameters.Add("@hours", SqlDbType.Decimal).Value = NewTotalHours;
                            cmd.Parameters.Add("@days", SqlDbType.Decimal).Value = NewTotalHours / Const.DEFAULT_WORKHOURS_OF_DAY;
                            cmd.Parameters.Add("@appmins", SqlDbType.Int).Value = NewTotalHours * 60;
							cmd.Parameters.Add("@now", SqlDbType.DateTime).Value = DateTime.Now;
                            cmd.Parameters.Add("@user", SqlDbType.VarChar).Value = User.Identity.Name;
                            cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = line.LeaveID;
                            #endregion
                            lineCount += cmd.ExecuteNonQuery();
                        }
                    }

                    t.Commit();

                    return lineCount;
                }
                catch (Exception e)
                {
                    try
                    {
                        if (t != null)
                            t.Rollback();
                    }
                    catch
                    {
                    }
                    throw e;
                }
            }
        }

        /// <summary>
        /// Get การระงับของผู้มีสิทธิ์ระงับทั้งหมด
        /// </summary>
        /// <param name="LeaveID">เลขที่ใบลา</param>
        /// <returns>Empty list หากไม่มีการเซ็ตการระงับไว้</returns>
        protected List<VetoRecord> GetVetoes(Int64? RequestID, Int64? LeaveID)
        {
            using (SqlConnection conn = this.GetConnection())
            {
                conn.Open();
                List<VetoRecord> list = new List<VetoRecord>();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "SELECT DISTINCT [Request ID],[Digest],[Head Person No],[Head E-Mail],[Action Status],[Action Date],[Reason] " +
                        "FROM [LV Veto] WITH(READUNCOMMITTED) WHERE [Leave ID]=@id OR [Request ID]=@rid";
                    cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = LeaveID.HasValue ? LeaveID : (object)DBNull.Value;
                    cmd.Parameters.Add("@rid", SqlDbType.BigInt).Value = RequestID.HasValue ? RequestID : (object)DBNull.Value;
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        VetoRecord rec;
                        Dictionary<string, string> cols = Tool.ReadColumns(rs.GetSchemaTable());
                        while (rs.Read())
                        {
                            rec = new VetoRecord();
                            #region Read Properties
                            rec.RequestID = rs.GetValue<Int64>(cols["REQUESTID"]);
                            rec.Digest = rs.GetValue<string>(cols["DIGEST"]);
                            rec.HeadPersonNo = rs.GetValue<string>(cols["HEADPERSONNO"]);
                            rec.NotifyToEmail = rs.GetValue<string>(cols["HEADE-MAIL"]);
                            rec.ActionStatus = rs.GetValue<int>(cols["ACTIONSTATUS"]);
                            rec.ActionDate = rs.GetValue<DateTime?>(cols["ACTIONDATE"]);
                            rec.Reason = rs.GetValue<string>(cols["REASON"]);
                            #endregion
                            list.Add(rec);
                        }
                    }
                }
                // วน loop เพื่อ get person information ของ Veto Head (แบบประหยัด Connection ใน Pool)
                foreach (VetoRecord rec in list)
                {
                    rec.Person = Person.GetInfo(User, rec.HeadPersonNo, conn);
                }
                return list;
            }
        }

        /// <summary>
        /// Get การอนุมัติของผู้อนุมัติทั้งหมด
        /// </summary>
        /// <param name="LeaveID">เลขที่ใบลา</param>
        /// <returns>Empty list หากไม่มีการเซ็ตการอนุมัติไว้</returns>
        protected List<GrantRecord> GetApprovals(Int64 LeaveID)
        {
            using (SqlConnection conn = this.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "SELECT * FROM [LV Grant] WITH(READUNCOMMITTED) WHERE [Leave ID]=@id ORDER BY [Priority]";
                    cmd.Parameters.Add("@id", SqlDbType.Int).Value = LeaveID;
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        GrantRecord rec;
                        List<GrantRecord> list = new List<GrantRecord>();
                        Dictionary<string, string> cols = Tool.ReadColumns(rs.GetSchemaTable());
                        while (rs.Read())
                        {
                            rec = new GrantRecord();
                            #region Read Properties
                            rec.RequestID = rs.GetValue<Int64>(cols["REQUESTID"]);
                            rec.LeaveID = rs.GetValue<Int64>(cols["LEAVEID"]);
                            rec.Priority = rs.GetValue<int>(cols["PRIORITY"]);
                            rec.HeadPersonNo = rs.GetValue<string>(cols["HEADPERSONNO"]);
                            rec.GrantStepID = rs.GetValue<int>(cols["GRANTSTEPID"]);
                            rec.StatusID = rs.GetValue<int>(cols["STATUSID"]);
                            rec.GrantDate = rs.GetValue<DateTime?>(cols["GRANTDATE"]);
                            rec.GrantComment = rs.GetValue<string>(cols["GRANTCOMMENT"]);
                            rec.CancelGrantStepID = rs.GetValue<int>(cols["CANCELGRANTSTEPID"]);
                            rec.CancelStatusID = rs.GetValue<int>(cols["CANCELSTATUSID"]);
                            rec.CancelGrantDate = rs.GetValue<DateTime?>(cols["CANCELGRANTDATE"]);
                            rec.CancelGrantComment = rs.GetValue<string>(cols["CANCELGRANTCOMMENT"]);
                            #endregion
                            list.Add(rec);
                        }
                        return list;
                    }
                }
            }
        }

        /// <summary>
        /// หา Grant Record ล่าสุดที่เป็นของ Head ที่ระบุ
        /// </summary>
        /// <param name="gs"></param>
        /// <param name="HeadPersonNo"></param>
        /// <returns>null ถ้าไม่เจอ</returns>
        protected GrantRecord CurrentApproval(List<GrantRecord> gs, string HeadPersonNo)
        {
            if (gs == null || gs.Count == 0)
                return null;

            if (gs.Count == 1)
                return gs[0].HeadPersonNo == HeadPersonNo ? gs[0] :  null;

            // กรณี Worse Case คือ 1 ใบลา เซ็ตผู้อนุมัติไว้ 2 ลำดับหรือมากกว่า และทั้ง 2 ลำดับเป็นผู้อนุมัติคนเดียวกัน (HR Faulty)
            // ถ้าเป็นกรณีนี้ return เฉพาะ Grant ที่เป็นปัจจุบันที่สุด
            // เริ่มจาก Sort by Priority ก่อน
            gs.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            GrantRecord g = null;
            // วน loop ดูที่วันที่ Grant ก่อนแล้ว return อันแรกที่ยังไม่มีการพิจารณา
            for (int i = 0; i < gs.Count; i++)
            {
                if (gs[i].HeadPersonNo == HeadPersonNo)
                {
                    if (!gs[i].GrantDate.HasValue) // ยังไม่มี Grant Date แสดงว่ายังไม่มีการพิจารณา
                        return gs[i];
                    g = gs[i];
                }
            }

            // มาถึงจุดนี้แสดงว่ามีการพิจารณาทุก Step แล้ว ซึ่งก็แค่ return อันสุดท้าย ซึ่งเป็นอันที่ Current สุดแล้ว
            return g;
        }

        protected bool WillConflictWithOthers(LeaveRecord line)
        {
            string Query = string.Format("SELECT 1 FROM [LV Leave] WITH(READUNCOMMITTED) " +
                "WHERE [Leave ID]<>@id AND [Person No]=@perno AND [Leave Date]=@date AND [Status ID] NOT IN ({0}) AND (" +
                "   (CONVERT(VARCHAR,[Begin Time],108) < CONVERT(VARCHAR,@until,108) AND CONVERT(VARCHAR,[Begin Time],108) BETWEEN CONVERT(VARCHAR,@begin,108) AND CONVERT(VARCHAR,@until,108)) " +
                "OR (CONVERT(VARCHAR,[Until Time],108) > CONVERT(VARCHAR,@begin,108) AND CONVERT(VARCHAR,[Until Time],108) BETWEEN CONVERT(VARCHAR,@begin,108) AND CONVERT(VARCHAR,@until,108)) " +
                "OR (CONVERT(VARCHAR,@begin,108) >= CONVERT(VARCHAR,[Begin Time],108) AND CONVERT(VARCHAR,[Until Time],108) >= CONVERT(VARCHAR,@until,108)) " +
                ")", string.Join(",", Const.LEAVE_STATUS_ACTIVE()));
            using (SqlConnection conn = this.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = Query;
                    cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = line.LeaveID;
                    cmd.Parameters.Add("@perno", SqlDbType.VarChar).Value = ((line.LeaveRequested ?? new RequestRecord()).Person ?? new PersonRecord()).PersonNo;
                    cmd.Parameters.Add("@date", SqlDbType.DateTime).Value = ((object)line.LeaveDate) ?? DBNull.Value;
                    cmd.Parameters.Add("@begin", SqlDbType.DateTime).Value = ((object)line.BeginTime) ?? DBNull.Value;
                    cmd.Parameters.Add("@until", SqlDbType.DateTime).Value = ((object)line.UntilTime) ?? DBNull.Value;
                    object result = cmd.ExecuteScalar();
                    if (result == null || DBNull.Value.Equals(result))
                        return false;
                    return true;
                }
            }
        }

        #endregion

        #region Static Functions

        /// <summary>
        /// ตรวจสอบกับ Status Model ว่าการเปลี่ยนสถานะถูกต้องมั้ย
        /// </summary>
        /// <param name="CurrentStatus"></param>
        /// <param name="NewStatus"></param>
        /// <returns></returns>
        public static bool IsValidStatusPath(int CurrentStatus, int NewStatus)
        {
            if (CurrentStatus == Const.STATUS_LEAVE_PENDING_APPROVAL)
            #region Switch / Case
            {
                switch (NewStatus)
                {
                    case Const.STATUS_LEAVE_CANCELLED:
                    case Const.STATUS_LEAVE_APPROVED:
                    case Const.STATUS_LEAVE_AWAITING:
                    case Const.STATUS_LEAVE_REJECTED:
                        return true;
                }
            }
            else if (CurrentStatus == Const.STATUS_LEAVE_AWAITING)
            {
                switch (NewStatus)
                {
                    case Const.STATUS_LEAVE_PENDING_APPROVAL:
                    case Const.STATUS_LEAVE_CANCELLED:
                    case Const.STATUS_LEAVE_APPROVED:
                    case Const.STATUS_LEAVE_REJECTED:
                        return true;
                }
            }
            else if (CurrentStatus == Const.STATUS_LEAVE_REJECTED)
            {
                switch (NewStatus)
                {
                    case Const.STATUS_LEAVE_PENDING_APPROVAL:
                    case Const.STATUS_LEAVE_AWAITING:
                    case Const.STATUS_LEAVE_APPROVED:
                        return true;
                }
            }
            else if (CurrentStatus == Const.STATUS_LEAVE_APPROVED)
            {
                switch (NewStatus)
                {
                    case Const.STATUS_LEAVE_CANCELREQUEST:
                        return true;
                }
            }
            else if (CurrentStatus == Const.STATUS_LEAVE_CANCELREQUEST)
            {
                switch (NewStatus)
                {
                    case Const.STATUS_LEAVE_CANCELREJECTED:
                    case Const.STATUS_LEAVE_CANCELAPPROVED:
                    case Const.STATUS_LEAVE_CANCELLED:
                    case Const.STATUS_LEAVE_APPROVED:
                    case Const.STATUS_LEAVE_REJECTED:
                        return true;
                }
            }
            #endregion
            return false;
        }

        #region List Usage
        //พนักงานติดตาม
        //        List<RequestRecord> Leave.ListRecent(User, PersonNo, Page, Size)

        //พนักงานค้น History
        //        List<RequestRecord> Leave.List(User, PersonNo, Begin, Until, TypeID, Page, Size)

        //หัวหน้าอนุมัติผ่านเว็บ
        //        List<RequestRecord> Leave.ListApprove(User, HeadPersonNo, Page, Size)

        //หัวหน้าเปิดดูใบลาเพื่ออนุมัติ
        //        List<LeaveRecord> Leave.List(User, RequestID)

        //หัวหน้ากด Next เพื่อเปิดใบลาถัดไป
        //        List<LeaveRecord> Leave.ListNextApprove(User, CurrentRequestID)

        //หัวหน้ากด Back เพื่อเปิดใบลาก่อนหน้า
        //        List<LeaveRecord> Leave.ListPrevApprove(User, CurrentRequestID)

        //หัวหน้าอนุมัติผ่านอีเมลล์
        //        List<LeaveRecord> Leave.ListApprove(User, HeadPersonNo)

        //หัวหน้า Veto
        //        List<LeaveRecord> Leave.List(User, RequestID)

        //HR ค้น History
        //        List<RequestRecord> Leave.List(User, PersonNo, Begin, Until, TypeID, Page, Size)
        #endregion

        /// <summary>
        /// ดึงรายการจาก database โดยใช้เงื่อนไขของ Request ID หรือ Leave ID ก็ได้
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="RequestID">Request ID หรือ 0 ถ้าไม่ระบุ</param>
        /// <returns>List&lt;LeaveRecord&gt; ที่ไม่ใช่ null</returns>
        public static List<LeaveRecord> List(IPrincipal User, Int64 RequestID)
        {
            return List(User, RequestID, 0);
        }

        /// <summary>
        /// ดึงรายการจาก database โดยใช้เงื่อนไขของ Request ID หรือ Leave ID ก็ได้
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="RequestID">Request ID หรือ 0 ถ้าไม่ระบุ</param>
        /// <param name="LeaveID"></param>
        /// <returns>List&lt;LeaveRecord&gt; ที่ไม่ใช่ null</returns>
        public static List<LeaveRecord> List(IPrincipal User, Int64 RequestID, Int64 LeaveID)
        {
            return QueryLines(User, RequestID, LeaveID, null, 0, null, null, null, null, 0, 0, 0, false, false);
        }

        /// <summary>
        /// ดึงรายการจาก database โดยใช้เงื่อนไขต่างๆตาม Parameters (เรียงจาก Request ID, Leave ID น้อยไปมาก)
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="RequestID">Request ID หรือ 0 ถ้าไม่ระบุ</param>
        /// <param name="LeaveID">Leave ID หรือ 0 หากไม่ระบุ</param>
        /// <param name="CurrentRequestIDCompare">ใช้คู่กับ CurrentRequestID เป็นเครื่องหมายเปรียบเทียบ (&lt;,&gt; ฯลฯ) หรือ null หากไม่ต้องการระบุ</param>
        /// <param name="CurrentRequestID">เลขที่ใบลาที่ใช้เป็นฐานในการค้นหาใบลาถัดไป หรือ 0 หากไม่ระบุ</param>
        /// <param name="PersonNo">รหัสพนักงาน หรือ null หากไม่ระบุ</param>
        /// <param name="Begin">วันที่ใบลาสำหรับ "ตั้งแต่" หรือ null หากไม่ระบุ</param>
        /// <param name="Until">วันที่ใบลาสำหรับ "จนถึง" หรือ null หากไม่ระบุ</param>
        /// <param name="HeadPersonNo">รหัสหัวหน้า หรือ null หากไม่ระบุ</param>
        /// <param name="TypeSubID">รหัสประเภทการลา หรือ 0 หากไม่ระบุ</param>
        /// <param name="Page">เริ่มจาก 1</param>
        /// <param name="PageSize">เริ่มจาก 1 หรือ 0 หากไม่ต้องการระบุจำนวน</param>
        /// <param name="GetOnlyFirstRecord">true ถ้าต้องการแค่ 1 รายการแรกเพื่อประหยัด Memory</param>
        /// <param name="GetOnlyLastRecord">true ถ้าต้องการแค่ 1 รายการสุดท้ายเพื่อประหยัด Memory</param>
        /// <returns>List&lt;LeaveRecord&gt; ที่ไม่ใช่ null</returns>
        protected static List<LeaveRecord> QueryLines(IPrincipal User, Int64 RequestID, Int64 LeaveID, string CurrentRequestIDCompare
            , Int64 CurrentRequestID, string PersonNo, DateTime? Begin, DateTime? Until, string HeadPersonNo
            , int TypeSubID, int Page, int PageSize, bool GetOnlyFirstRecord, bool GetOnlyLastRecord)
        {
            List<SqlParameter> Params = new List<SqlParameter>(2);
            Params.Add(new SqlParameter("@headid", SqlDbType.BigInt));
            Params[0].Value = RequestID;
            Params.Add(new SqlParameter("@lineid", SqlDbType.BigInt));
            Params[1].Value = LeaveID;
            Params.Add(new SqlParameter("@perno", SqlDbType.VarChar));
            Params[2].Value = string.IsNullOrEmpty(PersonNo) ? (object)DBNull.Value : PersonNo;
            Params.Add(new SqlParameter("@begin", SqlDbType.DateTime));
            Params[3].Value = Begin.HasValue ? Begin : (object)DBNull.Value;
            Params.Add(new SqlParameter("@until", SqlDbType.DateTime));
            Params[4].Value = Until.HasValue ? Until : (object)DBNull.Value;

            string WhereCurrentRequestID = CurrentRequestID != 0 ? string.Format(" AND head.[Request ID]{0}{1} ", CurrentRequestIDCompare, CurrentRequestID) : "";
            string WherePersonNo = string.IsNullOrEmpty(PersonNo) ? "" : " AND head.[Person No]=@perno ";

            string WhereLeaveID = "";
            if (LeaveID != 0) WhereLeaveID = "AND line.[Leave ID]=@lineid ";
            else if(RequestID != 0) WhereLeaveID = "AND head.[Request ID]=@headid ";

            string WhereLeaveDates = "";
            if (Begin.HasValue && Until.HasValue) WhereLeaveDates = "AND line.[Leave Date] BETWEEN @begin AND @until ";
            else if (Begin.HasValue) WhereLeaveDates = "AND line.[Leave Date]>=@begin ";
            else if (Until.HasValue) WhereLeaveDates = "AND line.[Leave Date]<=@until ";

            string WhereTypeSubID = TypeSubID > 0 ? string.Format("AND head.[Type Sub ID]={0} ", TypeSubID) : "";

            List<SqlParameter> Params2;
            string WhereHeadPersonNo = "";
            if (!string.IsNullOrEmpty(HeadPersonNo))
            {
                WhereHeadPersonNo = WhereApprovingBy("AND", HeadPersonNo, "head", "line", out Params2);
                Params.AddRange(Params2);
            }

            string OrderBy = "head.[Request ID], line.[Leave ID]";
            string Query = "SELECT {0},head.[Request ID] " +
                  ",head.[Type Sub ID],head.[Type Sub Name],head.[Person No],head.[Reason] " +
                  ",head.[Since],head.[Until],head.[Contact],head.[Apply Date],head.[Apply By HR] " +
                  ",line.[Leave ID],line.[Leave Date],line.[Begin Time],line.[Until Time] " +
                  ",line.[Total Hours],line.[Total Days],line.[Approve Minutes],line.[Comment],line.[Modify Date],line.[Modify Person] " +
                  ",person.[TH Prefix],person.[TH First Name],person.[TH Last Name] " +
                  ",person.[EN First Name],person.[EN Last Name],person.[EN Prefix],person.[Mobile No],person.[E-Mail] " +
                  ",emp.[Employee No],emp.[Starting Date],emp.[Until Date],emp.[Company Code],emp.Department,emp.Section" +
                  ",emp.[TH Position],emp.[EN Position]" +
                  ",ISNULL((SELECT TOP 1 " + Const.STATUS_LEAVE_INTERRUPTED + " FROM [LV Veto] WHERE [Leave ID]=line.[Leave ID] AND [Action Status]=" + Const.VETO_INTERRUPTED + "),line.[Status ID]) AS [Status ID] " +
                  ",lvst.[TH Name] AS [Display Status Name] " +
              "FROM [LV Request] head WITH(READUNCOMMITTED) " +
              "INNER JOIN [LV Leave] line WITH(READUNCOMMITTED) ON head.[Request ID]=line.[Request ID] " +
              "INNER JOIN [LV Status] AS lvst WITH(READUNCOMMITTED) ON line.[Status ID]=lvst.[Status ID] " +
              "INNER JOIN [HR Person] person WITH(READUNCOMMITTED) ON head.[Person No]=person.[Person No] " +
              "INNER JOIN [HR Employee] AS emp WITH(READUNCOMMITTED) ON person.[Person No]=emp.[Person No] " +
              "WHERE 1=1 " + WhereLeaveID + WhereCurrentRequestID + WherePersonNo + WhereLeaveDates + WhereHeadPersonNo + WhereTypeSubID +
              "ORDER BY " + OrderBy;

            Leave obj = new Leave(User, null);
            List<LeaveRecord> list = obj.QueryLines(Query, OrderBy, Params, Page, PageSize, GetOnlyFirstRecord, GetOnlyLastRecord);

            string QueryStatusName = null;
            // Update Veto status name
            foreach (LeaveRecord line in list)
            {
                if (line.StatusID == Const.STATUS_LEAVE_INTERRUPTED)
                {
                    if (QueryStatusName == null)
                        QueryStatusName = obj.ExecuteScalar<string>("SELECT [TH Name] FROM [LV Status] WHERE [Status ID]=" + Const.STATUS_LEAVE_INTERRUPTED, null);
                    line.DisplayStatusName = QueryStatusName;
                }
				if (line.TotalHours == 0)
					line.DisplayStatusName = StatusLeaveZero;
            }

            bool multiStatuses = false;
            int prevStatusID = -1;
            foreach (LeaveRecord line in list)
            {
                if (prevStatusID != -1 && prevStatusID != line.StatusID)
                    multiStatuses = true;
                prevStatusID = line.StatusID;
            }

            QueryStatusName = null;
            if (multiStatuses)
            {
                foreach (LeaveRecord rec in list)
				{
                    rec.LeaveRequested.StatusID = Const.STATUS_MULTIPLE_STATUSES;
                    if (QueryStatusName == null)
                        QueryStatusName = obj.ExecuteScalar<string>("SELECT [TH Name] FROM [LV Status] WHERE [Status ID]=" + Const.STATUS_MULTIPLE_STATUSES, null);
                    rec.LeaveRequested.DisplayStatusName = QueryStatusName;
				}
            }
            return list;
        }

        /// <summary>
        /// ดึงรายการจาก database โดยใช้เงื่อนไขต่างๆตาม Parameters
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="RequestID">Request ID หรือ 0 ถ้าไม่ระบุ</param>
        /// <param name="LeaveID">Leave ID หรือ 0 หากไม่ระบุ</param>
        /// <param name="CurrentRequestIDCompare">ใช้คู่กับ CurrentRequestID เป็นเครื่องหมายเปรียบเทียบ (&lt;,&gt; ฯลฯ) หรือ null หากไม่ต้องการระบุ</param>
        /// <param name="CurrentRequestID">เลขที่ใบลาที่ใช้เป็นฐานในการค้นหาใบลาถัดไป หรือ 0 หากไม่ระบุ</param>
        /// <param name="PersonNo">รหัสพนักงาน หรือ null หากไม่ระบุ</param>
        /// <param name="Begin">วันที่ใบลาสำหรับ "ตั้งแต่" หรือ null หากไม่ระบุ</param>
        /// <param name="Until">วันที่ใบลาสำหรับ "จนถึง" หรือ null หากไม่ระบุ</param>
        /// <param name="HeadPersonNo">รหัสหัวหน้า หรือ null หากไม่ระบุ</param>
        /// <param name="TypeSubID">รหัสประเภทการลา หรือ 0 หากไม่ระบุ</param>
        /// <param name="Page">เริ่มจาก 1</param>
        /// <param name="PageSize">เริ่มจาก 1 หรือ 0 หากไม่ต้องการระบุจำนวน</param>
        /// <param name="GetOnlyFirstRecord">true ถ้าต้องการแค่ 1 รายการแรกเพื่อประหยัด Memory</param>
        /// <param name="GetOnlyLastRecord">true ถ้าต้องการแค่ 1 รายการสุดท้ายเพื่อประหยัด Memory</param>
        /// <returns></returns>
        protected static List<RequestRecord> QueryHeaders(IPrincipal User, Int64 RequestID, Int64 LeaveID, string CurrentRequestIDCompare
            , Int64 CurrentRequestID, string PersonNo, DateTime? Begin, DateTime? Until, string HeadPersonNo, int TypeSubID
            , int Page, int PageSize, bool GetOnlyFirstRecord, bool GetOnlyLastRecord)
        {
            List<SqlParameter> Params = new List<SqlParameter>(2);
            Params.Add(new SqlParameter("@headid", SqlDbType.BigInt));
            Params[0].Value = RequestID;
            Params.Add(new SqlParameter("@lineid", SqlDbType.BigInt));
            Params[1].Value = LeaveID;
            Params.Add(new SqlParameter("@perno", SqlDbType.VarChar));
            Params[2].Value = string.IsNullOrEmpty(PersonNo) ? (object)DBNull.Value : PersonNo;
            Params.Add(new SqlParameter("@begin", SqlDbType.DateTime));
            Params[3].Value = Begin.HasValue ? Begin : (object)DBNull.Value;
            Params.Add(new SqlParameter("@until", SqlDbType.DateTime));
            Params[4].Value = Until.HasValue ? Until : (object)DBNull.Value;

            string WhereCurrentRequestID = CurrentRequestID != 0 ? string.Format(" AND head.[Request ID]{0}{1} ", CurrentRequestIDCompare, CurrentRequestID) : "";
            string WherePersonNo = string.IsNullOrEmpty(PersonNo) ? "" : " AND head.[Person No]=@perno ";

            string WhereLeaveID = "";
            if (LeaveID != 0) WhereLeaveID = "AND line.[Leave ID]=@lineid ";
            else if (RequestID != 0) WhereLeaveID = "AND head.[Request ID]=@headid ";

            string WhereLeaveDates = "";
            if (Begin.HasValue && Until.HasValue) WhereLeaveDates = "AND line.[Leave Date] BETWEEN @begin AND @until ";
            else if (Begin.HasValue) WhereLeaveDates = "AND line.[Leave Date]>=@begin ";
            else if (Until.HasValue) WhereLeaveDates = "AND line.[Leave Date]<=@until ";

            string WhereTypeSubID = TypeSubID > 0 ? string.Format("AND head.[Type Sub ID]={0} ", TypeSubID) : "";

            List<SqlParameter> Params2;
            string WhereHeadPersonNo = "";
            if (!string.IsNullOrEmpty(HeadPersonNo))
            {
                WhereHeadPersonNo = "AND line.[Total Hours] > 0 " + WhereApprovingBy("AND", HeadPersonNo, "head", "line", out Params2);
                Params.AddRange(Params2);
            }

            Leave obj = new Leave(User, null);
            string MultipleStatusName = obj.ExecuteScalar<string>("SELECT [TH Name] FROM [LV Status] WHERE [Status ID]=" + Const.STATUS_MULTIPLE_STATUSES, null);
            Params.Add(new SqlParameter("@multistsname", SqlDbType.VarChar));
            Params[Params.Count - 1].Value = MultipleStatusName;


            string OrderBy = "head.[Request ID]";
            //string Query =
            //      "SELECT {0},head.[Request ID],head.[Type Sub ID],head.[Type Sub Name],head.[Person No],head.[Reason], "
            //    + "             head.[Since],head.[Until],head.[Contact],head.[Apply Date],head.[Apply By HR], "
            //    + "				person.[TH Prefix],person.[TH First Name],person.[TH Last Name],atta.[Real Path],atta.[Virtual Path], "
            //    + "				person.[EN First Name],person.[EN Last Name],person.[EN Prefix],person.[Mobile No],person.[E-Mail], "
            //    + "				emp.[Employee No],emp.[Starting Date],emp.[Until Date],emp.[Company Code],emp.Department,emp.Section, "
            //    + "				CASE WHEN COUNT(DISTINCT line.[Status ID])=1 THEN MIN(line.[Status ID]) ELSE " + Const.STATUS_MULTIPLE_STATUSES + " END AS [Status ID], "
            //    + "				CASE WHEN COUNT(DISTINCT line.[Status ID])=1 THEN MIN(lvst.[TH Name]) ELSE @multistsname END AS [Display Status Name], "
            //    + "				CONVERT(DECIMAL(8,3), SUM(ISNULL(line.[Total Hours], 0))) AS [Total Leave Hours], "
            //    + "				CONVERT(DECIMAL(8,3), SUM(ISNULL(line.[Approve Days], 0))) AS [Total Approve Days] "
            //    + "FROM			[LV Request] AS head WITH(READUNCOMMITTED) "
            //    + "				INNER JOIN [LV Leave] AS line WITH(READUNCOMMITTED) ON head.[Request ID]=line.[Request ID] "
            //    + "				INNER JOIN [LV Status] AS lvst WITH(READUNCOMMITTED) ON line.[Status ID]=lvst.[Status ID] "
            //    + "				INNER JOIN [HR Person] AS person WITH(READUNCOMMITTED) ON head.[Person No]=person.[Person No] "
            //    + "				    LEFT OUTER JOIN [HR Employee] AS emp WITH(READUNCOMMITTED) INNER JOIN ("
            //    + "                     SELECT [Person No], MAX([Starting Date]) AS [Starting Date] "
            //    + "                     FROM [HR Employee] WITH(READUNCOMMITTED) WHERE [Person No] = ISNULL(@perno, [Person No]) "
            //    + "                     GROUP BY [Person No] "
            //    + "                 ) latest ON emp.[Person No]=latest.[Person No] AND emp.[Starting Date]=latest.[Starting Date] "
            //    + "             ON person.[Person No] = emp.[Person No] "
            //    + "             LEFT JOIN [LV File] AS atta WITH(READUNCOMMITTED) ON head.[Request ID]=atta.[Request ID] "
            //    + "WHERE 1=1 " + WhereLeaveID + WhereCurrentRequestID + WherePersonNo + WhereLeaveDates + WhereHeadPersonNo + WhereTypeSubID
            //    + "GROUP BY		head.[Request ID],head.[Type Sub ID],head.[Type Sub Name],head.[Person No],head.[Reason], "
            //    + "				head.[Since],head.[Until],head.[Contact],head.[Apply Date],head.[Apply By HR], "
            //    + "             person.[TH Prefix], person.[TH First Name], person.[TH Last Name],atta.[Real Path],atta.[Virtual Path], "
            //    + "             person.[EN First Name],person.[EN Last Name],person.[EN Prefix],person.[Mobile No],person.[E-Mail], "
            //    + "				emp.[Employee No],emp.[Starting Date],emp.[Until Date],emp.[Company Code],emp.Department,emp.Section "
            //    + "ORDER BY " + OrderBy;

            // แสดงสถานะ Veto ด้วย
            string Query =
                  "SELECT {0},head.[Request ID],head.[Type Sub ID],head.[Type Sub Name],head.[Person No],head.[Reason], "
                + "             head.[Since],head.[Until],head.[Contact],head.[Apply Date],head.[Apply By HR], "
                + "				person.[TH Prefix],person.[TH First Name],person.[TH Last Name],atta.[Real Path],atta.[Virtual Path], "
                + "				person.[EN First Name],person.[EN Last Name],person.[EN Prefix],person.[Mobile No],person.[E-Mail], "
                + "				emp.[Employee No],emp.[Starting Date],emp.[Until Date],emp.[Company Code],emp.Department,emp.Section, "
                + "				emp.[TH Position],emp.[EN Position], "
                + "				CASE WHEN COUNT(DISTINCT line.[Status ID])=1 "
                + "             THEN ISNULL((SELECT TOP 1 " + Const.STATUS_LEAVE_INTERRUPTED + " FROM [LV Veto] WHERE [Leave ID]=MIN(line.[Leave ID]) AND [Action Status]=" + Const.VETO_INTERRUPTED + "),MIN(line.[Status ID])) "
                + "             ELSE " + Const.STATUS_MULTIPLE_STATUSES + " END AS [Status ID], "
                + "				CASE WHEN COUNT(DISTINCT line.[Status ID])=1 THEN MIN(lvst.[TH Name]) ELSE @multistsname END AS [Display Status Name], "
				//+ "				CASE WHEN (SELECT COUNT(DISTINCT (CASE WHEN b.[Action Status]=" + Const.VETO_INTERRUPTED + " THEN " + Const.STATUS_LEAVE_INTERRUPTED + " ELSE a.[Status ID] END)) FROM [LV Leave] AS a LEFT OUTER JOIN [LV Veto] AS b ON a.[Leave ID]=b.[Leave ID] WHERE a.[Request ID]=head.[Request ID] GROUP BY a.[Request ID]) = 1 THEN ISNULL((SELECT TOP 1 " + Const.STATUS_LEAVE_INTERRUPTED + " FROM [LV Veto] WHERE [Leave ID]=MIN(line.[Leave ID]) AND [Action Status]=" + Const.VETO_INTERRUPTED + "),MIN(line.[Status ID])) ELSE " + Const.STATUS_MULTIPLE_STATUSES + " END AS [Status ID], "
				//+ "				CASE WHEN (SELECT COUNT(DISTINCT (CASE WHEN b.[Action Status]=" + Const.VETO_INTERRUPTED + " THEN " + Const.STATUS_LEAVE_INTERRUPTED +" ELSE a.[Status ID] END)) FROM [LV Leave] AS a LEFT OUTER JOIN [LV Veto] AS b ON a.[Leave ID]=b.[Leave ID] WHERE a.[Request ID]=head.[Request ID] GROUP BY a.[Request ID]) = 1 THEN MIN(lvst.[TH Name]) ELSE @multistsname END AS [Display Status Name], "
                + "				SUM(ISNULL(line.[Total Hours], 0)) AS [Total Leave Hours], "
                + "				SUM(ISNULL(line.[Approve Minutes], 0)) AS [Total Approve Minutes] "
                + "FROM			[LV Request] AS head WITH(READUNCOMMITTED) "
                + "				INNER JOIN [LV Leave] AS line WITH(READUNCOMMITTED) ON head.[Request ID]=line.[Request ID] "
                + "				INNER JOIN [LV Status] AS lvst WITH(READUNCOMMITTED) ON line.[Status ID]=lvst.[Status ID] "
                + "				INNER JOIN [HR Person] AS person WITH(READUNCOMMITTED) ON head.[Person No]=person.[Person No] "
                + "				    LEFT OUTER JOIN [HR Employee] AS emp WITH(READUNCOMMITTED) INNER JOIN ("
                + "                     SELECT [Person No], MAX([Starting Date]) AS [Starting Date] "
                + "                     FROM [HR Employee] WITH(READUNCOMMITTED) WHERE [Person No] = ISNULL(@perno, [Person No]) "
                + "                     GROUP BY [Person No] "
                + "                 ) latest ON emp.[Person No]=latest.[Person No] AND emp.[Starting Date]=latest.[Starting Date] "
                + "             ON person.[Person No] = emp.[Person No] "
                + "             LEFT JOIN [LV File] AS atta WITH(READUNCOMMITTED) ON head.[Request ID]=atta.[Request ID] "
                + "WHERE 1=1 " + WhereLeaveID + WhereCurrentRequestID + WherePersonNo + WhereLeaveDates + WhereHeadPersonNo + WhereTypeSubID
                + "GROUP BY		head.[Request ID],head.[Type Sub ID],head.[Type Sub Name],head.[Person No],head.[Reason], "
                + "				head.[Since],head.[Until],head.[Contact],head.[Apply Date],head.[Apply By HR], "
                + "             person.[TH Prefix], person.[TH First Name], person.[TH Last Name],atta.[Real Path],atta.[Virtual Path], "
                + "             person.[EN First Name],person.[EN Last Name],person.[EN Prefix],person.[Mobile No],person.[E-Mail], "
                + "				emp.[Employee No],emp.[Starting Date],emp.[Until Date],emp.[Company Code],emp.Department,emp.Section, "
                + "				emp.[TH Position],emp.[EN Position] "
                + "ORDER BY " + OrderBy;
            List<RequestRecord> list = obj.QueryHeaders(Query, OrderBy, Params, Page, PageSize, GetOnlyFirstRecord, GetOnlyLastRecord);

            // Update Veto status name
            string InterruptStatusName = obj.ExecuteScalar<string>("SELECT [TH Name] FROM [LV Status] WHERE [Status ID]=" + Const.STATUS_LEAVE_INTERRUPTED, null);
			foreach (var rec in list)
			{
				if (rec.StatusID == Const.STATUS_LEAVE_INTERRUPTED)
					rec.DisplayStatusName = InterruptStatusName;
				if (rec.TotalLeaveHours == 0)
					rec.DisplayStatusName = StatusLeaveZero;
			}

            return list;
        }

        /// <summary>
        /// ดึงรายการจาก database เพื่อเป็นรายการให้เลือกอนุมัติผ่านหน้าเมลล์
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="HeadPersonNo">รหัสหัวหน้า</param>
        /// <param name="Page">เลขหน้าเริ่มจาก 1</param>
        /// <param name="PageSize">จำนวนรายการเริ่มจาก 1 หากเป็น 0 จะดึงรายการทั้งหมด</param>
        /// <returns>List&lt;LeaveRecord&gt; ที่ไม่ใช่ null</returns>
        //public static List<LeaveRecord> ListApprove(IPrincipal User, string HeadPersonNo)
        //{
        //    return QueryLines(User, 0, 0, null, 0, null, null, null, HeadPersonNo, 0, 1, 0, false, false);
        //}

        /// <summary>
        /// ดึงรายการจาก database เพื่อเป็นรายการให้เลือกอนุมัติผ่านหน้าเว็บ
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="HeadPersonNo">รหัสหัวหน้า</param>
        /// <param name="Page">เลขหน้าเริ่มจาก 1</param>
        /// <param name="PageSize">จำนวนรายการเริ่มจาก 1 หากเป็น 0 จะดึงรายการทั้งหมด</param>
        /// <returns>List&lt;LeaveRecord&gt; ที่ไม่ใช่ null</returns>
        public static List<RequestRecord> ListApprove(IPrincipal User, string HeadPersonNo, int Page, int PageSize)
        {
            return QueryHeaders(User, 0, 0, null, 0, null, null, null, HeadPersonNo, 0, Page, PageSize, false, false);
        }

        /// <summary>
        /// ดึงเฉพาะรายการ Line ของใบลาที่อยู่ถัดจากใบลาปัจจุบัน
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="HeadPersonNo">รหัสหัวหน้า</param>
        /// <param name="CurrentRequestID">เลขที่ใบลาที่ใช้เป็นฐานในการค้นหาใบลาถัดไป</param>
        /// <returns>List&lt;LeaveRecord&gt; ที่ไม่ใช่ null</returns>
        public static List<LeaveRecord> ListNextApprove(IPrincipal User, string HeadPersonNo, Int64 CurrentRequestID)
        {
            List<RequestRecord> list = QueryHeaders(User, 0, 0, ">", CurrentRequestID, null, null, null, HeadPersonNo, 0, 1, 1, true, false);
            if (list.Count == 0)
                return new List<LeaveRecord>();
            return QueryLines(User, list[0].RequestID, 0, null, 0, null, null, null, null, 0, 1, 0, false, false);
        }

        /// <summary>
        /// ดึงเฉพาะรายการ Line ของใบลาที่อยู่ก่อนใบลาปัจจุบัน
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="HeadPersonNo">รหัสหัวหน้า</param>
        /// <param name="CurrentRequestID">เลขที่ใบลาที่ใช้เป็นฐานในการค้นหาใบลาถัดไป</param>
        /// <returns>List&lt;LeaveRecord&gt; ที่ไม่ใช่ null</returns>
        public static List<LeaveRecord> ListPrevApprove(IPrincipal User, string HeadPersonNo, Int64 CurrentRequestID)
        {
            List<RequestRecord> list = QueryHeaders(User, 0, 0, "<", CurrentRequestID, null, null, null, HeadPersonNo, 0, 1, 1, false, true);
            if (list.Count == 0)
                return new List<LeaveRecord>();
            return QueryLines(User, list[0].RequestID, 0, null, 0, null, null, null, null, 0, 1, 0, false, false);
        }

        /// <summary>
        /// ดึงรายการจาก database โดยใช้เงื่อนไขของ Request ID
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="RequestID">Request ID หรือ 0 ถ้าไม่ระบุ</param>
        /// <returns>List&lt;RequestRecord&gt; ที่ไม่ใช่ null</returns>
        public static RequestRecord GetHeader(IPrincipal User, Int64 RequestID)
        {
            return QueryHeaders(User, RequestID, 0, null, 0, null, null, null, null, 0, 1, 0, false, false).DefaultIfEmpty(null).FirstOrDefault();
        }

        /// <summary>
        /// ดึงรายการจาก database โดยใช้เงื่อนไขของ Parameters
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="PersonNo">รหัสพนักงาน หรือ null หากไม่ระบุ</param>
        /// <param name="Begin">วันที่ใบลาสำหรับ "ตั้งแต่" หรือ null หากไม่ระบุ</param>
        /// <param name="Until">วันที่ใบลาสำหรับ "จนถึง" หรือ null หากไม่ระบุ</param>
        /// <param name="TypeSubID">รหัสประเภทการลา หรือ 0 หากไม่ระบุ</param>
        /// <param name="Page">เริ่มจาก 1</param>
        /// <param name="PageSize">เริ่มจาก 1 หรือ 0 หากไม่ต้องการระบุจำนวน</param>
        /// <returns>List&lt;RequestRecord&gt; ที่ไม่ใช่ null</returns>
        public static List<RequestRecord> ListHeaders(IPrincipal User, string PersonNo
            , DateTime? Begin, DateTime? Until, int TypeSubID, int Page, int PageSize)
        {
            return QueryHeaders(User, 0, 0, null, 0, PersonNo, Begin, Until, null, TypeSubID, Page, PageSize, false, false);
        }

        /// <summary>
        /// ดึงเฉพาะรายการ Line ของใบลาที่อยู่ถัดจากใบลาปัจจุบัน
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="PersonNo">รหัสพนักงาน หรือ null หากไม่ระบุ</param>
        /// <param name="Begin">วันที่ใบลาสำหรับ "ตั้งแต่" หรือ null หากไม่ระบุ</param>
        /// <param name="Until">วันที่ใบลาสำหรับ "จนถึง" หรือ null หากไม่ระบุ</param>
        /// <param name="TypeSubID">รหัสประเภทการลา หรือ 0 หากไม่ระบุ</param>
        /// <param name="CurrentRequestID">เลขที่ใบลาที่ใช้เป็นฐานในการค้นหาใบลาถัดไป</param>
        /// <returns>List&lt;LeaveRecord&gt; ที่ไม่ใช่ null</returns>
        public static List<LeaveRecord> ListNext(IPrincipal User, string PersonNo
            , DateTime? Begin, DateTime? Until, int TypeSubID, Int64 CurrentRequestID)
        {
            List<RequestRecord> list = QueryHeaders(User, 0, 0, ">", CurrentRequestID, PersonNo, Begin, Until, null, TypeSubID, 1, 1, true, false);
            if (list.Count == 0)
                return new List<LeaveRecord>();
            return QueryLines(User, list[0].RequestID, 0, null, 0, null, null, null, null, 0, 1, 0, false, false);
        }

        /// <summary>
        /// ดึงเฉพาะรายการ Line ของใบลาที่อยู่ก่อนใบลาปัจจุบัน
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="PersonNo">รหัสพนักงาน หรือ null หากไม่ระบุ</param>
        /// <param name="Begin">วันที่ใบลาสำหรับ "ตั้งแต่" หรือ null หากไม่ระบุ</param>
        /// <param name="Until">วันที่ใบลาสำหรับ "จนถึง" หรือ null หากไม่ระบุ</param>
        /// <param name="TypeSubID">รหัสประเภทการลา หรือ 0 หากไม่ระบุ</param>
        /// <param name="CurrentRequestID">เลขที่ใบลาที่ใช้เป็นฐานในการค้นหาใบลาถัดไป</param>
        /// <returns>List&lt;LeaveRecord&gt; ที่ไม่ใช่ null</returns>
        public static List<LeaveRecord> ListPrev(IPrincipal User, string PersonNo
            , DateTime? Begin, DateTime? Until, int TypeSubID, Int64 CurrentRequestID)
        {
            List<RequestRecord> list = QueryHeaders(User, 0, 0, "<", CurrentRequestID, PersonNo, Begin, Until, null, TypeSubID, 1, 1, false, true);
            if (list.Count == 0)
                return new List<LeaveRecord>();
            return QueryLines(User, list[0].RequestID, 0, null, 0, null, null, null, null, 0, 1, 0, false, false);
        }

        /// <summary>
        /// ดึงรายการจาก database โดยใช้เงื่อนไขของ Parameters
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="PersonNo">รหัสพนักงาน หรือ null หากไม่ระบุ</param>
        /// <param name="Page">เริ่มจาก 1</param>
        /// <param name="PageSize">เริ่มจาก 1 หรือ 0 หากไม่ต้องการระบุจำนวน</param>
        /// <returns>List&lt;RequestRecord&gt; ที่ไม่ใช่ null</returns>
        public static List<RequestRecord> ListRecents(IPrincipal User, string PersonNo, int Page, int PageSize)
        {
            DateTime Today = DateTime.Now.Date;
            DateTime PrevMonth = Today.AddMonths(-1);
            DateTime NextMonth = Today.AddMonths(2);
            DateTime Begin = new DateTime(PrevMonth.Year, PrevMonth.Month, 1);
            //DateTime Until = new DateTime(NextMonth.Year, NextMonth.Month, 1).AddDays(-1);
            return QueryHeaders(User, 0, 0, null, 0, PersonNo, Begin, null, null, 0, Page, PageSize, false, false);
        }

        /// <summary>
        /// ดึงเฉพาะรายการ Line ของใบลาที่อยู่ถัดจากใบลาปัจจุบัน
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="PersonNo">รหัสพนักงาน หรือ null หากไม่ระบุ</param>
        /// <param name="CurrentRequestID">เลขที่ใบลาที่ใช้เป็นฐานในการค้นหาใบลาถัดไป</param>
        /// <returns>List&lt;LeaveRecord&gt; ที่ไม่ใช่ null</returns>
        public static List<LeaveRecord> ListNextRecent(IPrincipal User, string PersonNo, Int64 CurrentRequestID)
        {
            DateTime Today = DateTime.Now.Date;
            DateTime PrevMonth = Today.AddMonths(-1);
            DateTime NextMonth = Today.AddMonths(2);
            DateTime Begin = new DateTime(PrevMonth.Year, PrevMonth.Month, 1);
            //DateTime Until = new DateTime(NextMonth.Year, NextMonth.Month, 1).AddDays(-1);
            List<RequestRecord> list = QueryHeaders(User, 0, 0, ">", CurrentRequestID, PersonNo, Begin, null, null, 0, 1, 1, true, false);
            if (list.Count == 0)
                return new List<LeaveRecord>();
            return QueryLines(User, list[0].RequestID, 0, null, 0, null, null, null, null, 0, 1, 0, false, false);
        }

        /// <summary>
        /// ดึงเฉพาะรายการ Line ของใบลาที่อยู่ก่อนใบลาปัจจุบัน
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="PersonNo">รหัสพนักงาน หรือ null หากไม่ระบุ</param>
        /// <param name="CurrentRequestID">เลขที่ใบลาที่ใช้เป็นฐานในการค้นหาใบลาถัดไป</param>
        /// <returns>List&lt;LeaveRecord&gt; ที่ไม่ใช่ null</returns>
        public static List<LeaveRecord> ListPrevRecent(IPrincipal User, string PersonNo, Int64 CurrentRequestID)
        {
            DateTime Today = DateTime.Now.Date;
            DateTime PrevMonth = Today.AddMonths(-1);
            DateTime NextMonth = Today.AddMonths(2);
            DateTime Begin = new DateTime(PrevMonth.Year, PrevMonth.Month, 1);
            //DateTime Until = new DateTime(NextMonth.Year, NextMonth.Month, 1).AddDays(-1);
            List<RequestRecord> list = QueryHeaders(User, 0, 0, "<", CurrentRequestID, PersonNo, Begin, null, null, 0, 1, 1, false, true);
            if (list.Count == 0)
                return new List<LeaveRecord>();
            return QueryLines(User, list[0].RequestID, 0, null, 0, null, null, null, null, 0, 1, 0, false, false);
        }

        /// <summary>
        /// ดึงเฉพาะรายการ Line ของพนักงานในช่วงวันที่ระบุ
        /// </summary>
        /// <param name="User">Current login user เช่น Context.User</param>
        /// <param name="PersonNo">รหัสพนักงาน หรือ null หากไม่ระบุ</param>
        /// <param name="Begin">วันที่เริ่มค้นใบลา</param>
        /// <param name="Until">วันที่ค้นถึง</param>
        /// <returns>List&lt;LeaveRecord&gt; ที่ไม่ใช่ null</returns>
        public static List<RequestRecord> ListLeaveDaysOf(IPrincipal User, string PersonNo, DateTime Begin, DateTime Until)
        {
            return QueryHeaders(User, 0, 0, null, 0, PersonNo, Begin, Until, null, 0, 1, 0, false, false);
            //return QueryLines(User, 0, 0, null, 0, PersonNo, Begin, Until, null, 0, 1, 0, false, false);
        }

        /// <summary>
        /// สร้างเงื่อนไข WHERE เพื่อดึงรายการที่รออนุมัติ เรียกใช้โดย
        /// - รายการหน้าเว็บผู้อนุมัติ
        /// - รายการที่ส่งในเมลล์
        /// </summary>
        /// <param name="PreOperator">Operator ตัวแรกสำหรับเชื่อม Statement เช่น WHERE หรือ AND หรือ OR</param>
        /// <param name="HeadPersonNo"></param>
        /// <param name="headAlias"></param>
        /// <param name="lineAlias"></param>
        /// <param name="Params"></param>
        /// <returns></returns>
        protected static string WhereApprovingBy(string PreOperator, string HeadPersonNo, string headAlias, string lineAlias, out List<SqlParameter> Params)
        {
            Params = new List<SqlParameter>(1);
            Params.Add(new SqlParameter("@headno", SqlDbType.VarChar));
            Params[0].Value = HeadPersonNo;

            //string where = PreOperator + string.Format(
            //    // ใบลาที่ไม่ถูกระงับ
            //  " NOT EXISTS(SELECT 1 FROM [LV Veto] WHERE [Leave ID]={1}.[Leave ID] AND [Action Status]={2}) "
            //    // ใบลาที่ผู้อนุมัติเป็นหัวหน้าคนนั้น (เฉพาะใบลาที่รออนุมัติเท่านั้น)
            //+ "AND EXISTS("
            //    + "SELECT 1 FROM [LV Grant] g WHERE g.[Leave ID]={1}.[Leave ID] AND g.[Head Person No]=@headno AND ("
            //    // ที่อยู่สถานะ "Pending", ที่อยู่ลำดับที่ 1 หรือ ลำดับที่ 2 (แต่ลำดับที่ 1 ต้องอนุมัติแล้ว)
            //    + "   (g.Priority=1 AND g.[Status ID]={4} AND g.[Grant Step ID]={3}) "
            //    + "OR (g.Priority=2 AND g.[Status ID]={4} AND g.[Grant Step ID]={3} AND EXISTS(SELECT 1 FROM [LV Grant] WHERE [Leave ID]=g.[Leave ID] AND Priority=1 AND [Status ID]={5})) "
            //    // ที่อยู่สถานะ "Waiting", ที่อยู่ลำดับที่ 1 หรือ ลำดับที่ 2 (แต่ลำดับที่ 1 ต้องอนุมัติแล้ว)
            //    + "OR (g.Priority=1 AND g.[Status ID]={8} AND g.[Grant Step ID]={3}) "
            //    + "OR (g.Priority=2 AND g.[Status ID]={8} AND g.[Grant Step ID]={3} AND EXISTS(SELECT 1 FROM [LV Grant] WHERE [Leave ID]=g.[Leave ID] AND Priority=1 AND [Status ID]={5})) "
            //    // ที่อยู่สถานะ "Cancel Request", ที่อยู่ลำดับที่ 1 หรือ ลำดับที่ 2 (แต่ลำดับที่ 1 ต้องอนุมัติแล้ว)
            //    + "OR (g.Priority=1 AND ISNULL(g.[Cancel Status ID],0)={6} AND ISNULL(g.[Cancel Grant Step ID],{3})={3}) "
            //    + "OR (g.Priority=2 AND ISNULL(g.[Cancel Status ID],0)={6} AND ISNULL(g.[Cancel Grant Step ID],{3})={3} AND EXISTS(SELECT 1 FROM [LV Grant] WHERE [Leave ID]=g.[Leave ID] AND Priority=1 AND [Cancel Status ID]={7})) "
            //    + ")"
            //+ ") "
            //, headAlias, lineAlias
            //, /*{2}*/Const.VETO_INTERRUPTED
            //, /*{3}*/Const.STATUS_GRANTSTEP_PENDING_APPROVAL
            //, /*{4}*/Const.STATUS_LEAVE_PENDING_APPROVAL
            //, /*{5}*/Const.STATUS_LEAVE_APPROVED
            //, /*{6}*/Const.STATUS_LEAVE_CANCELREQUEST
            //, /*{7}*/Const.STATUS_LEAVE_CANCELAPPROVED
            //, /*{8}*/Const.STATUS_LEAVE_AWAITING);

            string where = PreOperator + string.Format(
                // ใบลาที่ไม่ถูกระงับ
              " NOT EXISTS(SELECT 1 FROM [LV Veto] WHERE [Leave ID]={1}.[Leave ID] AND [Action Status]={2}) "
                // ใบลาที่ไม่ถูกยกเลิก
			+ "AND {1}.[Status ID] NOT IN ({8}) "
				// ใบลาที่ผู้อนุมัติเป็นหัวหน้าคนนั้น (เฉพาะใบลาที่รออนุมัติเท่านั้น)
            + "AND EXISTS("
                + "SELECT 1 FROM [LV Grant] g WHERE g.[Leave ID]={1}.[Leave ID] AND g.[Head Person No]=@headno AND "
                // ที่อยู่สถานะ "Pending", "Waiting" และรออนุมัติอยู่ โดยไม่สนลำดับการอนุมัติ
                + "  (g.[Status ID] IN ({3},{5}) AND g.[Grant Step ID]={2} "
                // ที่อยู่สถานะ "Cancel Request" และรออนุมัติอยู่ โดยไม่สนลำดับการอนุมัติ
                + "OR g.[Status ID]={7} AND g.[Grant Step ID]={6} AND g.[Cancel Status ID]={4} AND g.[Cancel Grant Step ID]={2})"
            + ") "
            , headAlias, lineAlias
            , /*{2}*/Const.STATUS_GRANTSTEP_PENDING_APPROVAL
            , /*{3}*/Const.STATUS_LEAVE_PENDING_APPROVAL
            , /*{4}*/Const.STATUS_LEAVE_CANCELREQUEST
            , /*{5}*/Const.STATUS_LEAVE_AWAITING
            , /*{6}*/Const.STATUS_GRANTSTEP_APPROVED
            , /*{7}*/Const.STATUS_LEAVE_APPROVED
            , /*{8}*/Const.STATUS_LEAVE_CANCELLED);
            
            return where;
        }

        /// <summary>
        /// Generate security token สำหรับระบบความปลอดภัย
        /// </summary>
        /// <param name="HeadPersonNo"></param>
        /// <param name="RequestID"></param>
        /// <returns></returns>
        public static string DigestVeto(string HeadPersonNo, Int64 RequestID)
        {
            return Tool.CreateHash(HeadPersonNo + RequestID + SaltTextVeto);
        }

        /// <summary>
        /// Generate security token สำหรับระบบความปลอดภัย
        /// </summary>
        /// <param name="HeadPersonNo"></param>
        /// <param name="RequestID"></param>
        /// <returns></returns>
        public static string DigestGrant(string HeadPersonNo, Int64 RequestID)
        {
            return Tool.CreateHash(HeadPersonNo + RequestID + SaltTextGrant);
        }

        /// <summary>
        /// Get รายชื่อ Veto Person ของใบลาที่ระบุ
        /// </summary>
        /// <param name="User"></param>
        /// <param name="RequestID">null หากระบุ LeaveID</param>
        /// <param name="LeaveID">null หากระบุ RequestID</param>
        /// <returns></returns>
        public static List<VetoRecord> GetVetoes(IPrincipal User, Int64? RequestID, Int64? LeaveID)
        {
            Leave obj = new Leave(User, null);
            return obj.GetVetoes(RequestID, LeaveID);
        }

        /// <summary>
        /// Get รายชื่อ Grantor Person ของใบลาที่ระบุ
        /// </summary>
        /// <param name="User"></param>
        /// <param name="RequestID">null หากระบุ LeaveID</param>
        /// <returns></returns>
        public static List<GrantRecord> GetApprovals(IPrincipal User, Int64 LeaveID)
        {
            Leave obj = new Leave(User, null);
            return obj.GetApprovals(LeaveID);
        }

        /// <summary>
        /// หา Grant Record ล่าสุดที่เป็นของ Head ที่ระบุ
        /// </summary>
        /// <param name="User"></param>
        /// <param name="gs"></param>
        /// <param name="HeadPersonNo"></param>
        /// <returns></returns>
        public static GrantRecord CurrentApproval(IPrincipal User, List<GrantRecord> gs, string HeadPersonNo)
        {
            Leave obj = new Leave(User, null);
            return obj.CurrentApproval(gs, HeadPersonNo);
        }

        /// <summary>
        /// คำนวนหาจำนวนนาทีที่ลาจริงๆที่ลาในวันนั้น
        /// </summary>
        /// <param name="WorkshiftMorningIn"></param>
        /// <param name="WorkshiftMorningOut"></param>
        /// <param name="WorkshiftAfternoonIn"></param>
        /// <param name="WorkshiftAfternoonOut"></param>
        /// <param name="DaySince"></param>
        /// <param name="DayUntil"></param>
        /// <returns></returns>
        public static double CalcTotalLeaveMinutes(DateTime? WorkshiftMorningIn, DateTime? WorkshiftMorningOut,
            DateTime? WorkshiftAfternoonIn, DateTime? WorkshiftAfternoonOut,
            ref DateTime DaySince, ref DateTime DayUntil, ref string Error)
        {
            if (DaySince.Date != DayUntil.Date)
                Error = "ระบบไม่สามารถคำนวนจำนวนชั่วโมงการลาได้ มีข้อผิดพลาดภายในระบบ";

            bool HasLunchTime = false;
            bool HasWorkShift = WorkshiftMorningIn.HasValue && WorkshiftMorningOut.HasValue &&
                WorkshiftAfternoonIn.HasValue && WorkshiftAfternoonOut.HasValue;
            double Minutes = DayUntil.Subtract(DaySince).TotalMinutes;
            if (HasWorkShift)
            {
                if (WorkshiftMorningOut.Value.TimeOfDay <= DaySince.TimeOfDay && DaySince.TimeOfDay < WorkshiftAfternoonIn.Value.TimeOfDay)
                {
                    Error = string.Format("กรุณาระบุ \"เริ่มตั้งแต่เวลา\" นอกช่วงพักเที่ยง {0} - {1}"
                        , WorkshiftMorningOut.Value.TimeOfDay.ToString(@"hh\:mm")
                        , WorkshiftAfternoonIn.Value.TimeOfDay.ToString(@"hh\:mm"));
                    return Minutes;
                }
                if (WorkshiftMorningOut.Value.TimeOfDay < DayUntil.TimeOfDay && DayUntil.TimeOfDay <= WorkshiftAfternoonIn.Value.TimeOfDay)
                {
                    Error = string.Format("กรุณาระบุ \"จนถึงเวลา\" นอกช่วงพักเที่ยง {0} - {1}"
                        , WorkshiftMorningOut.Value.TimeOfDay.ToString(@"hh\:mm")
                        , WorkshiftAfternoonIn.Value.TimeOfDay.ToString(@"hh\:mm"));
                    return Minutes;
                }
                // ถ้าเริ่มลาในช่วงเช้าและสิ้นสุดลาในช่วงบ่าย การคำนวนจะตัดพักเที่ยงออก
                if (WorkshiftMorningIn.Value.TimeOfDay <= DaySince.TimeOfDay && DaySince.TimeOfDay <= WorkshiftMorningOut.Value.TimeOfDay &&
                    WorkshiftAfternoonIn.Value.TimeOfDay <= DayUntil.TimeOfDay && DayUntil.TimeOfDay <= WorkshiftAfternoonOut.Value.TimeOfDay)
                {
                    HasLunchTime = true;
                    Minutes = DayUntil.TimeOfDay.Subtract(DaySince.TimeOfDay).TotalMinutes
                        - WorkshiftAfternoonIn.Value.TimeOfDay.Subtract(WorkshiftMorningOut.Value.TimeOfDay).TotalMinutes;
                }
            }
            // ลาแต่ละครั้ง ต่ำสุดครั้งละ 30 นาที และปัดขึ้นลงครั้งละ 3 นาที (ตามระเบียบบริษัทฯ)
            const double MinMinutes = 30d;
            const double UnitMinutes = 3d;

            if (Minutes < MinMinutes)
            {
                Error = "ลาได้อย่างน้อย 30 นาที/1 ใบลา และเพิ่ม/ลดได้ทีละ 3 นาที";
                return Minutes;
            }

            double Mod = Minutes % UnitMinutes;
            int Devide = Convert.ToInt32(Math.Round(Minutes / UnitMinutes));

            double CalcMinutes = Minutes;
            if (Mod != 0d)
            {
                // ปัดขึ้น ปัดลง เช่น ลา 44 นาที ... 43/3 = 14.3 ปัดลงเป็น 14 ... 14x3 = 42 นาที
                // หรือ          ลา 34 นาที ... 35/3 = 11.67 ปัดขึ้นเป็น 12 ... 12x3 = 36 นาที เป็นต้น
                CalcMinutes = (UnitMinutes * Devide);
                DateTime NewDayUntil = DaySince.AddMinutes(CalcMinutes);
                if (HasWorkShift && HasLunchTime)
                {
                    NewDayUntil = DaySince.AddMinutes(CalcMinutes
                        + WorkshiftAfternoonIn.Value.TimeOfDay.Subtract(WorkshiftMorningOut.Value.TimeOfDay).TotalMinutes);
                }
                if (HasWorkShift && NewDayUntil.TimeOfDay > WorkshiftAfternoonOut.Value.TimeOfDay)
                {
                    CalcMinutes -= NewDayUntil.TimeOfDay.Subtract(WorkshiftAfternoonOut.Value.TimeOfDay).TotalMinutes;
                    DayUntil = new DateTime(
                        NewDayUntil.Year, NewDayUntil.Month, NewDayUntil.Day,
                        WorkshiftAfternoonOut.Value.Hour, WorkshiftAfternoonOut.Value.Minute, 0);
                }
                else
                {
                    DayUntil = NewDayUntil;
                }
            }
            return CalcMinutes;
        }
        #endregion
    }

}