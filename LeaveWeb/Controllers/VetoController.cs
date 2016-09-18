using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using System.Configuration;
using System.Web.Configuration;
using System.Threading;
using System.Security;
using System.Security.Principal;
using LeaveCore;
using LeaveCore.Email.Services;

namespace Leave.Controllers
{
    public class VetoController : Controller
    {
        //
        // GET: /Veto/

		public VetoController()
		{
		}

        public ActionResult Index()
        {
            return View();
        }

		[HttpGet]
		public ActionResult Interrupt(Int64 RequestID, Int64 LeaveID, string HeadPersonNo, string Digest)
		{
			if (!User.Identity.IsAuthenticated)
				return View();
			if ((!User.Identity.Name.Equals(HeadPersonNo) && !User.IsInRole(Const.ROLE_HR)) ||
                !LeaveCore.Leave.DigestVeto(HeadPersonNo, RequestID).Equals(Digest))
				throw new SecurityException("UNAUTHORIZED");

			List<LeaveRecord> obj = LeaveCore.Leave.List(User, RequestID);
			PersonRecord Person = obj[0].LeaveRequested.Person;
			ViewBag.PersonNo = Person.PersonNo;
			ViewBag.EmployeeNo = Person.Employee.EmployeeNo;
			ViewBag.PersonName = Person.PrefixTH+Person.NameFirstTH+" "+Person.NameLastTH;
			ViewBag.PositionName = Person.Employee.PositionTH;
			ViewBag.DepartmentName = Person.Employee.DisplaySection;
			ViewBag.StartingDate = Person.Employee.StartingDate.Value.Date == DateTime.MinValue.Date ? "" :
								   Person.Employee.StartingDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture);
			ViewBag.HeadPersonNo = HeadPersonNo;

			ViewBag.Digest = Digest;
			ViewBag.RequestID = RequestID;

			List<object> results = new List<object>();
			foreach (var rec in obj.OrderBy(e => e.LeaveDate))
			{
				results.Add(new
				{
					LeaveID = rec.LeaveID,
					StatusID = rec.StatusID,
					IsAction = DateTime.Now.Subtract(rec.LeaveRequested.Since.Value).TotalDays < 3,
					IsInterrupted = rec.StatusID == Const.STATUS_LEAVE_INTERRUPTED,
					IsStatusActive = Const.LEAVE_STATUS_ACTIVE().Contains(rec.StatusID),
					LeaveDays = rec.TotalDays,
					ApplyDate = rec.LeaveRequested.ApplyDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeaveType = rec.LeaveRequested.TypeSubName,
					LeaveDate = rec.LeaveDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					LeavePeriod = rec.DisplayPeriod,
					LeaveAmount = rec.DisplayTotalHours,
					LeaveStatus = rec.DisplayStatusName,
					LeaveReason = rec.LeaveRequested.Reason,
					ActionRequested = string.Empty,
					HeadComment = string.Empty
				});
			}
			ViewBag.JsonLocalData = new JavaScriptSerializer().Serialize(results);

			return View();
		}

		[HttpPost]
		public ActionResult AjaxVeto(Int64 RequestID, Int64 LeaveID, string HeadPersonNo, string Digest, int NewStatusID, string HeadComment)
		{			
			int records = 0;
			object results = null;
			LeaveRecord rec = null;
			bool error = false;
			string errorMessage = null;
			try
			{
				LeaveCore.Leave obj = new LeaveCore.Leave(User, null);
				records = obj.Load(RequestID, LeaveID).Veto(HeadPersonNo, Digest, NewStatusID, HeadComment);
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

			if (!error && records > 0)
			{
				try
				{
					var Grantors = new Grantors(User, rec.LeaveRequested.Person.PersonNo);
					var Vetoes = new Vetoes(User, rec.LeaveRequested.Person.PersonNo);
					string toAcc = rec.LeaveRequested.Person.Email;
					if(Grantors.Heads != null)
						foreach(var a in Grantors.Heads.Where(e => !string.IsNullOrEmpty(e.HeadEmail)))
							toAcc = string.Concat(toAcc, ";", a.HeadEmail);
					if(Vetoes.Heads != null)
						foreach(var b in Vetoes.Heads.Where(e => !string.IsNullOrEmpty(e.Email)))
							toAcc = string.Concat(toAcc, ";", b.Email);
					//string cc = string.Join("; ", Grantors.Heads.Select(e => e.HeadEmail).ToArray());
					//cc = string.Concat(cc, "; ", string.Join("; ", Vetoes.Heads.Select(e => e.Email).ToArray()));
					var model = new LeaveCore.Email.Model.ChangeEmailModel(
						User,
						RequestID,
						rec.LeaveRequested.Person.PersonNo,
						NewStatusID == Const.VETO_PROCEEDING ? "ยกเลิกระงับใบลาแล้ว" : "ใบลาถูกระงับ")
					{
						InternetBasedUrl = Leave.Properties.Settings.Default.InternetBasedUrl
					};
					EmailService service = new EmailService(User);
					service.SendEmail(
						toAcc,
						null,
						"แจ้งความเคลื่อนไหวใบลาของคุณ"+rec.LeaveRequested.Person.NameFirstTH+" "+rec.LeaveRequested.Person.NameLastTH,
						EmailType.Change, model);
				}
				catch (Exception e) { errorMessage = e.Message; }
			}

			if (rec != null)
			{
				results = new
				{
					LeaveID = rec.LeaveID,
					StatusID = rec.StatusID,
					IsInterrupted = rec.StatusID == Const.STATUS_LEAVE_INTERRUPTED,
					IsStatusActive = Const.LEAVE_STATUS_ACTIVE().Contains(rec.StatusID),
					LeaveAmount = rec.DisplayTotalHours,
					LeaveStatus = rec.DisplayStatusName
				};
			}
			var JsonData = new { error = error, errorMessage = errorMessage, results = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		[HttpPost]
		public ActionResult AjaxVetoAll(Int64 RequestID, Int64[] LeaveIDs, string HeadPersonNo, string Digest, int NewStatusID, string[] HeadComments)
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
				if (obj.Load(RequestID, LeaveIDs[i]).Veto(HeadPersonNo, Digest, NewStatusID, HeadComments[i]) > 0) records ++;
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
					var Grantors = new Grantors(User, rec[0].LeaveRequested.Person.PersonNo);
					var Vetoes = new Vetoes(User, rec[0].LeaveRequested.Person.PersonNo);
					string toAcc = rec[0].LeaveRequested.Person.Email;
					if(Grantors.Heads != null)
						foreach(var a in Grantors.Heads.Where(e => !string.IsNullOrEmpty(e.HeadEmail)))
							toAcc = string.Concat(toAcc, ";", a.HeadEmail);
					if(Vetoes.Heads != null)
						foreach(var b in Vetoes.Heads.Where(e => !string.IsNullOrEmpty(e.Email)))
							toAcc = string.Concat(toAcc, ";", b.Email);
					//string cc = string.Join(";", Grantors.Heads.Select(e => e.HeadEmail).ToArray());
					//cc = string.Concat(cc, ";", string.Join(";", Vetoes.Heads.Select(e => e.Email).ToArray()));
					var model = new LeaveCore.Email.Model.ChangeEmailModel(
						User,
						RequestID,
						rec[0].LeaveRequested.Person.PersonNo,
						NewStatusID == Const.VETO_PROCEEDING ? "ยกเลิกระงับใบลาแล้ว" : "ใบลาถูกระงับ")
					{
						InternetBasedUrl = Leave.Properties.Settings.Default.InternetBasedUrl
					};
					EmailService service = new EmailService(User);
					service.SendEmail(
						toAcc,
						null,
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
					results.Add(new
					{
						LeaveID = abc.LeaveID,
						StatusID = abc.StatusID,
						IsInterrupted = abc.StatusID == Const.STATUS_LEAVE_INTERRUPTED,
						IsStatusActive = Const.LEAVE_STATUS_ACTIVE().Contains(abc.StatusID),
						LeaveAmount = abc.DisplayTotalHours,
						LeaveStatus = abc.DisplayStatusName
					});
				}
			}
			var JsonData = new { error = error, errorMessage = errorMessage, results = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

    }
}
