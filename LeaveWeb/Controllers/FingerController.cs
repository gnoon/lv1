using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Globalization;
using LeaveCore;

namespace Leave.Controllers
{
    public class FingerController : Controller
    {
        //
        // GET: /Finger/

        public ActionResult Index()
        {
            return View();
        }

		public ActionResult Setting()
		{
			return View();
		}

		public ActionResult Getting()
		{
			return View();
		}

		public ActionResult Exception()
		{
			return View();
		}
		
		[HttpPost]
		public ActionResult AjaxListUserID(int page, int rows, bool _search, string sidx, string sord, string filters)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();

			Dictionary<string, string> filtered = null;
			if (_search && !string.IsNullOrEmpty(filters))
			{				
				JObject jObject = JObject.Parse(filters);
				JToken  tObject = jObject["rules"];
				filtered = new Dictionary<string, string>();
				foreach (var token in tObject.ToList())
				{
					string Key = token.SelectToken("field").Value<string>();
					string Value = token.SelectToken("data").Value<string>();
					filtered.Add(Key.ToUpper(), Value);
				}
			}

			try
			{
				PersonRecord rec;
				PersonRecord identity = null;
				if(User.IsInRole(Const.ROLE_ASSTHR)) identity = Person.GetInfo(User, User.Identity.Name, null);
				string ID = Person.List(User, DateTime.Now.Year);
				var fuser = Finger.ListUserID(User);
				while ((rec = Person.Next(ID)) != null)
				{
					bool match = !User.IsInRole(Const.ROLE_ASSTHR);
					if (!match && identity != null)
					{
						match = rec.Employee.CompanyCode.Trim().Equals(identity.Employee.CompanyCode.Trim());
					}
					if(filtered != null)
					{
						match = false;
						if (filtered.ContainsKey("EMPLOYEENO") && !string.IsNullOrEmpty(rec.Employee.EmployeeNo))
						    match = rec.Employee.EmployeeNo.ToLower().IndexOf(filtered["EMPLOYEENO"].ToLower()) > -1;
						if (filtered.ContainsKey("PERSONNAME") && !string.IsNullOrEmpty(string.Concat(rec.NameFirstTH," ",rec.NameLastTH)))
						    match = string.Concat(rec.NameFirstTH," ",rec.NameLastTH).ToLower().IndexOf(filtered["PERSONNAME"].ToLower()) > -1;
						if (filtered.ContainsKey("SECTIONNAME") && !string.IsNullOrEmpty(rec.Employee.DisplaySection))
						    match = rec.Employee.DisplaySection.ToLower().IndexOf(filtered["SECTIONNAME"].ToLower()) > -1;
						if (filtered.ContainsKey("POSITIONNAME") && !string.IsNullOrEmpty(rec.Employee.PositionTH))
						    match = rec.Employee.PositionTH.ToLower().IndexOf(filtered["POSITIONNAME"].ToLower()) > -1;
					}
					if (match)
					{
						num++;
						records++;
					} else continue;
					if (num < (((page - 1) * rows) + 1) || num > (page * rows)) continue;
					results.Add(new
					{
						id = num,
						cell = new[]
						{
							rec.PersonNo,
							rec.Employee.EmployeeNo,
							rec.PrefixTH + rec.NameFirstTH + " " + rec.NameLastTH,
							rec.Employee.DisplaySection,
							rec.Employee.PositionTH,
							fuser.ContainsKey(rec.PersonNo) ? fuser[rec.PersonNo] : null
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxChangeUserID(string PersonNo, int UserID)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			object results = null;

			try
			{
				records = Finger.ChangeUserID(User, PersonNo, UserID);
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}
			
			if (records > 0)
			{	
				results = new { PersonNo = PersonNo, UserID = Finger.GetUserID(User, PersonNo)  };
			}
			
			var JsonData = new {
				error = error, errorMessage = errorMessage, records = records, results = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		[HttpPost]
		public ActionResult AjaxSetException(string oper, string PersonNo)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;

			try
			{
				if (oper.Equals("add"))
					records = Finger.AddException(User, PersonNo);
				else if (oper.Equals("del"))
					records = Finger.DeleteException(User, PersonNo);
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}
						
			var JsonData = new { error = error, errorMessage = errorMessage, records = records };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		[HttpPost]
		public ActionResult AjaxListException(int page, int rows, bool _search, string sidx, string sord, string filters)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();
			try
			{	
				var Object = Finger.ListException(User);
				records = Object.Count();
				foreach (var rec in Object.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[] {
							rec.PersonNo,
							rec.Employee.EmployeeNo,
							rec.PrefixTH + rec.NameFirstTH + " " + rec.NameLastTH,
							rec.Employee.DisplaySection,
							rec.Employee.PositionTH
                        }
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxGetData(Int64 LogID, string BeginDate, string UntilDate)
		{
			bool error = false;
			string errorMessage = null;

			try
			{
				DateTime Begin = DateTime.ParseExact(BeginDate, 
					"d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None);
				DateTime Until = DateTime.ParseExact(UntilDate, 
					"d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None);
				if (Until.Date < Begin.Date)
					throw new Exception("END_DATE_INVALID");

				CommandLine.Run(CommandLineHelper.FingerScanCommand(
					FingerScanCommand.Get, Begin, Until, LogID,
					Leave.Properties.Settings.Default.WorkingDirectory));
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}
			
			var JsonData = new { error = error, errorMessage = errorMessage };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		[HttpPost]
		public ActionResult AjaxSetLogGet(string BeginDate, string UntilDate)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			object results = null;

			Int64 Id = 0;
			DateTime Begin, Until;

			try
			{
				Id = DateTime.Now.Ticks;
				Begin = DateTime.ParseExact(BeginDate,
					"d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None);
				Until = DateTime.ParseExact(UntilDate,
					"d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None);
				if (Until.Date < Begin.Date)
					throw new Exception("END_DATE_INVALID");

				records = Finger.AddLogGet(User, Id, Begin, Until);
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}
			
			if (records > 0)
			{
				results = new { LogID = Id.ToString(), BeginDate = BeginDate, UntilDate = UntilDate };
			}
			
			var JsonData = new {
				error = error, errorMessage = errorMessage, records = records, results = results };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		[HttpPost]
		public ActionResult AjaxListLogGet(int page, int rows, bool _search, string sidx, string sord)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();
			try
			{	
				var Object = Finger.ListLogGet(User).DefaultIfEmpty(null).FirstOrDefault();
				if (Object != null)
				{
					records++;
					results.Add(new
					{
						id = ++num,
						cell = new[] {
							Object.LogID.ToString(),
							Object.BeginDate.Value.ToString("dd/MM/yyyy", new CultureInfo("en-US")),
							Object.UntilDate.Value.ToString("dd/MM/yyyy", new CultureInfo("en-US")),
							Object.LogTime.Value.ToString("dd/MM/yyyy HH:mm", new CultureInfo("en-US")),
							Object.TotalRecord.ToString("#,###"),
							Object.StatusProgress,
							Object.Person.PrefixTH + Object.Person.NameFirstTH + " " + Object.Person.NameLastTH
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxListLogGetHistory(int page, int rows, bool _search, string sidx, string sord)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();
			try
			{	
				var Object = Finger.ListLogGet(User);
				records = Object.Count();
				foreach (var rec in Object.Skip(page == 1 ? 1 : (page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[] {
							rec.BeginDate.Value.ToString("dd/MM/yyyy", new CultureInfo("en-US")),
							rec.UntilDate.Value.ToString("dd/MM/yyyy", new CultureInfo("en-US")),
							rec.LogTime.Value.ToString("dd/MM/yyyy HH:mm", new CultureInfo("en-US")),
							rec.TotalRecord.ToString("#,###"),
							rec.StatusProgress,
							rec.Person.PrefixTH + rec.Person.NameFirstTH + " " + rec.Person.NameLastTH
                        }
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		public ActionResult ExportCSV()
		{
			var sb = new StringBuilder();
			var fuser = Finger.ListUserID(User);
			
			PersonRecord rec;
			string ID = Person.List(User, DateTime.Now.Year);
			while ((rec = Person.Next(ID)) != null)
			{
				sb.AppendFormat(
					"{0},{1},{2},{3},{4},{5},{6},{7},{8}", 
					rec.PersonNo, rec.Employee.EmployeeNo, 
					rec.PrefixTH, rec.NameFirstTH, rec.NameLastTH, 
					rec.Employee.Department, rec.Employee.Section, 
					fuser.ContainsKey(rec.PersonNo) ? fuser[rec.PersonNo] : string.Empty,
					Environment.NewLine);
			}

			//return this.File(new UTF8Encoding().GetBytes(sb.ToString()), "text/csv", string.Format("Finger-{0}.csv", DateTime.Now.ToString("g").Replace("/","-").Replace(":","_").Replace(" ", "-")));
			return this.File(Encoding.GetEncoding(874).GetBytes(sb.ToString()), "text/csv", string.Format("Finger-{0}.csv", DateTime.Now.ToString("g").Replace("/","-").Replace(":","_").Replace(" ", "-")));
		}

    }
}