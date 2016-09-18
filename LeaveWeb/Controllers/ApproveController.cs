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
using LeaveCore;
using LeaveCore.Email.Services;

namespace Leave.Controllers
{
    public class ApproveController : Controller
    {
        //
        // GET: /Approve/

		public ApproveController()
		{
		}

		public IPrincipal CreateIdentity(string HeadPersonNo, Int64 RequestID, string Digest)
		{
			if (LeaveCore.Leave.DigestVeto(HeadPersonNo, RequestID).Equals(Digest))
			{
				string ConnStr = Tool.GetConnectionString("~", "LEAVE");
                LoginIdentity id = LoginIdentity.CreateIdentity(HeadPersonNo, "Forms", ConnStr);
				return new GenericPrincipal(id, id.Roles.ToArray());
			}
			return null;
		}

        public ActionResult Index()
        {
			if (User.Identity.IsAuthenticated)
			{
				LoginIdentity user = (LoginIdentity)User.Identity;
				Regex reg = new Regex(@"นาย|นาง|นางสาว|น.ส.");
				string Prefix = reg.Replace(user.Prefix, "คุณ");
				ViewBag.PersonNo = user.Name;
				ViewBag.PersonName = string.Concat(Prefix, user.FirstName, " ", user.LastName);
			}
			return View();
        }

		[HttpGet]
		public ActionResult Consider(Int64 RequestID, Int64 LeaveID, string HeadPersonNo, int RequestCompare, string Digest)
		{
			IPrincipal Prin = this.CreateIdentity(HeadPersonNo, RequestID, Digest);
			if (Prin == null && User.Identity.IsAuthenticated && User.Identity.Name.Equals(HeadPersonNo)) Prin = User;
			if (Prin == null) throw new SecurityException("UNAUTHORIZED");

			List<LeaveRecord> obj = null;
			if (RequestCompare == 0)  obj = LeaveCore.Leave.List(Prin, RequestID);
			if (RequestCompare == 1)  obj = LeaveCore.Leave.ListNextApprove(Prin, HeadPersonNo, RequestID);
			if (RequestCompare == -1) obj = LeaveCore.Leave.ListPrevApprove(Prin, HeadPersonNo, RequestID);
			
			PersonRecord Person = obj[0].LeaveRequested.Person;
			ViewBag.PersonNo = Person.PersonNo;
			ViewBag.EmployeeNo = Person.Employee.EmployeeNo;
			ViewBag.PersonName = Person.PrefixTH+Person.NameFirstTH+" "+Person.NameLastTH;
			ViewBag.PositionName = string.Empty;
			ViewBag.DepartmentName = Person.Employee.DisplaySection;
			ViewBag.StartingDate = Person.Employee.StartingDate.Value.Date == DateTime.MinValue.Date ? "" :
								   Person.Employee.StartingDate.Value.ToString("dd/MM/yyyy",Thread.CurrentThread.CurrentCulture);
			ViewBag.HeadPersonNo = HeadPersonNo;

			ViewBag.Digest = Digest;
			ViewBag.RequestID = obj[0].LeaveRequested.RequestID;
			ViewBag.UrlParamPrev = string.Join("/",new string[]{RequestID.ToString(),LeaveID.ToString(),HeadPersonNo,"-1",Digest});
			ViewBag.UrlParamNext = string.Join("/",new string[]{RequestID.ToString(),LeaveID.ToString(),HeadPersonNo,"1",Digest});

			List<object> rows = new List<object>();
			foreach (var rec in obj)
			{
				var Grant = LeaveCore.Leave.CurrentApproval(Prin, LeaveCore.Leave.GetApprovals(Prin, rec.LeaveID), HeadPersonNo);
				int ActiveStatusID = this.GetActiveStatusID(Grant, rec.StatusID);
				rows.Add(new
				{
					LeaveID = rec.LeaveID,
					StatusID = rec.StatusID,
					ActiveStatusID = ActiveStatusID,
					IsGrantor = Grant != null,
					IsPending = ActiveStatusID == Const.STATUS_LEAVE_PENDING_APPROVAL,
					IsAwaiting = ActiveStatusID == Const.STATUS_LEAVE_AWAITING,
					IsCancelRequest = ActiveStatusID == Const.STATUS_LEAVE_CANCELREQUEST,
					LeaveType = rec.LeaveRequested.TypeSubName,
					LeaveDate = rec.LeaveDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeavePeriod = string.Format("{0} - {1}", rec.BeginTime.Value.ToString("HH:mm"), rec.UntilTime.Value.ToString("HH:mm")),
					LeaveAmount = string.Format("{0} ({1}h)", rec.TotalDays.ToString("0.##"), rec.TotalHours.ToString("0.##")),
					LeaveStatus = Grant == null ? string.Empty : Const.GetStatusName(Prin, ActiveStatusID, null),
					ActionRequested = string.Empty,
					HeadComment = string.Empty
				});
			}
			ViewBag.JsonLocalData = new JavaScriptSerializer().Serialize(rows);
			
			return View();
		}

		[HttpPost]
		public ActionResult AjaxGetListApprove(int page, int rows, string _search, string sidx, string sord, string PersonNo)
		{
			int n = 0;
			int total = 0;
			int records = 0;
			List<object> _rows = new List<object>();
			try
			{
				List<RequestRecord> data = LeaveCore.Leave.ListApprove(User, PersonNo, page, rows);
				records = data.Count;
				if (sord != "desc") data = data.OrderBy(ko => Tool.OrderByProperty(sidx, ko)).ToList();
				else data = data.OrderByDescending(ko => Tool.OrderByProperty(sidx, ko)).ToList();
				data = data.Skip((page - 1) * rows).Take(rows).ToList();
				foreach (var rec in data)
				{
					_rows.Add(new
					{
						id = ++n,
						cell = new[] {
							rec.RequestID.ToString(),
							null,
							string.Concat(rec.Person.PrefixTH, rec.Person.NameFirstTH, " ", rec.Person.NameLastTH),
							rec.TypeSubName,
							rec.Since.Value.Date == rec.Until.Value.Date 
								? rec.Since.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture) 
								: string.Format("{0} - {1}", 
										 rec.Since.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
										 rec.Until.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture)),
							rec.DisplayPeriod,
							rec.DisplayTotalDays,
							rec.DisplayStatusName,
							null
						}
					});
				}
			}
			finally { }

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = _rows };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxVerifyRequestCompare(int RequestID, string HeadPersonNo, string Digest)
		{
			IPrincipal Prin = null;
			if (User.Identity.IsAuthenticated && User.Identity.Name.Equals(HeadPersonNo)) Prin = User;
		    if (Prin == null) Prin = this.CreateIdentity(HeadPersonNo, RequestID, Digest);
		    if (Prin == null) throw new SecurityException("UNAUTHORIZED");
			
		    bool hasNextRequested = false;
		    bool hasPrevRequested = false;
		    try
		    {
		        hasNextRequested = LeaveCore.Leave.ListNextApprove(Prin, HeadPersonNo, RequestID).Count == 1;
		        hasPrevRequested = LeaveCore.Leave.ListPrevApprove(Prin, HeadPersonNo, RequestID).Count == 1;
		    }
		    finally {}

		    var JsonData = new { hasNextRequested = hasNextRequested, hasPrevRequested = hasPrevRequested };
		    return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		[HttpPost]
		public ActionResult AjaxGrant(Int64 RequestID, Int64 LeaveID,
			string HeadPersonNo, string Digest, int NewStatusID, string HeadComment)
		{
			IPrincipal Prin = null;
			if (User.Identity.IsAuthenticated && User.Identity.Name.Equals(HeadPersonNo)) Prin = User;
		    if (Prin == null) Prin = this.CreateIdentity(HeadPersonNo, RequestID, Digest);
		    if (Prin == null) throw new SecurityException("UNAUTHORIZED");
			
			int resCount = 0;
			bool error = false;
			string errorMessage = null;
			LeaveRecord rec = null;
			try
			{
				LeaveCore.Leave obj = new LeaveCore.Leave(Prin, null);
				resCount = obj.Load(RequestID, LeaveID).Grant(HeadPersonNo, NewStatusID, HeadComment);
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
				rec = LeaveCore.Leave.List(Prin, RequestID, LeaveID).DefaultIfEmpty(null).FirstOrDefault();
			}

			if (!error && (resCount > 0 || NewStatusID == Const.STATUS_LEAVE_AWAITING))
			{
				try
				{
					string msg = null;
					if(NewStatusID == Const.STATUS_LEAVE_APPROVED) msg = "อนุมัติใบลาแล้ว";
					if(NewStatusID == Const.STATUS_LEAVE_REJECTED) msg = "ใบลาไม่ได้รับการอนุมัติ";
					if(NewStatusID == Const.STATUS_LEAVE_AWAITING) msg = "โปรดรอพิจารณาใบลา";					
					var model = new LeaveCore.Email.Model.ChangeEmailModel(
						Prin,
						RequestID,
						rec.LeaveRequested.Person.PersonNo,
						msg)
					{
						InternetBasedUrl = Leave.Properties.Settings.Default.InternetBasedUrl
					};
					EmailService service = new EmailService(Prin);
					service.SendEmail(
						rec.LeaveRequested.Person.Email,
						null,
						"แจ้งความเคลื่อนไหวใบลาของคุณ"+rec.LeaveRequested.Person.NameFirstTH+" "+rec.LeaveRequested.Person.NameLastTH,
						EmailType.Change, model);
				}
				catch (Exception e) { errorMessage = e.Message; }
			}

			object results = null;
			if (rec != null)
			{
				var Grant = LeaveCore.Leave.CurrentApproval(Prin, LeaveCore.Leave.GetApprovals(Prin, rec.LeaveID), HeadPersonNo);
				int ActiveStatusID = this.GetActiveStatusID(Grant, rec.StatusID);
				results = new
				{
					StatusID = rec.StatusID,
					ActiveStatusID = ActiveStatusID,
					DisplayStatusName = Const.GetStatusName(Prin, ActiveStatusID, null),
					IsPending = ActiveStatusID == Const.STATUS_LEAVE_PENDING_APPROVAL,
					IsAwaiting = ActiveStatusID == Const.STATUS_LEAVE_AWAITING,
					IsCancelRequest = rec.StatusID == Const.STATUS_LEAVE_CANCELREQUEST
				};
			}
			var JsonData = new { error = error, errorMessage = errorMessage, records = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		[HttpPost]
		public ActionResult AjaxGrantAll(Int64 RequestID, Int64[] LeaveIDs,
			string HeadPersonNo, string Digest, int NewStatusID, string[] HeadComments)
		{
			IPrincipal Prin = null;
			if (User.Identity.IsAuthenticated && User.Identity.Name.Equals(HeadPersonNo)) Prin = User;
		    if (Prin == null) Prin = this.CreateIdentity(HeadPersonNo, RequestID, Digest);
		    if (Prin == null) throw new SecurityException("UNAUTHORIZED");
			
			int resCount = 0;
			bool error = false;
			string errorMessage = null;
			List<LeaveRecord> rec = null;
			try
			{
				LeaveCore.Leave obj = new LeaveCore.Leave(Prin, null);
				for (int i = 0; i < LeaveIDs.Length; i++)
				if (obj.Load(RequestID, LeaveIDs[i]).Grant(HeadPersonNo, NewStatusID, HeadComments[i]) > 0) resCount++;
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
				rec = LeaveCore.Leave.List(Prin, RequestID);
			}

			if (!error && resCount > 0)
			{
				try
				{
					string msg = null;
					if(NewStatusID == Const.STATUS_LEAVE_APPROVED) msg = "อนุมัติใบลาแล้ว";
					if(NewStatusID == Const.STATUS_LEAVE_REJECTED) msg = "ใบลาไม่ได้รับการอนุมัติ";
					if(NewStatusID == Const.STATUS_LEAVE_AWAITING) msg = "โปรดรอพิจารณาใบลา";					
					var model = new LeaveCore.Email.Model.ChangeEmailModel(
						Prin,
						RequestID,
						rec[0].LeaveRequested.Person.PersonNo,
						msg)
					{
						InternetBasedUrl = Leave.Properties.Settings.Default.InternetBasedUrl
					};
					EmailService service = new EmailService(Prin);
					service.SendEmail(
						rec[0].LeaveRequested.Person.Email,
						null,
						"แจ้งความเคลื่อนไหวใบลาของคุณ"+rec[0].LeaveRequested.Person.NameFirstTH+" "+rec[0].LeaveRequested.Person.NameLastTH,
						EmailType.Change, model);
				}
				catch (Exception e) { errorMessage = e.Message; }
			}

			List<object> results = new List<object>();
			if (rec != null)
			{
				foreach (var r in rec)
				{
					var Grant = LeaveCore.Leave.CurrentApproval(Prin, LeaveCore.Leave.GetApprovals(Prin, r.LeaveID), HeadPersonNo);
					int ActiveStatusID = this.GetActiveStatusID(Grant, r.StatusID);
					results.Add(new
					{
						LeaveID = r.LeaveID,
						StatusID = r.StatusID,
						ActiveStatusID = ActiveStatusID,
						DisplayStatusName = Const.GetStatusName(Prin, ActiveStatusID, null),
						IsPending = ActiveStatusID == Const.STATUS_LEAVE_PENDING_APPROVAL,
						IsAwaiting = ActiveStatusID == Const.STATUS_LEAVE_AWAITING,
						IsCancelRequest = r.StatusID == Const.STATUS_LEAVE_CANCELREQUEST
					});
				}
			}
			var JsonData = new { error = error, errorMessage = errorMessage, records = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		private int GetActiveStatusID(GrantRecord rec, int LeaveStatusID)
		{
			int StatusID;
			if (rec == null || rec.StatusID == 0)
				return 0;
			if (LeaveStatusID == Const.STATUS_LEAVE_INTERRUPTED ||
				(LeaveStatusID == Const.STATUS_LEAVE_CANCELREQUEST && (rec.CancelStatusID == null || rec.CancelStatusID == 0))) 
				StatusID = LeaveStatusID;
			if (rec.CancelStatusID == null || rec.CancelStatusID == 0)
				return rec.StatusID;
			else return (int)rec.CancelStatusID;
		}

    }
}
