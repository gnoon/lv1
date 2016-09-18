using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Threading;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Configuration;
using System.Web.Configuration;
using System.Security;
using System.Security.Principal;
using System.Globalization;
using LeaveCore;
using Newtonsoft.Json.Linq;
using LeaveCore.Email.Services;

namespace Leave.Controllers
{
    public class GrantController : Controller
    {
        //
        // GET: /Grant/

		public GrantController()
		{
		}

        public ActionResult Index()
        {
			if (User.Identity.IsAuthenticated)
			{
				LoginIdentity user = (LoginIdentity)User.Identity;
				Regex reg = new Regex(@"นาย|นางสาว|นาง|น.ส.");
				string Prefix = reg.Replace(user.Prefix, "คุณ");
				ViewBag.PersonNo = user.Name;
				ViewBag.PersonName = string.Concat(Prefix, user.FirstName, " ", user.LastName);
			}
			return View();
        }

		[HttpGet]
		public ActionResult Approve(Int64 RequestID, Int64 LeaveID, string HeadPersonNo, int RequestCompare, string Digest)
		{
			if (!User.Identity.IsAuthenticated)
				return View();
			if ((!User.Identity.Name.Equals(HeadPersonNo) && !User.IsInRole(Const.ROLE_HR)) ||
                !LeaveCore.Leave.DigestGrant(HeadPersonNo, RequestID).Equals(Digest))
				throw new SecurityException("UNAUTHORIZED");

			List<LeaveRecord> obj = null;
			if (RequestCompare == 0)  obj = LeaveCore.Leave.List(User, RequestID);
			if (RequestCompare == 1)  obj = LeaveCore.Leave.ListNextApprove(User, HeadPersonNo, RequestID);
			if (RequestCompare == -1) obj = LeaveCore.Leave.ListPrevApprove(User, HeadPersonNo, RequestID);

			if (obj == null || obj.Count == 0) obj = LeaveCore.Leave.List(User, RequestID);
			
			PersonRecord Person = obj[0].LeaveRequested.Person;
			ViewBag.PersonNo = Person.PersonNo;
			ViewBag.EmployeeNo = Person.Employee.EmployeeNo;
			ViewBag.PersonName = Person.PrefixTH+Person.NameFirstTH+" "+Person.NameLastTH;
			ViewBag.PositionName = Person.Employee.PositionTH;
			ViewBag.DepartmentName = Person.Employee.DisplaySection;
			ViewBag.StartingDate = Person.Employee.StartingDate.Value.Date == DateTime.MinValue.Date ? "" :
								   Person.Employee.StartingDate.Value.ToString("dd/MM/yyyy",Thread.CurrentThread.CurrentCulture);
			ViewBag.HeadPersonNo = HeadPersonNo;

            ViewBag.RequestID = obj[0].LeaveRequested.RequestID;
            ViewBag.UrlPrevData = string.Join("/", new string[] { obj[0].LeaveRequested.RequestID.ToString(), obj[0].LeaveID.ToString(), HeadPersonNo, "-1", LeaveCore.Leave.DigestGrant(HeadPersonNo, obj[0].LeaveRequested.RequestID) });
            ViewBag.UrlNextData = string.Join("/", new string[] { obj[0].LeaveRequested.RequestID.ToString(), obj[0].LeaveID.ToString(), HeadPersonNo, "1", LeaveCore.Leave.DigestGrant(HeadPersonNo, obj[0].LeaveRequested.RequestID) });
			ViewBag.UrlLinkForm = string.Concat(Leave.Properties.Settings.Default.LinkReportForm, obj[0].LeaveRequested.RequestID);

			List<object> results = new List<object>();
			foreach (var rec in obj.OrderBy(e => e.LeaveDate))
			{
				var Grant = LeaveCore.Leave.CurrentApproval(User, LeaveCore.Leave.GetApprovals(User, rec.LeaveID), HeadPersonNo);
				int gStatusID = CurrentStatus(Grant, rec.StatusID);
				string gStatusName = Const.GetStatusName(User, gStatusID, null);
				if (rec.TotalHours == 0) gStatusName = rec.DisplayStatusName;
				results.Add(new
				{
					LeaveID = rec.LeaveID,
					StatusID = rec.StatusID,
					gStatusID = gStatusID,
					IsAction = Grant != null,
					IsPending = gStatusID == Const.STATUS_LEAVE_PENDING_APPROVAL,
					IsAwaiting = gStatusID == Const.STATUS_LEAVE_AWAITING,
					IsCancelRequest = gStatusID == Const.STATUS_LEAVE_CANCELREQUEST,
					LeaveDays = rec.TotalDays,
					ApplyDate = rec.LeaveRequested.ApplyDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeaveType = rec.LeaveRequested.TypeSubName,
					LeaveDate = rec.LeaveDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeavePeriod = rec.DisplayPeriod,
					LeaveAmount = rec.DisplayTotalHours,
					LeaveStatus = gStatusName,
					LeaveReason = rec.Comment,
					ActionRequested = string.Empty,
					HeadComment = string.Empty
				});
			}
			ViewBag.JsonLocalData = new JavaScriptSerializer().Serialize(results);
			
			return View();
		}

		[HttpPost]
		public ActionResult AjaxListApprove(int page, int rows, bool _search, string sidx, string sord, string HeadPersonNo)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();
			try
			{
				List<RequestRecord> Requested = LeaveCore.Leave.ListApprove(User, HeadPersonNo, 0, 0);
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						Requested = Requested.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						Requested = Requested.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				records = Requested.Count;
				foreach (var rec in Requested.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[] {
							rec.RequestID.ToString(),
							rec.StatusID.ToString(),
							HeadPersonNo,
							LeaveCore.Leave.DigestGrant(HeadPersonNo, rec.RequestID),
							null,
							rec.ApplyDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
							string.Concat(rec.Person.PrefixTH, rec.Person.NameFirstTH, " ", rec.Person.NameLastTH),
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
		public ActionResult AjaxVerifyRequestCompare(int RequestID, string HeadPersonNo)
		{			
		    bool hasNextRequested = false;
		    bool hasPrevRequested = false;
		    try
		    {
		        hasNextRequested = LeaveCore.Leave.ListNextApprove(User, HeadPersonNo, RequestID).Count == 1;
		        hasPrevRequested = LeaveCore.Leave.ListPrevApprove(User, HeadPersonNo, RequestID).Count == 1;
		    }
		    finally {}

		    var JsonData = new { hasNextRequested = hasNextRequested, hasPrevRequested = hasPrevRequested };
		    return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		[HttpPost]
		public ActionResult AjaxGrant(Int64 RequestID, Int64 LeaveID, string HeadPersonNo, int NewStatusID, string HeadComment)
		{			
			int records = 0;
			object results = null;
			LeaveRecord rec = null;
			bool error = false;
			string errorMessage = null;
			try
			{
				LeaveCore.Leave obj = new LeaveCore.Leave(User, null);
				records = obj.Load(RequestID, LeaveID).Grant(HeadPersonNo, NewStatusID, HeadComment);
			}
			catch (LeaveGrantException e)
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

			if (!error && (records > 0 || NewStatusID == Const.STATUS_LEAVE_AWAITING))
			{
				try
				{
					string msg = null;
					if(NewStatusID == Const.STATUS_LEAVE_APPROVED) msg = "อนุมัติใบลาแล้ว";
					if(NewStatusID == Const.STATUS_LEAVE_REJECTED) msg = "ใบลาไม่ได้รับการอนุมัติ";
					if(NewStatusID == Const.STATUS_LEAVE_AWAITING) msg = "โปรดรอพิจารณาใบลา";
					if(NewStatusID == Const.STATUS_LEAVE_CANCELAPPROVED) msg = "อนุมัติให้ยกเลิกใบลาแล้ว";
					if(NewStatusID == Const.STATUS_LEAVE_CANCELREJECTED) msg = "ใบลาไม่ได้รับการอนุมัติให้ยกเลิก";
					var model = new LeaveCore.Email.Model.ChangeEmailModel(
						User,
						RequestID,
						rec.LeaveRequested.Person.PersonNo,
						msg)
					{
						InternetBasedUrl = Leave.Properties.Settings.Default.InternetBasedUrl
					};

                    // HARDCODE เฉพาะช่วงเริ่มระบบ
                    // ให้ส่งเมลล์หา HR เพราะอยาก cross เช็คกับพนักงานด้วย
                    string HR_mails = null;
                    if (NewStatusID == Const.STATUS_LEAVE_APPROVED || NewStatusID == Const.STATUS_LEAVE_CANCELAPPROVED)
                    {
                        var bcc = Const.REQUEST_APPROVED_BCC;
                        if (!string.IsNullOrWhiteSpace(bcc))
                        {
                            if (bcc.IndexOf(",") != -1)
                                bcc = bcc.Replace(',', ';');
                            HR_mails = bcc;
                        }
                    }

					EmailService service = new EmailService(User);
					service.SendEmail(
						rec.LeaveRequested.Person.Email,
                        HR_mails,
						"แจ้งความเคลื่อนไหวใบลาของคุณ"+rec.LeaveRequested.Person.NameFirstTH+" "+rec.LeaveRequested.Person.NameLastTH,
						EmailType.Change, model);
				}
				catch (Exception e) { errorMessage = e.Message; }
			}

			if (rec != null)
			{
				var Grant = LeaveCore.Leave.CurrentApproval(User, LeaveCore.Leave.GetApprovals(User, rec.LeaveID), HeadPersonNo);
				int gStatusID = CurrentStatus(Grant, rec.StatusID);
				string gStatusName = Const.GetStatusName(User, gStatusID, null);
				if (rec.TotalHours == 0) gStatusName = rec.DisplayStatusName;
				results = new
				{
					StatusID = rec.StatusID,
					gStatusID = gStatusID,
					IsPending = gStatusID == Const.STATUS_LEAVE_PENDING_APPROVAL,
					IsAwaiting = gStatusID == Const.STATUS_LEAVE_AWAITING,
					IsCancelRequest = rec.StatusID == Const.STATUS_LEAVE_CANCELREQUEST,
					LeaveAmount = rec.DisplayTotalHours,
					LeaveStatus = gStatusName,
				};
			}
			var JsonData = new { error = error, errorMessage = errorMessage, results = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		[HttpPost]
		public ActionResult AjaxGrantAll(Int64 RequestID, Int64[] LeaveIDs, string HeadPersonNo, int NewStatusID, string[] HeadComments)
		{			
			int records = 0;
			List<object> results = null;
			List<LeaveRecord> rec = null;
			bool error = false;
			string errorMessage = null;
			try
			{
				LeaveCore.Leave obj = new LeaveCore.Leave(User, null);
				for (int i = 0; i < LeaveIDs.Length; i++)
				if (obj.Load(RequestID, LeaveIDs[i]).Grant(HeadPersonNo, NewStatusID, HeadComments[i]) > 0) records++;
			}
			catch (LeaveGrantException e)
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

			if (!error && records > 0)
			{
				try
				{
					string msg = null;
					if(NewStatusID == Const.STATUS_LEAVE_APPROVED) msg = "อนุมัติใบลาแล้ว";
					if(NewStatusID == Const.STATUS_LEAVE_REJECTED) msg = "ใบลาไม่ได้รับการอนุมัติ";
					if(NewStatusID == Const.STATUS_LEAVE_AWAITING) msg = "โปรดรอพิจารณาใบลา";
					if(NewStatusID == Const.STATUS_LEAVE_CANCELAPPROVED) msg = "อนุมัติให้ยกเลิกใบลาแล้ว";	
					if(NewStatusID == Const.STATUS_LEAVE_CANCELREJECTED) msg = "ใบลาไม่ได้รับการอนุมัติให้ยกเลิก";	
					var model = new LeaveCore.Email.Model.ChangeEmailModel(
						User,
						RequestID,
						rec[0].LeaveRequested.Person.PersonNo,
						msg)
					{
						InternetBasedUrl = Leave.Properties.Settings.Default.InternetBasedUrl
                    };

                    // HARDCODE เฉพาะช่วงเริ่มระบบ
                    // ให้ส่งเมลล์หา HR เพราะอยาก cross เช็คกับพนักงานด้วย
                    string HR_mails = null;
                    if (NewStatusID == Const.STATUS_LEAVE_APPROVED || NewStatusID == Const.STATUS_LEAVE_CANCELAPPROVED)
                    {
                        var bcc = Const.REQUEST_APPROVED_BCC;
                        if (!string.IsNullOrWhiteSpace(bcc))
                        {
                            if (bcc.IndexOf(",") != -1)
                                bcc = bcc.Replace(',', ';');
                            HR_mails = bcc;
                        }
                    }

					EmailService service = new EmailService(User);
					service.SendEmail(
						rec[0].LeaveRequested.Person.Email,
                        HR_mails,
						"แจ้งความเคลื่อนไหวใบลาของคุณ"+rec[0].LeaveRequested.Person.NameFirstTH+" "+rec[0].LeaveRequested.Person.NameLastTH,
						EmailType.Change, model);
				}
				catch (Exception e) { errorMessage = e.Message; }
			}

			if (rec != null)
			{
				results = new List<object>();
				foreach (var abc in rec.OrderBy(e => e.LeaveDate))
				{
					var Grant = LeaveCore.Leave.CurrentApproval(User, LeaveCore.Leave.GetApprovals(User, abc.LeaveID), HeadPersonNo);
					int gStatusID = CurrentStatus(Grant, abc.StatusID);
					string gStatusName = Const.GetStatusName(User, gStatusID, null);
					if (abc.TotalHours == 0) gStatusName = abc.DisplayStatusName;
					results.Add(new
					{
						LeaveID = abc.LeaveID,
						StatusID = abc.StatusID,
						gStatusID = gStatusID,
						IsPending = gStatusID == Const.STATUS_LEAVE_PENDING_APPROVAL,
						IsAwaiting = gStatusID == Const.STATUS_LEAVE_AWAITING,
						IsCancelRequest = abc.StatusID == Const.STATUS_LEAVE_CANCELREQUEST,
						LeaveAmount = abc.DisplayTotalHours,
						LeaveStatus = gStatusName
					});
				}
			}
			var JsonData = new { error = error, errorMessage = errorMessage, results = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		private static int CurrentStatus(GrantRecord rec, int StatusID)
		{
			if (rec == null || rec.StatusID == 0) return 0;
			int[] ci = new[] 
			{
				Const.STATUS_LEAVE_CANCELLED,
				Const.STATUS_LEAVE_INTERRUPTED,
				Const.STATUS_LEAVE_CANCELREQUEST
			};
			if (ci.Contains(StatusID)) return StatusID;
			if (rec.CancelStatusID > 0) return (int)rec.CancelStatusID;
			else return rec.StatusID;
		}

    }
}
