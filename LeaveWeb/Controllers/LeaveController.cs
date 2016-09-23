using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Threading;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Web.Script.Serialization;
using System.Security;
using System.Security.Principal;
using LeaveCore;
using Leave.Common;
using Newtonsoft.Json.Linq;
using LeaveCore.Email.Services;
using System.Net.Mail;
using System.Drawing;

namespace Leave.Controllers
{
    public class LeaveController : Controller
    {
        //
        // GET: /Leave/

		public LeaveController()
		{
		}

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var pb = filterContext.Controller.ViewBag.Postback;
            if (pb == null || !(pb is Leave.Models.FormPostbackData))
                filterContext.Controller.ViewBag.Postback = new Leave.Models.FormPostbackData();
            base.OnActionExecuting(filterContext);
        }

        public ActionResult Index()
        {
			if (User.Identity.IsAuthenticated)
			{
				LoginIdentity user = (LoginIdentity)User.Identity;
                if (user.Roles.Any(x => x.Contains(Const.ROLE_ASSTHR)))
                {
                    return RedirectToAction("Profiles", "Setting");
                }
                var person = Person.GetInfo(User, user.PersonNo, null);
                ViewBag.WorkBeginString = person != null && person.Employee != null &&
                    person.Employee.StartingDate.HasValue && person.Employee.StartingDate.Value != DateTime.MinValue.Date
                    ? person.Employee.StartingDate.Value.ToString("dd/MM/yyyy") : "-";
				ViewBag.PersonNo = user.Name;
				ViewBag.PersonName = user.Prefix + user.FirstName + " " + user.LastName;
			}
			return View();
        }

        public ActionResult Recents()
        {
			if (User.Identity.IsAuthenticated)
			{
				LoginIdentity user = (LoginIdentity)User.Identity;
				ViewBag.PersonNo = user.Name;
				ViewBag.PersonName = user.Prefix + user.FirstName + " " + user.LastName;
			}
			return View();
        }

        public ActionResult Search()
        {
			if (User.Identity.IsAuthenticated)
			{
				LoginIdentity user = (LoginIdentity)User.Identity;
				ViewBag.PersonNo = user.Name;
				ViewBag.PersonName = user.Prefix + user.FirstName + " " + user.LastName;

				Quota obj = new Quota(User, user.PersonNo);
				ViewBag.LeaveTypeOptions = string.Join(";", obj.OfLeaveType.Select(
					e => string.Concat(e.LeaveType.TypeSubID, ":", e.LeaveType.NameTH)).ToArray());
			}
			return View();
        }

        public ActionResult Rechecked()
        {
			if (User.Identity.IsAuthenticated)
			{
				Quota obj = new Quota(User, User.Identity.Name);
				ViewBag.LeaveTypeOptions = string.Join(";", obj.OfLeaveType.Select(
					e => string.Concat(e.LeaveType.TypeSubID, ":", e.LeaveType.NameTH)).ToArray());
			}
			return View();
        }

		public ActionResult Underling()
		{
			if (User.Identity.IsAuthenticated)
			{
				ViewBag.HeadPersonNo = User.Identity.Name;

				Quota obj = new Quota(User, User.Identity.Name);
				ViewBag.LeaveTypeOptions = string.Join(";", obj.OfLeaveType.Select(
					e => string.Concat(e.LeaveType.TypeSubID, ":", e.LeaveType.NameTH)).ToArray());
			}
			return View();
		}

		public ActionResult Calendar()
		{
			return View();
		}

        public ActionResult AjaxLeaveCalendar(string PersonNo, double? start, double? end)
        {
            string[] colorNames = Enum.GetNames(typeof(KnownColor));
            int running = Math.Abs((int)DateTime.Now.Ticks);
            string bgColorName = colorNames[running % colorNames.Length]; // random color

            DateTime n = DateTime.Now;
            DateTime _1 = new DateTime(n.Year, n.Month, 1);
            DateTime _n = new DateTime(n.Year, n.Month, DateTime.DaysInMonth(n.Year, n.Month));

            CultureInfo en = new CultureInfo("en-US");
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime since = start.HasValue ? origin.AddSeconds(start.Value) : _1;
            DateTime until = end.HasValue ? origin.AddSeconds(end.Value) : _n;
            List<object> list = new List<object>();
            if (!string.IsNullOrWhiteSpace(PersonNo))
            {
                PersonRecord p;
                string name;
                foreach (var r in LeaveCore.Leave.ListLeaveDaysOf(User, PersonNo, since, until))
                {
                    p = r.Person;
                    name = p.NameFirstTH + " " + p.NameLastTH;
                    var o = TimeSpan.FromMinutes(r.TotalLeaveMinutes);
                    list.Add(new
                    {
                        id = r.RequestID,
                        title = string.Format("({0}h) {1}", o.Minutes > 0 ? Math.Floor(o.TotalHours) + o.ToString("':'mm") : o.TotalHours.ToString(), name),
                        allDay = false,//r.TotalLeaveHours >= Const.DEFAULT_WORKHOURS_OF_DAY,
                        start = r.Since.Value.ToString("yyyy-MM-ddTHH:mm:ss", en),
                        end = r.Until.Value.ToString("yyyy-MM-ddTHH:mm:ss", en)/*,
                        color = bgColorName,
                        textColor = IdealTextColorName(bgColorName)*/
                    });
                }
            }
            return Json(list);
        }


        public string IdealTextColorName(string bgName)
        {
            Color bg = Color.FromName(bgName);
            int nThreshold = 105;
            int bgDelta = Convert.ToInt32((bg.R * 0.299) + (bg.G * 0.587) + (bg.B * 0.114));

            Color foreColor = (255 - bgDelta < nThreshold) ? Color.Black : Color.White;
            return foreColor.Name;
        }

		[HttpGet]
		public ActionResult Form(string PersonNo)
		{
			Person Person = null;
			if (!string.IsNullOrEmpty(PersonNo))
			{
				Person = new Person(User, PersonNo, true);
			}
			else Person = new Person(User, User.Identity.Name, true);
			ViewBag.PersonNo = Person.PersonNo;
			ViewBag.EmployeeNo = Person.Record.Employee.EmployeeNo;
			ViewBag.PersonName = Person.Record.PrefixTH+Person.Record.NameFirstTH+" "+Person.Record.NameLastTH;
			ViewBag.PositionName = Person.Record.Employee.PositionTH;
            ViewBag.DepartmentName = Person.Record.Employee.DisplaySection;
            ViewBag.CompanyCode = Person.Record.Employee.CompanyCode;

			Grantors Grantors = new Grantors(User, Person.PersonNo);
			int fIndex = Grantors.Heads.FindIndex(f => f.Priority == Grantors.Heads.Max(m => m.Priority));
			if (fIndex > -1)
			{
				Regex reg = new Regex(@"นาย|นางสาว|นาง|น.ส.");
				string HeadPrefixTH = reg.Replace(Grantors.Heads[fIndex].HeadPrefixTH, "คุณ");
				ViewBag.HeadPersonName = HeadPrefixTH+Grantors.Heads[fIndex].HeadNameFirstTH+" "+Grantors.Heads[fIndex].HeadNameLastTH;
			}

            //switch (Person.Record.Employee.CompanyCode.Trim())
            //{
            //    case "บริษัท วีรสุ กรุ๊ป จำกัด": ViewBag.aVerasuGroup = "checked"; break;
            //    case "บริษัท วีรสุ รีเทล จำกัด": ViewBag.aVerasuRetail = "checked"; break;
            //    case "บริษัท วิสต้าคาเฟ่ จำกัด": ViewBag.aVistaCafe = "checked"; break;
            //    case "ห้างหุ้นส่วนจำกัด วีรสุ": ViewBag.aVerasuLimited = "checked"; break;
            //    case "บริษัท เซอร์กิต เซ็นเตอร์ จำกัด": ViewBag.aCircuitCenter = "checked"; break;
            //}

			Quota Quota = new Quota(User, Person.PersonNo);
			Dictionary<string, Dictionary<string, string>> dict = new Dictionary<string, Dictionary<string, string>>()
			{
				{Const.TYPE_NO_SICK, new Dictionary<string, string>()
					{{"label","hasSick"},{"value","SickTakenAmount"}}},
				{Const.TYPE_NO_BUSINESS, new Dictionary<string, string>()
					{{"label","hasBusiness"},{"value","BusinessTakenAmount"}}},
				{Const.TYPE_NO_VACATION, new Dictionary<string, string>()
					{{"label","hasVocation"},{"value","VocationTakenAmount"}}},
				{Const.TYPE_NO_MILITARY, new Dictionary<string, string>()
					{{"label","hasMilitary"},{"value","MilitaryTakenAmount"}}},
				{Const.TYPE_NO_MATERNITY, new Dictionary<string, string>()
					{{"label","hasMaternity"},{"value","MaternityTakenAmount"}}}
			};
			ViewBag.TypeCaseID = Quota.GetValue(Const.TYPE_NO_BUSINESS, "TypeSubID");

            string selected;
            Leave.Models.FormPostbackData pb = ViewBag.Postback;

			int OtherMinutes = 0;
			List<string> list = new List<string>();
			foreach (var rec in Quota.OfLeaveType)
			{
                var s = Tool.ConvertMinutesToString(rec.TakenMinutes);
				if (dict.ContainsKey(rec.LeaveType.TypeNo))
				{
					ViewData.Add(dict[rec.LeaveType.TypeNo]["label"], rec.TakenMinutes > 0 ? "checked" : "");
                    ViewData.Add(dict[rec.LeaveType.TypeNo]["value"], rec.TakenMinutes > 0 ? s : "");
				}
                else OtherMinutes += rec.TakenMinutes;
                selected = rec.LeaveType.TypeSubID == pb.TypeSubID ? " selected" : "";
                list.Add("<option value=\"" + rec.LeaveType.TypeSubID + "\"" + selected + ">" + rec.LeaveType.NameTH + "</option>");
            }
            var o = TimeSpan.FromMinutes(OtherMinutes);
            var t = Tool.ConvertMinutesToString(OtherMinutes);
			ViewData.Add("hasOther", OtherMinutes > 0 ? "checked" : "");
            ViewData.Add("OtherTakenAmount", OtherMinutes > 0 ? t: "");

			ViewData.Add("LeaveTypeOptions", string.Join("\n", list));
			ViewBag.TypeCase = pb.TypeCase;
			
			return View("Form");
		}

		[HttpPost]
		public ActionResult Apply(
			string Action,
			string PersonNo,
			int TypeSubID,
			string TypeCase,
			string BeginDate,
			string BeginTime,
			string UntilDate,
			string UntilTime,
			string Reason,
			HttpPostedFileBase AttachFiles)
		{
			bool IsError = false;
			string ErrorMessage = null;
			bool IsSuccess = false;
			bool IsRequireAttached = false;
			bool IsConfirm = Action.Equals("Saved");

			Int64 RequestID = 0;
			Workshifts Shifts = null;
			LeaveCore.Leave obj = null;
			List<LeaveRecord> RequestedList = new List<LeaveRecord>();
			List<LeaveRecord> DuplicationList = new List<LeaveRecord>();
            List<EmailLogRecord> EmailLogs = new List<EmailLogRecord>();

            #region Input Validation
            var pb = new Leave.Models.FormPostbackData();

            ViewBag.Postback = pb;

            pb.PersonNo = PersonNo;
            pb.TypeSubID = TypeSubID;
            pb.BeginDate = BeginDate;
            pb.BeginTime = BeginTime;
            pb.UntilDate = UntilDate;
            pb.UntilTime = UntilTime;
            pb.Reason = Reason;
            pb.TypeCase = TypeCase;

			ViewBag.PersonNo = PersonNo;
			ViewBag.TypeSubID = TypeSubID;
			ViewBag.BeginDate = BeginDate;
			ViewBag.BeginTime = BeginTime;
			ViewBag.UntilDate = UntilDate;
			ViewBag.UntilTime = UntilTime;
			ViewBag.Reason = Reason;
			ViewBag.TypeCase = TypeCase;

            if (TypeSubID == 0)
            {
                pb.AlertEnabled = true;
                pb.FocusOnElementID = "TypeSubID";
                pb.AlertMessage = "โปรดระบุประเภทการลา";
                return Form(PersonNo);
            }

            if (TypeSubID == 2 && string.IsNullOrEmpty(Reason)) //Const.TYPE_NO_BUSINESS)
            {
                pb.AlertEnabled = true;
                pb.FocusOnElementID = "Reason";
                pb.AlertMessage = "โปรดระบุเหตุผลการลา";
                return Form(PersonNo);
            }

            CultureInfo enLocale = new CultureInfo("en-US");
            DateTime Since;
            if (!DateTime.TryParseExact(BeginDate, "dd/MM/yyyy", enLocale, DateTimeStyles.None, out Since))
            {
                pb.AlertEnabled = true;
                pb.FocusOnElementID = "BeginDate";
                pb.AlertMessage = "โปรดระบุวันที่ให้ถูกต้อง";
                return Form(PersonNo);
            }
            if (!DateTime.TryParseExact(BeginTime, "HH:mm", enLocale, DateTimeStyles.None, out Since))
            {
                pb.AlertEnabled = true;
                pb.FocusOnElementID = "BeginTime";
                pb.AlertMessage = "โปรดระบุเวลาให้ถูกต้อง";
                return Form(PersonNo);
            }
            Since = DateTime.ParseExact(BeginDate + " " + BeginTime, "dd/MM/yyyy HH:mm", enLocale, DateTimeStyles.None);
            DateTime Until;
            if (!DateTime.TryParseExact(UntilDate, "dd/MM/yyyy", enLocale, DateTimeStyles.None, out Until))
            {
                pb.AlertEnabled = true;
                pb.FocusOnElementID = "UntilDate";
                pb.AlertMessage = "โปรดระบุวันที่ให้ถูกต้อง";
                return Form(PersonNo);
            }
            if (!DateTime.TryParseExact(UntilTime, "HH:mm", enLocale, DateTimeStyles.None, out Until))
            {
                pb.AlertEnabled = true;
                pb.FocusOnElementID = "UntilTime";
                pb.AlertMessage = "โปรดระบุเวลาให้ถูกต้อง";
                return Form(PersonNo);
            }
            Until = DateTime.ParseExact(UntilDate + " " + UntilTime, "dd/MM/yyyy HH:mm", enLocale, DateTimeStyles.None);

            // แปลงพ.ศ.ให้เป็นค.ศ.อัตโนมัติ
            if (Since.Year >= 2500)
                Since = Since.AddYears(-543);
            if (Until.Year >= 2500)
                Until = Until.AddYears(-543);

            if (Math.Abs(Since.Year - DateTime.Now.Year) > 1)
            {
                pb.AlertEnabled = true;
                if (Since.Date < DateTime.Now.Date) pb.AlertMessage = "โปรดระบุเวลาให้ถูกต้อง";
                else pb.AlertMessage = "ยื่นลาล่วงหน้าได้ไม่เกิน 1 ปี";
                pb.FocusOnElementID = "BeginDate";
                return Form(PersonNo);
            }
            else if (Math.Abs(Until.Year - DateTime.Now.Year) > 1)
            {
                pb.AlertEnabled = true;
                if (Since.Date < DateTime.Now.Date) pb.AlertMessage = "โปรดระบุเวลาให้ถูกต้อง";
                else pb.AlertMessage = "ยื่นลาล่วงหน้าได้ไม่เกิน 1 ปี";
                pb.FocusOnElementID = "UntilDate";
                return Form(PersonNo);
            }
            else if (Math.Abs(Until.Date.Subtract(Since.Date).Days) >= 120)
            {
                pb.AlertEnabled = true;
                pb.AlertMessage = "ลาต่อเนื่องได้สูงสุด 120 วัน";
                pb.FocusOnElementID = "UntilDate";
                return Form(PersonNo);
            }
            else if (Since > Until)
            {
                pb.AlertEnabled = true;
                pb.AlertMessage = "โปรดระบุช่วงวันลาให้ถูกต้อง";
                pb.FocusOnElementID = Since.Date > Until.Date ? "UntilDate" : "UntilTime";
                return Form(PersonNo);
            }
            #endregion

            #region Process Leave (Apply/Saved)
			try
			{
				obj = new LeaveCore.Leave(User, PersonNo);
				obj.SetLeaveParams(PersonNo, TypeSubID, TypeCase, Since, Until, Reason, null, AttachFiles,
                     Leave.Properties.Settings.Default.AttachFilePath);

				// IF confirm
                if (IsConfirm)
                {
                    RequestID = obj.NewLeaveRequest();
                    IsSuccess = RequestID != 0;
                    if (IsSuccess)
                    {
                        // Save log, Send email
						try
						{
							ActionLog.New(User, LeaveCore.Action.ApplyConfirmed, null, RequestID);
						} 
						catch {}

                        // ถ้าบันทึกใบลาเข้าระบบด้วยสิทธิ IMPERSONATE ไม่ต้องส่งเมลล์แจ้งหัวหน้าและใบลาจะถูกอนุมัติโดยอัตโนมัติ
                        var shouldSendMail = !User.IsInRole(Const.ROLE_IMPERSONATE);
                        if (!shouldSendMail)
                        {
                            ActionLog.File.Info(string.Format(
                                "บันทึกใบลาเข้าระบบด้วยสิทธิ IMPERSONATE: LeaveRequest={0}, PersonNo={1}, Date={2} - {3}, Time={4} - {5}, LeaveType={6}, ByUser={7}",
                                RequestID, PersonNo, BeginDate, UntilDate, BeginTime, UntilTime, TypeSubID, User.Identity.Name));
                        }
                        else
                        {
                            // แยก Try Catch หลายๆ block เพื่อที่ว่าหากส่งเมลล์หาคนแรกไม่ได้ จะได้ข้ามไปส่งคนถัดไปได้
                            // และไม่ควรส่งเมลล์หาคนเดิมซ้ำๆ
                            List<string> sentAccList = new List<string>();
                            //MailAddress mailAcc;
                            try
                            {
                                Grantors Grantors = new Grantors(User, PersonNo);

                                var msg = string.Format("leave request #{2} of {0} to its supervisor by e-mail (head={1})",
                                    PersonNo, Grantors.Heads == null ? "null" : string.Join(",", Grantors.Heads.Select(x => x.HeadPersonNo + "-" + x.HeadEmail).ToArray()), RequestID);
                                ActionLog.File.Debug(msg);

                                if (Grantors.Heads != null)
                                {
                                    EmailService service = new EmailService(User);
                                    foreach (var rec in Grantors.Heads)
                                    {
                                        var debug = "send to " + rec.HeadEmail;
                                        try
                                        {
                                            //mailAcc = new MailAddress(rec.HeadEmail);
                                            //if (sentAccList.Contains(mailAcc.Address)) // ถ้าส่งไปแล้วก็ข้ามไป
                                            if (sentAccList.Contains(rec.HeadEmail)) // ถ้าส่งไปแล้วก็ข้ามไป
                                                continue;
                                            var model = new LeaveCore.Email.Model.NotificationEmailModel(User, rec.HeadPersonNo, RequestID)
                                            {
                                                InternetBasedUrl = Leave.Properties.Settings.Default.InternetBasedUrl
                                            };
                                            service.SendEmail(rec.HeadEmail, null, "แจ้งการยื่นใบลาของพนักงาน", EmailType.Notification, model);
                                            //sentAccList.Add(mailAcc.Address);
                                            sentAccList.Add(rec.HeadEmail);
                                            debug += " ... success";
                                        }
                                        catch (Exception e)
                                        {
                                            debug += " ... error: " + e.ToString();
                                        }
                                        ActionLog.File.Debug(debug);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                ActionLog.File.Error(e);
                            }

                            sentAccList.Clear();
                            try
                            {
                                Vetoes Vetoes = new Vetoes(User, PersonNo);
                                //List<VetoRecord> Heads = LeaveCore.Leave.GetVetoes(User, RequestID, null);
                                if (Vetoes.Heads != null)
                                {
                                    EmailService service = new EmailService(User);
                                    foreach (var rec in Vetoes.Heads)
                                    {
                                        try
                                        {
                                            //mailAcc = new MailAddress(rec.Email);
                                            //if (sentAccList.Contains(mailAcc.Address)) // ถ้าส่งไปแล้วก็ข้ามไป
                                            if (sentAccList.Contains(rec.Email)) // ถ้าส่งไปแล้วก็ข้ามไป
                                                continue;
                                            var Veto = new VetoRecord();
                                            Veto.Person = rec;
                                            var model = new LeaveCore.Email.Model.VetoEmailModel(User, RequestID, Veto)
                                            {
                                                InternetBasedUrl = Leave.Properties.Settings.Default.InternetBasedUrl
                                            };
                                            service.SendEmail(rec.Email, null, "แจ้งการยื่นใบลาของพนักงาน", EmailType.Veto, model);
                                            //sentAccList.Add(mailAcc.Address);
                                            sentAccList.Add(rec.Email);
                                        }
                                        catch { }
                                    }
                                }
                            }
                            catch { }

                            try
                            {
                                EmailLogs = EmailLog.GetLog(User, RequestID);
                            }
                            catch { }
                        }
                    }
                }
                else
                {
                    // Save log
					try 
					{
						ActionLog.New(User, LeaveCore.Action.Applying, null, null);
					} 
					catch  {}
                }
			}
			
			catch (LeaveParameterException e)
			{
				IsError = true;
				if (e.ParameterName.Equals("AUTHORITY_MISSING"))
					ErrorMessage = "ยังไม่มีการระบุผู้อนุมัติใบลา";
				else
					ErrorMessage = "Parameter '" + e.ParameterName + "' has not set.";
			}
			catch (LeaveRequestDuplicationException e)
			{
				IsError = true;
				ErrorMessage = "ยื่นใบลาซ้ำ";
				DuplicationList = e.DuplicationList;
			}
			catch (LeaveRequestEffectiveVacationException e)
			{
				IsError = true;
				ErrorMessage = "ไม่สามารถยื่นใบลาได้ ต้องมีอายุงานอย่างน้อย " + e.MonthsToAllow + " เดือน";
			}
			catch (LeaveRequestQuotaExceedException e)
			{
				IsError = true;
                ErrorMessage =
                      "จำนวนชั่วโมงการลาเกินโควต้าที่กำหนด ( "
                    + "คงเหลือ = " + Tool.ConvertMinutesToString(e.Quota) + " วัน:ช.ม.:นาที, "
                    + "ยื่นลา = " + Tool.ConvertMinutesToString(e.Request) + " วัน:ช.ม.:นาที )";
			}
			catch (LeaveRequestPreDateException e)
			{
				IsError = true;
				if (e.DaysToAllow > -1)
					ErrorMessage = "ต้องยื่นใบลาล่วงหน้าอย่างน้อย " + e.DaysToAllow + " วัน !!";
				else
					ErrorMessage = "ระบุวันลาไม่ถูกต้อง ( กรณีลาป่วย อณุญาตให้ยื่นใบลาย้อนหลังเท่านั้น )";
			}
			catch (LeaveRequestMedicalCertificateException)
			{
				IsError = true;
				ErrorMessage = "ไม่มีไฟล์แนบ";
			}
			catch (LeaveException e)
			{
				IsError = true;
				ErrorMessage = "'" + e.Message + "' for RequestID is " + e.RequestID + ".";
			}
			catch (Exception e)
			{
				IsError = true;
				ErrorMessage = e.Message;
			}
			finally
			{
                try
                {
                    // error message ที่อาจจะเกิดจากการระบุเวลาในช่วงพักเที่ยง
                    string tempMessage = null;
                    if (IsSuccess) RequestedList = LeaveCore.Leave.List(User, RequestID);
                    else RequestedList = obj.SerializeLeaveLines(out tempMessage);

                    if (!IsError && tempMessage != null)
                    {
                        IsError = true;
                        ErrorMessage = tempMessage;
                    }

                    Shifts = new Workshifts(User, PersonNo);
					int temp = obj.ExecuteScalar<int>("SELECT [Type Sub ID] FROM [LV Type Sub] WHERE [Type No]='" + Const.TYPE_NO_SICK + "'", null);
                    if (temp.Equals(TypeSubID) && RequestedList.Sum(e => e.TotalMinutes) >= Const.REQUEST_SICK_CONTINUALLY_HOURS * 60)
						IsRequireAttached = true;
                }
                catch (Exception e)
                {
                    IsError = true;
                    ErrorMessage = e.Message;
                }
			}
			#endregion

			ViewBag.IsError = IsError;
			ViewBag.ErrorMessage = ErrorMessage;
			ViewBag.IsSuccess = IsSuccess;
			ViewBag.IsConfirm = IsConfirm;
			ViewBag.RequestedList = RequestedList;
			ViewBag.DuplicationList = DuplicationList;
			ViewBag.IsRequireAttached = IsRequireAttached;

			ViewBag.ShiftBegin = Shifts.Times[0].TimeBegin.Value.ToString("HH:mm");
			ViewBag.ShiftUntil = Shifts.Times[1].TimeUntil.Value.ToString("HH:mm");
			ViewBag.ShiftHours = Shifts.Times[1].TimeUntil.Value.Subtract(Shifts.Times[0].TimeBegin.Value).Hours - 1;
			ViewBag.ShiftMorning = string.Format("{0} - {1}", Shifts.Times[0].TimeBegin.Value.ToString("HH:mm"), Shifts.Times[0].TimeUntil.Value.ToString("HH:mm"));
			ViewBag.ShiftAfternoon = string.Format("{0} - {1}", Shifts.Times[0].TimeBegin.Value.ToString("HH:mm"), Shifts.Times[1].TimeUntil.Value.ToString("HH:mm"));

            ViewBag.EmailLogs = EmailLogs;
            ViewBag.RequestID = RequestID;

			ViewBag.UrlLinkForm = Leave.Properties.Settings.Default.LinkReportForm;

			return View();
		}

		[HttpGet]
		public ActionResult Todo(Int64 RequestID, string PersonNo, int RequestCompare)
		{
			List<LeaveRecord> obj = null;
			if (RequestCompare == 0) obj = LeaveCore.Leave.List(User, RequestID);
			else if (RequestCompare == 1) obj = LeaveCore.Leave.ListNextRecent(User, PersonNo, RequestID);
			else if (RequestCompare == -1) obj = LeaveCore.Leave.ListPrevRecent(User, PersonNo, RequestID);

            if (obj == null || obj.Count == 0) obj = LeaveCore.Leave.List(User, RequestID);

			PersonRecord Person = obj[0].LeaveRequested.Person;
			ViewBag.PersonNo = PersonNo;
			ViewBag.EmployeeNo = Person.Employee.EmployeeNo;
			ViewBag.PersonName = Person.PrefixTH + Person.NameFirstTH + " " + Person.NameLastTH;
			ViewBag.PositionName = Person.Employee.PositionTH;
			ViewBag.DepartmentName = Person.Employee.DisplaySection;
			ViewBag.StartingDate = Person.Employee.StartingDate.Value.Date == DateTime.MinValue.Date ? "" :
								   Person.Employee.StartingDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture);

			ViewBag.RequestID = obj[0].LeaveRequested.RequestID;
			ViewBag.Referrer = Request.UrlReferrer == null ? "" : Request.UrlReferrer.PathAndQuery;
            ViewBag.UrlPrevData = string.Join("/", new string[] { obj[0].LeaveRequested.RequestID.ToString(), PersonNo, "-1" });
            ViewBag.UrlNextData = string.Join("/", new string[] { obj[0].LeaveRequested.RequestID.ToString(), PersonNo, "1" });
			ViewBag.UrlLinkForm = string.Concat(Leave.Properties.Settings.Default.LinkReportForm, obj[0].LeaveRequested.RequestID);
			
			List<object> results = new List<object>();
			foreach (var rec in obj.OrderBy(e => e.LeaveDate))
			{
				results.Add(new
				{
					LeaveID = rec.LeaveID,
					StatusID = rec.StatusID,
					IsAction = DateTime.Now.Date < rec.LeaveDate.Value || rec.StatusID == Const.STATUS_LEAVE_PENDING_APPROVAL,
					IsPending = rec.StatusID == Const.STATUS_LEAVE_PENDING_APPROVAL,
					IsAwaiting = rec.StatusID == Const.STATUS_LEAVE_AWAITING,
					IsApproved = rec.StatusID == Const.STATUS_LEAVE_APPROVED,
                    LeaveMinutes = rec.TotalMinutes,
					ApplyDate = rec.LeaveRequested.ApplyDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeaveType = rec.LeaveRequested.TypeSubName,
					LeaveDate = rec.LeaveDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeavePeriod = rec.DisplayPeriod,
					LeaveAmount = rec.DisplayTotalHours,
					LeaveStatus = rec.DisplayStatusName,
					LeaveReason = rec.Comment,
					ActionRequested = string.Empty
				});
			}
			ViewBag.JsonLocalData = new JavaScriptSerializer().Serialize(results);

			return View();
		}

		[HttpGet]
		public ActionResult Viewer(Int64 RequestID, string PersonNo, int RequestCompare, string Digest)
		{
			if (!User.Identity.IsAuthenticated)
				return View();
			if ((!(User.Identity.Name.Equals(PersonNo) || User.IsInRole(Const.ROLE_HEAD) || User.IsInRole(Const.ROLE_HR))) ||
                !LeaveCore.Leave.DigestVeto(PersonNo, RequestID).Equals(Digest))
				throw new SecurityException("UNAUTHORIZED");
			
			List<LeaveRecord> obj = null;
			if (RequestCompare == 0) obj = LeaveCore.Leave.List(User, RequestID);
			if (RequestCompare == 1) obj = LeaveCore.Leave.ListNextRecent(User, PersonNo, RequestID);
			if (RequestCompare == -1) obj = LeaveCore.Leave.ListPrevRecent(User, PersonNo, RequestID);
			
			if (obj == null || obj.Count == 0) obj = LeaveCore.Leave.List(User, RequestID);

			PersonRecord Person = obj[0].LeaveRequested.Person;
			ViewBag.PersonNo = PersonNo;
			ViewBag.EmployeeNo = Person.Employee.EmployeeNo;
			ViewBag.PersonName = Person.PrefixTH + Person.NameFirstTH + " " + Person.NameLastTH;
			ViewBag.PositionName = Person.Employee.PositionTH;
			ViewBag.DepartmentName = Person.Employee.DisplaySection;
			ViewBag.StartingDate = Person.Employee.StartingDate.Value.Date == DateTime.MinValue.Date ? "" :
								   Person.Employee.StartingDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture);

			ViewBag.RequestID = obj[0].LeaveRequested.RequestID;
            ViewBag.Referrer = Request.UrlReferrer == null || Request.UrlReferrer.PathAndQuery.Contains("Login") ? Url.Content("~/Leave/Index") : Request.UrlReferrer.PathAndQuery;
            ViewBag.UrlPrevData = string.Join("/", new string[] { obj[0].LeaveRequested.RequestID.ToString(), PersonNo, "-1", LeaveCore.Leave.DigestVeto(PersonNo, obj[0].LeaveRequested.RequestID) });
            ViewBag.UrlNextData = string.Join("/", new string[] { obj[0].LeaveRequested.RequestID.ToString(), PersonNo, "1", LeaveCore.Leave.DigestVeto(PersonNo, obj[0].LeaveRequested.RequestID) });
			ViewBag.UrlLinkForm = string.Concat(Leave.Properties.Settings.Default.LinkReportForm, obj[0].LeaveRequested.RequestID);

			List<object> results = new List<object>();
			foreach (var rec in obj.OrderBy(e => e.LeaveDate))
			{
				results.Add(new
				{
					LeaveID = rec.LeaveID,
					StatusID = rec.StatusID,
                    LeaveMinutes = rec.TotalMinutes,
					ApplyDate = rec.LeaveRequested.ApplyDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeaveType = rec.LeaveRequested.TypeSubName,
					LeaveDate = rec.LeaveDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeavePeriod = rec.DisplayPeriod,
					LeaveAmount = rec.DisplayTotalHours,
					LeaveStatus = rec.DisplayStatusName,
					LeaveReason = rec.Comment
				});
			}
			ViewBag.JsonLocalData = new JavaScriptSerializer().Serialize(results);

			return View();
		}

		[HttpGet]
		public ActionResult Adjusting(Int64 RequestID, string PersonNo, int RequestCompare, string Digest)
		{	
			List<LeaveRecord> obj = null;
			if (RequestCompare == 0) obj = LeaveCore.Leave.List(User, RequestID);
			if (RequestCompare == 1) obj = LeaveCore.Leave.ListNextRecent(User, PersonNo, RequestID);
			if (RequestCompare == -1) obj = LeaveCore.Leave.ListPrevRecent(User, PersonNo, RequestID);
			
			if (obj == null || obj.Count == 0) obj = LeaveCore.Leave.List(User, RequestID);

			PersonRecord Person = obj[0].LeaveRequested.Person;
			ViewBag.PersonNo = PersonNo;
			ViewBag.EmployeeNo = Person.Employee.EmployeeNo;
			ViewBag.PersonName = Person.PrefixTH + Person.NameFirstTH + " " + Person.NameLastTH;
			ViewBag.PositionName = Person.Employee.PositionTH;
			ViewBag.DepartmentName = Person.Employee.DisplaySection;
			ViewBag.StartingDate = Person.Employee.StartingDate.Value.Date == DateTime.MinValue.Date ? "" :
								   Person.Employee.StartingDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture);

			ViewBag.RequestID = obj[0].LeaveRequested.RequestID;
            ViewBag.Referrer = Request.UrlReferrer == null ? "" : Request.UrlReferrer.PathAndQuery;
            ViewBag.UrlPrevData = string.Join("/", new string[] { obj[0].LeaveRequested.RequestID.ToString(), PersonNo, "-1", LeaveCore.Leave.DigestVeto(PersonNo, obj[0].LeaveRequested.RequestID) });
            ViewBag.UrlNextData = string.Join("/", new string[] { obj[0].LeaveRequested.RequestID.ToString(), PersonNo, "1", LeaveCore.Leave.DigestVeto(PersonNo, obj[0].LeaveRequested.RequestID) });
			ViewBag.UrlLinkForm = string.Concat(Leave.Properties.Settings.Default.LinkReportForm, obj[0].LeaveRequested.RequestID);

			List<object> results = new List<object>();
			foreach (var rec in obj.OrderBy(e => e.LeaveDate))
			{
				results.Add(new
				{
					LeaveID = rec.LeaveID,
					StatusID = rec.StatusID,
					IsCancelled = rec.StatusID == Const.STATUS_LEAVE_CANCELLED,
                    LeaveMinutes = rec.TotalMinutes,
					ApplyDate = rec.LeaveRequested.ApplyDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeaveType = rec.LeaveRequested.TypeSubName,
					LeaveDate = rec.LeaveDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeavePeriod = rec.DisplayPeriod,
					LeaveAmount = rec.DisplayTotalHours,
					LeaveStatus = rec.DisplayStatusName,
					LeaveReason = rec.Comment
				});
			}
			ViewBag.JsonLocalData = new JavaScriptSerializer().Serialize(results);

			try
			{
				ViewBag.LeaveTypeChanged = string.Join("", 
					new Quota(User, PersonNo)
					.OfLeaveType.Where(e => new[] { Const.TYPE_NO_SICK, Const.TYPE_NO_BUSINESS,  Const.TYPE_NO_VACATION }
					.Contains(e.LeaveType.TypeNo))
					.OrderBy(e => e.LeaveType.NameTH)
					.Select(e => string.Concat("<option value=\\\"", e.LeaveType.TypeSubID, "\\\">", e.LeaveType.NameTH, "</option>"))
					.ToArray());
			}
			catch 
			{
				ViewBag.LeaveTypeChanged = string.Empty;
			}

			try
			{
				ViewBag.LeaveStatusChanged = string.Concat(
					"<option value=\\\"", Const.STATUS_LEAVE_CANCELLED, "\\\">", 
					Const.GetStatusName(User, Const.STATUS_LEAVE_CANCELLED, null), "</option>");
			}
			catch
			{
				ViewBag.LeaveStatusChanged = string.Empty;
			}

			return View();
		}

		//[HttpGet]
		public ActionResult AjaxListQuota(int page, int rows, bool _search, string sidx, string sord, string PersonNo)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();
			try
			{
				Quota Quota = new Quota(User, PersonNo);
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						Quota.OfLeaveType = Quota.OfLeaveType.OrderBy(e => Tool.OrderByProperty(sidx, e) ?? Tool.OrderByProperty(sidx, e.LeaveType)).ToList();
					else
						Quota.OfLeaveType = Quota.OfLeaveType.OrderByDescending(e => Tool.OrderByProperty(sidx, e) ?? Tool.OrderByProperty(sidx, e.LeaveType)).ToList();
				}
				records = Quota.OfLeaveType.Count();
				decimal h = Const.DEFAULT_WORKHOURS_OF_DAY;
                var list = Quota.OfLeaveType.Skip((page - 1) * rows).Take(rows);
				foreach (var rec in list)
				{
                    Tool.ConvertMinutesToString(rec.TakenMinutes);
					var d = TimeSpan.FromDays((double)(rec.QuotaAmount + rec.QuotaPrevAmount));
					results.Add(new
					{
						id = ++num,
						cell = new[] {
                            rec.LeaveType.NameTH,
							d.TotalMinutes > 0 && d.TotalDays < Const.QUOTA_UNLIMITED
								? string.Format("{0}", d.TotalDays.ToString(), d.Minutes > 0 ? Math.Floor(d.TotalHours) + d.ToString("':'mm") : d.TotalHours.ToString())
								: (d.TotalDays < Const.QUOTA_UNLIMITED ? "-" : "มีสิทธิลา"),
							Tool.ConvertMinutesToString(rec.TakenMinutes),
							Tool.ConvertMinutesToString(rec.ApproveMinutes),
							Tool.ConvertMinutesToString(rec.BalanceMinutes)
                        }
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxListRecents(int page, int rows, bool _search, string sidx, string sord, string PersonNo)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();
			try
			{
				List<RequestRecord> Requested = LeaveCore.Leave.ListRecents(User, PersonNo, 0, 0);
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						Requested = Requested.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						Requested = Requested.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				records = Requested.Count();
				foreach (var rec in Requested.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[] {
							rec.RequestID.ToString(),
							rec.StatusID.ToString(),
							(rec.StatusID == Const.STATUS_LEAVE_PENDING_APPROVAL).ToString().ToLower(),
							(rec.StatusID == Const.STATUS_LEAVE_AWAITING).ToString().ToLower(),
							(rec.StatusID == Const.STATUS_LEAVE_APPROVED).ToString().ToLower(),
							rec.Person.PersonNo,
							null,
							rec.ApplyDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
							rec.TypeSubName,
							rec.Since.Value.ToString("dd/MM/yyyy HH:mm", Thread.CurrentThread.CurrentCulture),
							rec.Until.Value.ToString("dd/MM/yyyy HH:mm", Thread.CurrentThread.CurrentCulture),
							rec.DisplayTotalHours,
							rec.DisplayStatusName,
							rec.Reason,
							null
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxListHistory(int page, int rows, bool _search, string sidx, string sord, string filters, string PersonNo)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();

			int TypeSubID = 0;
			DateTime? Since = null;
			DateTime? Until = null;
			int tryparse;
			DateTime temp;

			Dictionary<string, string> filtered = null;
			if (_search && !string.IsNullOrEmpty(filters))
			{				
				JObject jObject = JObject.Parse(filters);
				JToken  kObject = jObject["rules"];
				filtered = new Dictionary<string, string>();
				foreach (var token in kObject.ToList())
				{
					string Key = token.SelectToken("field").Value<string>();
					string Value = token.SelectToken("data").Value<string>();
					filtered.Add(Key.ToUpper(), Value);
				}
				if (filtered.ContainsKey("TYPESUBNAME"))
					if (Int32.TryParse(filtered["TYPESUBNAME"], out tryparse))
						TypeSubID = tryparse;
                if (filtered.ContainsKey("SINCE"))
                    if (DateTime.TryParseExact(filtered["SINCE"], "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out temp))
                        Since = temp;
                if (filtered.ContainsKey("UNTIL"))
                    if (DateTime.TryParseExact(filtered["UNTIL"], "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out temp))
                        Until = temp;
			}

			try
			{
				if (TypeSubID > 0 && Since == null && Until == null)
				{
					Since = new DateTime(1900,1,1);
					Until = new DateTime(9999,12,31);
				}
				if (Since == null && Until != null) Since = new DateTime(1900,1,1);
				if (Since != null && Until == null) Until = new DateTime(9999,12,31);
				if (Since == null) Since = new DateTime(1900,1,1);
				if (Until == null) Until = new DateTime(1900,1,1);

				List<RequestRecord> Requested = LeaveCore.Leave.ListHeaders(User, PersonNo, Since, Until, TypeSubID, 0, 0);
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						Requested = Requested.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						Requested = Requested.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				records = Requested.Count();
				foreach (var rec in Requested.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
					    id = ++num,
					    cell = new[] {
					        rec.RequestID.ToString(),
					        rec.StatusID.ToString(),
							rec.Person.PersonNo,
							null,
							rec.ApplyDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					        rec.TypeSubName,
							rec.Since.Value.ToString("dd/MM/yyyy HH:mm", Thread.CurrentThread.CurrentCulture),
							rec.Until.Value.ToString("dd/MM/yyyy HH:mm", Thread.CurrentThread.CurrentCulture),
					        rec.DisplayTotalHours,
					        rec.DisplayStatusName,
					        rec.Reason,
							null
					    }
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxListRechecked(int page, int rows, bool _search, string sidx, string sord, string filters)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();

			int TypeSubID = 0;
			DateTime? Since = null;
			DateTime? Until = null;
			string PersonName = "";
			int tryparse;
			DateTime temp;

			Dictionary<string, string> filtered = null;
			if (_search && !string.IsNullOrEmpty(filters))
			{				
				JObject jObject = JObject.Parse(filters);
				JToken  kObject = jObject["rules"];
				filtered = new Dictionary<string, string>();
				foreach (var token in kObject.ToList())
				{
					string Key = token.SelectToken("field").Value<string>();
					string Value = token.SelectToken("data").Value<string>();
					filtered.Add(Key.ToUpper(), Value);
				}
				if (filtered.ContainsKey("PERSONNAME")) 
					PersonName = filtered["PERSONNAME"];
				if (filtered.ContainsKey("TYPESUBNAME"))
					if (Int32.TryParse(filtered["TYPESUBNAME"], out tryparse))
						TypeSubID = tryparse;
				if (filtered.ContainsKey("SINCE"))
					if (DateTime.TryParseExact(filtered["SINCE"], "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out temp))
						Since = temp;
				if (filtered.ContainsKey("UNTIL"))
					if (DateTime.TryParseExact(filtered["UNTIL"], "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out temp))
						Until = temp;
			}

			try
			{
				if ((TypeSubID > 0 || !string.IsNullOrEmpty(PersonName)) && Since == null && Until == null)
				{
				    Since = new DateTime(1900,1,1);
				    Until = new DateTime(9999,12,31);
				}
				if (Since == null && Until != null) Since = new DateTime(1900,1,1);
				if (Since != null && Until == null) Until = new DateTime(9999,12,31);
				if (Since == null) Since = new DateTime(1900,1,1);
				if (Until == null) Until = new DateTime(1900,1,1);
				
				PersonRecord Record;
                List<RequestRecord> Requested = new List<RequestRecord>();
                if (!string.IsNullOrWhiteSpace(PersonName)) // performance tunning
                {
                    string ID = Person.List(User, DateTime.Now.Year);
                    while ((Record = Person.Next(ID)) != null)
                    {
                        bool match = string.Concat(Record.NameFirstTH, " ", Record.NameLastTH).ToLower().IndexOf(PersonName.ToLower()) > -1;
                        if (match)
                        {
                            List<RequestRecord> obj = LeaveCore.Leave.ListHeaders(User, Record.PersonNo, Since, Until, TypeSubID, 0, 0);
                            foreach (var rec in obj)
                            {
                                Requested.Add(rec);
                            }
                        }
                    }
                }
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						Requested = Requested.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						Requested = Requested.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				records = Requested.Count();
				foreach (var rec in Requested.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
					    id = ++num,
					    cell = new[] {
					        rec.RequestID.ToString(),
					        rec.StatusID.ToString(),
					        rec.Person.PersonNo,
							LeaveCore.Leave.DigestVeto(rec.Person.PersonNo, rec.RequestID),
					        null,
							rec.ApplyDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					        rec.Person.PrefixTH+rec.Person.NameFirstTH+" "+rec.Person.NameLastTH,
					        rec.TypeSubName,
							rec.Since.Value.ToString("dd/MM/yyyy HH:mm", Thread.CurrentThread.CurrentCulture),
							rec.Until.Value.ToString("dd/MM/yyyy HH:mm", Thread.CurrentThread.CurrentCulture),
					        rec.DisplayTotalHours,
					        rec.DisplayStatusName,
					        rec.Reason,
					        rec.AttachedVirtualpath == null ? string.Empty : Url.Content(rec.AttachedVirtualpath)
					    }
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxListUnderling(int page, int rows, bool _search, string sidx, string sord, string filters, string HeadPersonNo)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();

			int TypeSubID = 0;
			DateTime? Since = null;
			DateTime? Until = null;
			string PersonName = "";
			int tryparse;
			DateTime temp;

			Dictionary<string, string> filtered = null;
			if (_search && !string.IsNullOrEmpty(filters))
			{				
				JObject jObject = JObject.Parse(filters);
				JToken  kObject = jObject["rules"];
				filtered = new Dictionary<string, string>();
				foreach (var token in kObject.ToList())
				{
					string Key = token.SelectToken("field").Value<string>();
					string Value = token.SelectToken("data").Value<string>();
					filtered.Add(Key.ToUpper(), Value);
				}
				if (filtered.ContainsKey("PERSONNAME")) 
					PersonName = filtered["PERSONNAME"];
				if (filtered.ContainsKey("TYPESUBNAME"))
					if (Int32.TryParse(filtered["TYPESUBNAME"], out tryparse))
						TypeSubID = tryparse;
				if (filtered.ContainsKey("SINCE"))
					if (DateTime.TryParseExact(filtered["SINCE"], "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out temp))
						Since = temp;
				if (filtered.ContainsKey("UNTIL"))
					if (DateTime.TryParseExact(filtered["UNTIL"], "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out temp))
						Until = temp;
			}

			try
			{
				if ((TypeSubID > 0 || !string.IsNullOrEmpty(PersonName)) && Since == null && Until == null)
				{
				    Since = new DateTime(1900,1,1);
				    Until = new DateTime(9999,12,31);
				}
				if (Since == null && Until != null) Since = new DateTime(1900,1,1);
				if (Since != null && Until == null) Until = new DateTime(9999,12,31);
				if (Since == null) Since = new DateTime(1900,1,1);
				if (Until == null) Until = new DateTime(1900,1,1);
				
				List<RequestRecord> Requested = new List<RequestRecord>();
				List<PersonRecord> Underling = Person.ListUnderling(User, HeadPersonNo);
				if (!string.IsNullOrEmpty(PersonName))
					Underling = Underling.Where(e => string.Concat(e.NameFirstTH, " ", e.NameLastTH).ToLower().IndexOf(PersonName.ToLower()) > -1).ToList();
				foreach (PersonRecord Sub in Underling)
				{
					//Sub.Any(f => rec.Person.PersonNo.Contains(f.PersonNo));
					List<RequestRecord> obj = LeaveCore.Leave.ListHeaders(User, Sub.PersonNo, Since, Until, TypeSubID, 0, 0);
					//foreach (var rec in obj.OrderByDescending(or => or.Since)) Requested.Add(rec);
					foreach (var rec in obj)
					{
						Requested.Add(rec);
					}
				}
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						Requested = Requested.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						Requested = Requested.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				records = Requested.Count();
				foreach (var rec in Requested.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
					    id = ++num,
					    cell = new[] {
					        rec.RequestID.ToString(),
					        rec.StatusID.ToString(),
					        rec.Person.PersonNo,
							LeaveCore.Leave.DigestVeto(rec.Person.PersonNo, rec.RequestID),
					        null,
							rec.ApplyDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					        rec.Person.PrefixTH+rec.Person.NameFirstTH+" "+rec.Person.NameLastTH,
					        rec.TypeSubName,
							rec.Since.Value.ToString("dd/MM/yyyy HH:mm", Thread.CurrentThread.CurrentCulture),
							rec.Until.Value.ToString("dd/MM/yyyy HH:mm", Thread.CurrentThread.CurrentCulture),
					        rec.DisplayTotalHours,
					        rec.DisplayStatusName,
					        rec.Reason,
					        rec.AttachedVirtualpath == null ? string.Empty : Url.Content(rec.AttachedVirtualpath)
					    }
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxVerifyRequestCompare(int RequestID, string PersonNo)
		{			
			bool hasNextRequested = false;
			bool hasPrevRequested = false;
			try
			{
				hasNextRequested = LeaveCore.Leave.ListNextRecent(User, PersonNo, RequestID).Count == 1;
				hasPrevRequested = LeaveCore.Leave.ListPrevRecent(User, PersonNo, RequestID).Count == 1;
			}
			finally { }

			var JsonData = new { hasNextRequested = hasNextRequested, hasPrevRequested = hasPrevRequested };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		[HttpPost]
		public ActionResult AjaxChange(Int64 RequestID, Int64 LeaveID, int NewStatusID)
		{
			object results = null;
			LeaveRecord rec = null;
			bool error = false;
			string errorMessage = null;
			try
			{
				LeaveCore.Leave obj = new LeaveCore.Leave(User, null);
                obj.Load(RequestID, LeaveID).Change(NewStatusID, null, null);
			}
			catch (ArgumentException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (LeaveException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}
			finally
			{
				rec = LeaveCore.Leave.List(User, RequestID, LeaveID).DefaultIfEmpty(null).FirstOrDefault();
			}

			// กรณีเป็นการขอยกเลิกใบลาหลังจากอนุมัติแล้ว ให้ส่งอีเมลล์แจ้งเตือนผู้อนุมัติเพื่อให้อนุมุติ/ไม่อนุมัติ อีกรอบ
			if (!error && NewStatusID == Const.STATUS_LEAVE_CANCELREQUEST)
			{
                // ไม่ควรส่งเมลล์หาคนเดิมซ้ำๆ     
				List<string> sentAccList = new List<string>();
                MailAddress mailAcc;
                try
                {
					Grantors Grantors = new Grantors(User, rec.LeaveRequested.Person.PersonNo);
					if (Grantors.Heads != null)
					{
						EmailService service = new EmailService(User);
						foreach (var r in Grantors.Heads)
                        {
                            try
                            {
                                mailAcc = new MailAddress(r.HeadEmail);
                                if (sentAccList.Contains(mailAcc.Address)) // ถ้าส่งไปแล้วก็ข้ามไป
                                    continue;
                                var model = new LeaveCore.Email.Model.NotificationEmailModel(User, r.HeadPersonNo, RequestID)
                                {
                                    InternetBasedUrl = Leave.Properties.Settings.Default.InternetBasedUrl
                                };
                                service.SendEmail(r.HeadEmail, null, "แจ้งมีใบลารออนุมัติ", EmailType.Notification, model);
                                sentAccList.Add(mailAcc.Address);
                            }
                            catch { }
						}
					}
				}
				catch (Exception e) { errorMessage = e.Message; }
			}

			if (rec != null)
			{
				results = new
				{
					LeaveID = rec.LeaveID,
					StatusID = rec.StatusID,
					IsPending = rec.StatusID == Const.STATUS_LEAVE_PENDING_APPROVAL,
					IsAwaiting = rec.StatusID == Const.STATUS_LEAVE_AWAITING,
					IsApproved = rec.StatusID == Const.STATUS_LEAVE_APPROVED,
					LeaveAmount = rec.DisplayTotalHours,
					LeaveStatus = rec.DisplayStatusName
				};
			}
			var JsonData = new { error = error, errorMessage = errorMessage, results = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		[HttpPost]
		public ActionResult AjaxChangeAll(Int64 RequestID, int NewStatusID)
		{
			List<object> results = null;
			List<LeaveRecord> rec = null;
			bool error = false;
			string errorMessage = null;
			try
			{
				LeaveCore.Leave obj = new LeaveCore.Leave(User, null);
				obj.Load(RequestID).Change(NewStatusID, null, null);
			}
			catch (ArgumentException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (LeaveException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}
			finally
			{
				rec = LeaveCore.Leave.List(User, RequestID);
			}

			// กรณีเป็นการขอยกเลิกใบลาหลังจากอนุมัติแล้ว ให้ส่งอีเมลล์แจ้งเตือนผู้อนุมัติเพื่อให้อนุมุติ/ไม่อนุมัติ อีกรอบ
			if (!error && NewStatusID == Const.STATUS_LEAVE_CANCELREQUEST)
			{
                // ไม่ควรส่งเมลล์หาคนเดิมซ้ำๆ     
				List<string> sentAccList = new List<string>();
                MailAddress mailAcc;
                try
                {
					Grantors Grantors = new Grantors(User, rec[0].LeaveRequested.Person.PersonNo);
					if (Grantors.Heads != null)
					{
						EmailService service = new EmailService(User);
						foreach (var r in Grantors.Heads)
                        {
                            try
                            {
                                mailAcc = new MailAddress(r.HeadEmail);
                                if (sentAccList.Contains(mailAcc.Address)) // ถ้าส่งไปแล้วก็ข้ามไป
                                    continue;
                                var model = new LeaveCore.Email.Model.NotificationEmailModel(User, r.HeadPersonNo, RequestID)
                                {
                                    InternetBasedUrl = Leave.Properties.Settings.Default.InternetBasedUrl
                                };
                                service.SendEmail(r.HeadEmail, null, "แจ้งมีใบลารออนุมัติ", EmailType.Notification, model);
                                sentAccList.Add(mailAcc.Address);
                            }
                            catch { }
						}
					}
				}
				catch (Exception e) { errorMessage = e.Message; }
			}

			if (rec != null)
			{
				results = new List<object>();
				foreach (var abc in rec.OrderBy(e => e.LeaveDate))
				{
					results.Add(new
					{
						LeaveID = abc.LeaveID,
						StatusID = abc.StatusID,
						IsPending = abc.StatusID == Const.STATUS_LEAVE_PENDING_APPROVAL,
						IsAwaiting = abc.StatusID == Const.STATUS_LEAVE_AWAITING,
						IsApproved = abc.StatusID == Const.STATUS_LEAVE_APPROVED,
						LeaveAmount = abc.DisplayTotalHours,
						LeaveStatus = abc.DisplayStatusName
					});
				}
			}
			var JsonData = new { error = error, errorMessage = errorMessage, results = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		[HttpPost]
		public ActionResult AjaxChangeType(Int64 RequestID, Int64 LeaveID, int NewTypeSubID)
		{
			int records = 0;
			List<object> results = null;
			List<LeaveRecord> rec = null;
			bool error = false;
			string errorMessage = null;

			try
			{
				LeaveCore.Leave obj = new LeaveCore.Leave(User, null);
				records = obj.Load(RequestID, 0).ChangeType(NewTypeSubID);
			}
			catch (ArgumentException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (LeaveException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}
			finally
			{
				rec = LeaveCore.Leave.List(User, RequestID, 0);
			}

			if (rec != null)
			{
				results = new List<object>();
				foreach (var abc in rec.OrderBy(e => e.LeaveDate))
				{
					results.Add(new
					{
						LeaveID = abc.LeaveID,
						StatusID = abc.StatusID,
						IsCancelled = abc.StatusID == Const.STATUS_LEAVE_CANCELLED,
                        LeaveMinutes = abc.TotalMinutes,
						LeaveType = abc.LeaveRequested.TypeSubName,
						LeaveDate = abc.LeaveDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
						LeavePeriod = abc.DisplayPeriod,
						LeaveAmount = abc.DisplayTotalHours,
						LeaveStatus = abc.DisplayStatusName
					});
				}
			}
			var JsonData = new { error = error, errorMessage = errorMessage, results = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		public ActionResult AjaxChangeDate(Int64 RequestID, Int64 LeaveID, string NewLeaveDate)
		{
			int records = 0;
			object results = null;
			LeaveRecord rec = null;
			bool error = false;
			string errorMessage = null;

			try
			{
				DateTime LeaveDate = DateTime.ParseExact(NewLeaveDate, 
					"d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None);
				
				LeaveCore.Leave obj = new LeaveCore.Leave(User, null);
				records = obj.Load(RequestID, LeaveID).ChangeDate(LeaveDate);
			}
			catch (ArgumentException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (LeaveException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}
			finally
			{
				rec = LeaveCore.Leave.List(User, RequestID, LeaveID).DefaultIfEmpty(null).FirstOrDefault();
			}

			if (rec != null)
			{
				results = new
				{
					LeaveID = rec.LeaveID,
					StatusID = rec.StatusID,
					IsCancelled = rec.StatusID == Const.STATUS_LEAVE_CANCELLED,
                    LeaveMinutes = rec.TotalMinutes,
					LeaveType = rec.LeaveRequested.TypeSubName,
					LeaveDate = rec.LeaveDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeavePeriod = rec.DisplayPeriod,
					LeaveAmount = rec.DisplayTotalHours,
					LeaveStatus = rec.DisplayStatusName
				};
			}
			var JsonData = new { error = error, errorMessage = errorMessage, results = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		public ActionResult AjaxChangeTime(Int64 RequestID, Int64 LeaveID, string NewBeginTime, string NewUntilTime)
		{
			int records = 0;
			object results = null;
			LeaveRecord rec = null;
			bool error = false;
			string errorMessage = null;

			try
			{
				TimeSpan BeginTime = TimeSpan.ParseExact(NewBeginTime, "hh\\:mm", new CultureInfo("en-US"), TimeSpanStyles.None);
				TimeSpan UntilTime = TimeSpan.ParseExact(NewUntilTime, "hh\\:mm", new CultureInfo("en-US"), TimeSpanStyles.None);
				LeaveCore.Leave obj = new LeaveCore.Leave(User, null);
				records = obj.Load(RequestID, LeaveID).ChangeTime(BeginTime, UntilTime);
			}
			catch (ArgumentException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (LeaveException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}
			finally
			{
				rec = LeaveCore.Leave.List(User, RequestID, LeaveID).DefaultIfEmpty(null).FirstOrDefault();
			}

			if (rec != null)
			{
				results = new
				{
					LeaveID = rec.LeaveID,
					StatusID = rec.StatusID,
					IsCancelled = rec.StatusID == Const.STATUS_LEAVE_CANCELLED,
                    LeaveMinutes = rec.TotalMinutes,
					LeaveType = rec.LeaveRequested.TypeSubName,
					LeaveDate = rec.LeaveDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeavePeriod = rec.DisplayPeriod,
					LeaveAmount = rec.DisplayTotalHours,
					LeaveStatus = rec.DisplayStatusName
				};
			}
			var JsonData = new { error = error, errorMessage = errorMessage, results = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		public ActionResult AjaxChangeAmount(Int64 RequestID, Int64 LeaveID, decimal NewTotalHours, decimal NewTotalMinutes)
		{
			int records = 0;
			object results = null;
			LeaveRecord rec = null;
			bool error = false;
			string errorMessage = null;

			try
			{
				decimal TotalHours = NewTotalHours;
				decimal TotalMinutes = NewTotalMinutes;
                //if (NewTotalMinutes > 0)
                //{
                //    const double UnitMinutes = 3d;
                //    double Minutes = Convert.ToDouble(NewTotalMinutes);
                //    double Mod = Minutes % UnitMinutes;
                //    int Devide = Convert.ToInt32(Math.Round(Minutes / UnitMinutes));
                //    double CalcMinutes = Minutes;
                //    if (Mod != 0d)
                //    {
                //        // ปัดขึ้น ปัดลง เช่น ลา 44 นาที ... 43/3 = 14.3 ปัดลงเป็น 14 ... 14x3 = 42 นาที
                //        // หรือ          ลา 34 นาที ... 35/3 = 11.67 ปัดขึ้นเป็น 12 ... 12x3 = 36 นาที เป็นต้น
                //        CalcMinutes = (UnitMinutes * Devide);
                //    }
                //    TotalMinutes = Convert.ToDecimal(CalcMinutes / 60d);
                //    TotalHours += TotalMinutes;
                //}
				LeaveCore.Leave obj = new LeaveCore.Leave(User, null);
                records = obj.Load(RequestID, LeaveID).ChangeAmount(TotalMinutes + (TotalHours * 60));
			}
			catch (ArgumentException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (LeaveException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}
			finally
			{
				rec = LeaveCore.Leave.List(User, RequestID, LeaveID).DefaultIfEmpty(null).FirstOrDefault();
			}

			if (rec != null)
			{
				results = new
				{
					LeaveID = rec.LeaveID,
					StatusID = rec.StatusID,
					IsCancelled = rec.StatusID == Const.STATUS_LEAVE_CANCELLED,
                    LeaveMinutes = rec.TotalMinutes,
					LeaveType = rec.LeaveRequested.TypeSubName,
					LeaveDate = rec.LeaveDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeavePeriod = rec.DisplayPeriod,
					LeaveAmount = rec.DisplayTotalHours,
					LeaveStatus = rec.DisplayStatusName
				};
			}
			var JsonData = new { error = error, errorMessage = errorMessage, results = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		public ActionResult AjaxChangeStatus(Int64 RequestID, Int64 LeaveID, int NewStatusID)
		{
			object results = null;
			LeaveRecord rec = null;
			bool error = false;
			string errorMessage = null;

			try
			{
				LeaveCore.Leave obj = new LeaveCore.Leave(User, null);
				obj.Load(RequestID, LeaveID).Change(NewStatusID, null, null);
			}
			catch (ArgumentException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (LeaveException e)
			{
				error = true;
				errorMessage = e.Message;
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}
			finally
			{
				rec = LeaveCore.Leave.List(User, RequestID, LeaveID).DefaultIfEmpty(null).FirstOrDefault();
			}

			if (rec != null)
			{
				results = new
				{
					LeaveID = rec.LeaveID,
					StatusID = rec.StatusID,
					IsCancelled = rec.StatusID == Const.STATUS_LEAVE_CANCELLED,
                    LeaveMinutes = rec.TotalMinutes,
					LeaveType = rec.LeaveRequested.TypeSubName,
					LeaveDate = rec.LeaveDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeavePeriod = rec.DisplayPeriod,
					LeaveAmount = rec.DisplayTotalHours,
					LeaveStatus = rec.DisplayStatusName
				};
			}
			var JsonData = new { error = error, errorMessage = errorMessage, results = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		public ActionResult AjexListGrantViewer(Int64 LeaveID)
		{
			List<object> grants = new List<object>();
			List<object> vetoes = new List<object>();
			try
			{
				foreach (GrantRecord rec in LeaveCore.Leave.GetApprovals(User, LeaveID))
				{
					PersonRecord per = Person.GetInfo(User, rec.HeadPersonNo, null);
					grants.Add(new
					{
						HeadPersonName = per.PrefixTH + "" + per.NameFirstTH + " " + per.NameLastTH,
						GrantStepName = Const.GetStatusName(User, 
							rec.CancelStatusID.HasValue && rec.CancelStatusID != 0 
								? rec.CancelGrantStepID.HasValue && rec.CancelGrantStepID == 1
									? Const.STATUS_LEAVE_PENDING_APPROVAL
									: rec.CancelStatusID.Value
								: rec.StatusID, null),
						GrantDate = string.Format("{0}", !rec.GrantDate.HasValue ? 
							string.Empty : rec.GrantDate.Value.ToString("dd/MM/yyyy", new CultureInfo("en-US"))),
						GrantComment = rec.GrantComment ?? string.Empty
					});
				}
			} finally {}

			try
			{
				foreach (VetoRecord rec in LeaveCore.Leave.GetVetoes(User, 0, (Int64?)LeaveID))
				{
					if (rec.ActionStatus == 1)
					{
						PersonRecord per = Person.GetInfo(User, rec.HeadPersonNo, null);
						vetoes.Add(new
						{

							HeadPersonName = per.PrefixTH + "" + per.NameFirstTH + " " + per.NameLastTH,
							ActionStatusName = Const.GetStatusName(User, Const.STATUS_LEAVE_INTERRUPTED, null),
							ActionDate = string.Format("{0}", !rec.ActionDate.HasValue ?
								string.Empty : rec.ActionDate.Value.ToString("dd/MM/yyyy", new CultureInfo("en-US"))),
							Reason = rec.Reason ?? string.Empty
						});
					}
				}
			} finally {}
			
			var JsonData = new { grants = grants, vetoes = vetoes };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

        [HttpPost]
        public ActionResult AjaxGetMailLog(Int64 RequestID)
        {
            System.Text.StringBuilder html = new System.Text.StringBuilder();
            try
            {
                bool error;
                string color;
                List<EmailLogRecord> list = EmailLog.GetLog(User, RequestID);
                html.Append("<ul>");
                foreach (var rec in list)
                {
                    error = !string.IsNullOrWhiteSpace(rec.Error);
                    color = error ? "red" : "black";
                    html.Append("<li style=\"").Append(color).Append("\">")
                        .Append(rec.SendResult).Append(" - ").Append(rec.ToName);
                    if (error)
                    {
                        html.Append(" : ").Append(rec.Error);
                    }
                    html.Append(Environment.NewLine);
                }
                html.Append("</ul>");
            }
            catch { }
            return Content(html.ToString());
        }

		[HttpGet, FileDownload]
        public FilePathResult FileOf(Int64? ID)
        {
            RequestRecord head = LeaveCore.Leave.GetHeader(User, ID ?? -1);
            string name = System.IO.Path.GetFileName(head.AttachedFilepath);

            var encoding = System.Text.UnicodeEncoding.UTF8;
            Response.Charset = encoding.WebName;
            Response.HeaderEncoding = encoding;

            Response.AddHeader("Content-Disposition", string.Format("attachment; filename=\"{0}\"", (Request.Browser.Browser == "IE") ? HttpUtility.UrlEncode(name, encoding) : name));

            // send file for save dialog
            FilePathResult file = File(head.AttachedFilepath, System.Net.Mime.MediaTypeNames.Application.Octet, name);
            //Response.Flush();

            return file;
        }

    }
}