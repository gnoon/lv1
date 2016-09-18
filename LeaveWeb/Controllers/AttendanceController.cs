using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Globalization;
using System.Threading;
using Newtonsoft.Json.Linq;
using LeaveCore;

namespace Leave.Controllers
{
    public class AttendanceController : Controller
    {
        //
        // GET: /Attendance/

		public AttendanceController()
		{
		}

        public ActionResult Index()
        {
			if (User.Identity.IsAuthenticated)
			{
				LoginIdentity user = (LoginIdentity)User.Identity;
				ViewBag.PersonNo = user.Name;
				ViewBag.PersonName = user.Prefix + user.FirstName + " " + user.LastName;

				var Object = new int[] {
					DateTime.Now.Year - 2, DateTime.Now.Year - 1,
					DateTime.Now.Year, DateTime.Now.Year + 1, DateTime.Now.Year + 2 };
				ViewBag.YearOptions = string.Join(";", Object.Select(e => string.Concat(e.ToString(), ":", e.ToString())).ToArray());
			}
			return View();
        }

		public ActionResult Daily()
		{
			if (User.Identity.IsAuthenticated)
			{
				LoginIdentity user = (LoginIdentity)User.Identity;
				ViewBag.PersonNo = user.Name;
				ViewBag.PersonName = user.Prefix + user.FirstName + " " + user.LastName;

				var Object = new int[] {
					DateTime.Now.Year - 2, DateTime.Now.Year - 1,
					DateTime.Now.Year, DateTime.Now.Year + 1, DateTime.Now.Year + 2 };
				ViewBag.YearOptions = string.Join(";", Object.Select(e => string.Concat(e.ToString(), ":", e.ToString())).ToArray());
			}
			return View();
		}

		public ActionResult Adjusting()
		{
			DateTime? BeginDate, UntilDate;
			Dictionary<string, string> Companies = new Dictionary<string, string>();
			Dictionary<string, string> Departments = new Dictionary<string, string>();
			Dictionary<string, string> Sections = new Dictionary<string, string>();
			
			try
			{
				if (Convert.ToInt32(DateTime.Now.GetWeekOfMonth() / 2) > 1)
				{
					BeginDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 16, 0, 0, 0);
					UntilDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0).AddMonths(1).AddDays(-1);
				}
				else
				{
					BeginDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0);
					UntilDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 15, 0, 0, 0);
				}
			}
			finally
			{}

			try
			{
				PersonRecord rec;
				string ID = Person.List(User, DateTime.Now.Year);
				while ((rec = Person.Next(ID)) != null)
				{
					if (!string.IsNullOrEmpty(rec.Employee.CompanyCode.Trim()) && !Companies.ContainsKey(rec.Employee.CompanyCode.Trim()))
					{
						Companies.Add(rec.Employee.CompanyCode.Trim(), rec.Employee.CompanyCode.Trim());
					}
					if (!string.IsNullOrEmpty(rec.Employee.Department.Trim()) && !Departments.ContainsKey(rec.Employee.Department.Trim()))
					{
						Departments.Add(rec.Employee.Department.Trim(), rec.Employee.Department.Trim());
					}
					if (!string.IsNullOrEmpty(rec.Employee.Section.Trim()) && !Sections.ContainsKey(rec.Employee.Section.Trim()))
					{
						Sections.Add(rec.Employee.Section.Trim(), rec.Employee.Section.Trim());
					}
				}
			}
			catch
			{}

			ViewBag.Companies = Companies;
			ViewBag.Departments = Departments;
			ViewBag.Sections = Sections;
			ViewBag.BeginDate = BeginDate.Value.ToString("dd/MM/yyyy", new CultureInfo("en-US"));
			ViewBag.UntilDate = UntilDate.Value.ToString("dd/MM/yyyy", new CultureInfo("en-US"));
			
			return View();
		}

		public ActionResult Rechecked()
		{
			return View();
		}

		public ActionResult Consolidate()
		{
			return View();
		}

		public ActionResult Mailing()
		{
			return View();
		}

		[HttpPost]
		public ActionResult AjaxListPerson(int page, int rows, bool _search, string sidx, string sord, string filters)
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
		        string ID = Person.List(User, DateTime.Now.Year);
		        while ((rec = Person.Next(ID)) != null)
		        {
					bool match = true;
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
		                    rec.Email
		                }
		            });
		        }
		    } finally {}

		    total = (int)Math.Ceiling(records / (float)rows);
		    var JsonData = new { page = page, total = total, records = records, rows = results };
		    return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		public ActionResult AjaxListDashBoard(int page, int rows, bool _search, string sidx, string sord, string filters, string PersonNo)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();

			int tryparse, y = DateTime.Now.Year;
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
			if (filtered != null)
				if (filtered.ContainsKey("MONTH"))
					if (Int32.TryParse(filtered["MONTH"], out tryparse)) y = tryparse;


			try
			{
				AttDashBoardRecord rec;
				decimal h = Const.DEFAULT_WORKHOURS_OF_DAY;
				var Object = Attendance.ListDashBoard(User, PersonNo, y);
				foreach (var m in Enumerable.Range(1, 12).Select(e => new DateTime(DateTime.Now.Year, e, 1)))
				{
					records++;
					rec = Object.Where(e => e.Month == m.Month).DefaultIfEmpty(null).FirstOrDefault();
					if (rec == null) rec = new AttDashBoardRecord();
					
					var a = new TimeSpan(
						Convert.ToInt32((rec.LeaveSick / 60) / 8),
						Convert.ToInt32((rec.LeaveSick % (h * 60)) / 60),
						Convert.ToInt32((rec.LeaveSick % (h * 60)) % 60), 0);
					var b = new TimeSpan(
						Convert.ToInt32((rec.LeaveBusiness / 60) / 8),
						Convert.ToInt32((rec.LeaveBusiness % (h * 60)) / 60),
						Convert.ToInt32((rec.LeaveBusiness % (h * 60)) % 60), 0);
					var c = new TimeSpan(
						Convert.ToInt32((rec.LeaveVocation / 60) / 8),
						Convert.ToInt32((rec.LeaveVocation % (h * 60)) / 60),
						Convert.ToInt32((rec.LeaveVocation % (h * 60)) % 60), 0);
					var d = new TimeSpan(
						Convert.ToInt32((rec.LeaveOther / 60) / 8),
						Convert.ToInt32((rec.LeaveOther % (h * 60)) / 60),
						Convert.ToInt32((rec.LeaveOther % (h * 60)) % 60), 0);
					//var x = new TimeSpan(
					//    Convert.ToInt32((rec.LateMinutes / 60) / 8),
					//    Convert.ToInt32((rec.LateMinutes % (h * 60)) / 60),
					//    Convert.ToInt32((rec.LateMinutes % (h * 60)) % 60), 0);
					results.Add(new
					{
						
						id = ++num,
						cell = new[] {
                            y.ToString(),
							m.ToString("MMMM", new CultureInfo("th-TH")),
							a.TotalMinutes > 0 ? a.ToString(@"dd\.hh\:mm") : "-",
							b.TotalMinutes > 0 ? b.ToString(@"dd\.hh\:mm") : "-",
							c.TotalMinutes > 0 ? c.ToString(@"dd\.hh\:mm") : "-",
							d.TotalMinutes > 0 ? d.ToString(@"dd\.hh\:mm") : "-",
							rec.LateMinutes > 0 ? TimeSpan.FromMinutes((double)rec.LateMinutes).ToString(@"hh\:mm") : "-",
							rec.LateCount > 0 ? rec.LateCount.ToString("#,###") : "-"
                        }
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		public ActionResult AjaxListDaily(int page, int rows, bool _search, string sidx, string sord, string filters, string PersonNo)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();

			int y_tryparse, m_tryparse;
			int y = DateTime.Now.Year;
			int m = DateTime.Now.Month;
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
			if (filtered != null)
			{
				if (filtered.ContainsKey("YEAR"))
					if (Int32.TryParse(filtered["YEAR"], out y_tryparse)) y = y_tryparse;
				if (filtered.ContainsKey("WORKDATE"))
					if (Int32.TryParse(filtered["WORKDATE"], out m_tryparse)) m = m_tryparse;
			}

			try
			{
				WorktimeRecord rec;
				DateTime Begin = new DateTime(y, m, 1);
				DateTime Until = new DateTime(y, m, 1).AddMonths(1).AddDays(-1);
				var Object = Attendance.ListDaily(User, PersonNo, y, m);
				for (var day = Begin.Date; day <= Until.Date; day = day.AddDays(1))
				{
					records++;
					rec = Object.Where(e => e.WorkDate == day).DefaultIfEmpty(null).FirstOrDefault();
					if (rec == null) rec = new WorktimeRecord();
					results.Add(new
					{
						id = ++num,
						cell = new[] {
                            m.ToString(),
							Convert.ToInt32(day.DayOfWeek).ToString(),
							day.ToString("yyyy", new CultureInfo("en-US")),
							day.ToString("dd/MM/yyyy", new CultureInfo("en-US")),
							string.Format("วัน{0}", day.ToString("dddd", new CultureInfo("th-TH"))),
							rec.MorningIn.HasValue ? rec.MorningIn.Value.ToString("HH:mm") : null,
							rec.AfternoonOut.HasValue ? rec.AfternoonOut.Value.ToString("HH:mm") : null,
							rec.FScanIn.HasValue ? rec.FScanIn.Value.ToString("HH:mm") : null,
							rec.FScanOut.HasValue ? rec.FScanOut.Value.ToString("HH:mm") : null,
							rec.RegularMinutes > 0 ? TimeSpan.FromMinutes((double)rec.RegularMinutes).ToString(@"hh\:mm") : null,
							rec.WorkMinutes > 0 ? TimeSpan.FromMinutes((double)rec.WorkMinutes).ToString(@"hh\:mm") : null,
							rec.LateMinutes > 0 ? TimeSpan.FromMinutes((double)rec.LateMinutes).ToString(@"hh\:mm") : null,
							rec.AbsenceMinutes > 0 ? TimeSpan.FromMinutes((double)rec.AbsenceMinutes).ToString(@"hh\:mm") : null,
							rec.LeaveSick > 0 ? TimeSpan.FromMinutes((double)rec.LeaveSick).ToString(@"hh\:mm") : null,
							rec.LeaveBusiness > 0 ? TimeSpan.FromMinutes((double)rec.LeaveBusiness).ToString(@"hh\:mm") : null,
							rec.LeaveVocation > 0 ? TimeSpan.FromMinutes((double)rec.LeaveVocation).ToString(@"hh\:mm") : null,
							rec.LeaveOther > 0 ? TimeSpan.FromMinutes((double)rec.LeaveOther).ToString(@"hh\:mm") : null
                        }
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		public ActionResult AjaxListAdjusting(int page, int rows, bool _search, string sidx, string sord, string filters, string BeginDate, string UntilDate)
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
				DateTime begin, until;
				if (DateTime.TryParseExact(BeginDate, "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out begin) &&
					DateTime.TryParseExact(UntilDate, "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out until))
				{
					PersonRecord rec;
					string ID = Person.List(User, DateTime.Now.Year);
					while ((rec = Person.Next(ID)) != null)
					{
						bool match = filtered == null;
						if (filtered != null)
						{
							match = false;
							//if (filtered.ContainsKey("EMPLOYEENO") && !string.IsNullOrEmpty(rec.Employee.EmployeeNo))
							//    match = rec.Employee.EmployeeNo.ToLower().IndexOf(filtered["EMPLOYEENO"].ToLower()) > -1;
							if (filtered.ContainsKey("PERSONNAME") && !string.IsNullOrEmpty(string.Concat(rec.NameFirstTH," ",rec.NameLastTH)))
							    match = string.Concat(rec.NameFirstTH," ",rec.NameLastTH).ToLower().IndexOf(filtered["PERSONNAME"].ToLower()) > -1;
							if (filtered.ContainsKey("COMPANYNAME") && !string.IsNullOrEmpty(rec.Employee.DisplaySection))
							    match = rec.Employee.CompanyCode.ToLower().IndexOf(filtered["COMPANYNAME"].ToLower()) > -1;
							if (filtered.ContainsKey("DEPARTMENTNAME") && !string.IsNullOrEmpty(rec.Employee.DisplaySection))
							    match = rec.Employee.Department.ToLower().IndexOf(filtered["DEPARTMENTNAME"].ToLower()) > -1;
							if (filtered.ContainsKey("SECTIONNAME") && !string.IsNullOrEmpty(rec.Employee.DisplaySection))
							    match = rec.Employee.Section.ToLower().IndexOf(filtered["SECTIONNAME"].ToLower()) > -1;
						}
						if (match)
						{
							int seq = 0;
							SummaryRecord r;
							var att = Attendance.ListCheckTime(User, rec.PersonNo, begin, until);
							foreach (DateTime aDate in Tool.GetDateRange(begin, until))
							{
								seq++;
								num++;
								records++;
								if (num < (((page - 1) * rows) + 1) || num > (page * rows)) continue;

								r = att.Where(e => e.CheckDate == aDate).DefaultIfEmpty(null).FirstOrDefault();
								results.Add(new
								{
									id = num,
									cell = new[] 
									{
										r == null ? null : r.UserID.ToString(),
										rec.PersonNo,
										rec.Employee.EmployeeNo,
										Convert.ToInt32(aDate.DayOfWeek).ToString(),
										Convert.ToInt32(r != null).ToString(),
										seq.ToString(),
										rec.Employee.EmployeeNo+": "+rec.PrefixTH+rec.NameFirstTH+" "+rec.NameLastTH,
										aDate.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
										string.Format("วัน{0}", aDate.ToString("dddd", new CultureInfo("th-TH"))),
										r == null ? null : r.MorningIn.Value.ToString("HH:mm"),
										r == null ? null : r.AfternoonOut.Value.ToString("HH:mm"),
										r == null ? null : r.CheckIn.Value.ToString("HH:mm"),
										r == null ? null : r.CheckOut.Value.ToString("HH:mm"),
										//r == null ? null : r.HoursByShift.Value.ToString("HH:mm"),
										r == null ? null : r.HoursGross.Value.ToString("HH:mm")
									}
								});
							}
						}
					}
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		public ActionResult AjaxListRechecked(int page, int rows, bool _search, string sidx, string sord, string filters)
		{
			return null;
		}

		[HttpPost]
		public ActionResult AjaxLockData(Int64 LogID)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;

			try
			{
				records = Attendance.LockData(User, LogID);
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
		public ActionResult AjaxConsoData(Int64 LogID, string BeginDate, string UntilDate)
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
					FingerScanCommand.Conso, Begin, Until, LogID,
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
		public ActionResult AjaxSetLogConso(string BeginDate, string UntilDate)
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
				if (Attendance.Locked(User, Begin, Until))
					throw new Exception("DATA_LOCKED");

				records = Attendance.AddLogConso(User, Id, Begin, Until);
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

		public ActionResult AjaxListLogConso(int page, int rows, bool _search, string sidx, string sord)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();
			try
			{
				var Object = Attendance.ListLogConso(User).DefaultIfEmpty(null).FirstOrDefault();
				if (Object != null)
				{
					records++;
					results.Add(new
					{
						id = ++num,
						cell = new[] {
							Object.LogID.ToString(),
							Object.Locked.ToString(),
							Object.TotalRecord.ToString("#,###"),
							Object.BeginDate.Value.ToString("dd/MM/yyyy", new CultureInfo("en-US")),
							Object.UntilDate.Value.ToString("dd/MM/yyyy", new CultureInfo("en-US")),
							Object.LogTime.Value.ToString("dd/MM/yyyy HH:mm", new CultureInfo("en-US")),
							Object.ProgressRecord.ToString("#,###"),
							Object.Person.PrefixTH + Object.Person.NameFirstTH + " " + Object.Person.NameLastTH
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		public ActionResult AjaxListLogConsoHistory(int page, int rows, bool _search, string sidx, string sord)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();
			try
			{
				var Object = Attendance.ListLogConso(User);
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
							//rec.TotalRecord.ToString("#,###"),
							rec.ProgressRecord.ToString("#,###"),
							rec.Person.PrefixTH + rec.Person.NameFirstTH + " " + rec.Person.NameLastTH
							//rec.Locked.ToString()
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxSetLogMail(string Date, string Data)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;

			List<PersonRecord> Person = new List<PersonRecord>();;
		    if (!string.IsNullOrEmpty(Data))
		    {				
		        JObject jObject = JObject.Parse(Data);
		        JToken  tObject = jObject["rules"];
		        foreach (var token in tObject.ToList())
		        {
					Person.Add(new PersonRecord() 
					{
						PersonNo = token.SelectToken("PersonNo").Value<string>(),
						Email = token.SelectToken("EmailAddress").Value<string>()
					});
		        }
		    }

			try
			{
				DateTime ScheduleDate = DateTime.ParseExact(Date,
				    "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None);

				records = Attendance.AddLogMail(User, ScheduleDate, Person);
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}
						
			var JsonData = new {
				error = error, errorMessage = errorMessage, records = records };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		public ActionResult AjaxListLogMail(int page, int rows, bool _search, string sidx, string sord)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			List<object> results = new List<object>();
			try
			{
				var Object = Attendance.ListLogMail(User);
				records = Object.Count();
				foreach (var rec in Object.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[] {
							rec.ScheduleDate.Value.ToString("dd/MM/yyyy", new CultureInfo("en-US")),
							rec.TotalRecord.ToString("#,###"),
							rec.Sent == 1 ? "SENT" : "PENDING",
							!rec.SendTime.HasValue
								? null 
								: rec.SendTime.Value.ToString("dd/MM/yyyy HH:mm", new CultureInfo("en-US")),
							rec.LogTime.Value.ToString("dd/MM/yyyy HH:mm", new CultureInfo("en-US")),
							rec.Person.PrefixTH + rec.Person.NameFirstTH + " " + rec.Person.NameLastTH
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, records = records, rows = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxChangeTime(Int64 UserID, string PersonNo, string CheckDate, string ShiftIn, string ShiftOut, string CheckIn, string CheckOut)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;

			try
			{
				CultureInfo enLocale = new CultureInfo("en-US");
				TimeSpan _ShiftIn = TimeSpan.ParseExact(ShiftIn, "hh\\:mm", enLocale, TimeSpanStyles.None);
				TimeSpan _ShiftOut = TimeSpan.ParseExact(ShiftOut, "hh\\:mm", enLocale, TimeSpanStyles.None);
				TimeSpan _CheckIn = TimeSpan.ParseExact(CheckIn, "hh\\:mm", enLocale, TimeSpanStyles.None);
				TimeSpan _CheckOut = TimeSpan.ParseExact(CheckOut, "hh\\:mm", enLocale, TimeSpanStyles.None);
				DateTime _CheckDate = DateTime.ParseExact(CheckDate, "hh\\:mm", enLocale, DateTimeStyles.None);

				records = Attendance.ChangeTime(User, UserID, _CheckDate, _ShiftIn, _ShiftOut, _CheckIn, _CheckOut); 
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}

			var JsonData = new { error = error, errorMessage = errorMessage, records = records };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}
    
	}
}
