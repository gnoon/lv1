using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Reflection;
using System.Threading;
using LeaveCore;
using Leave.Models;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Leave.Controllers
{
    public class SettingController : Controller
    {
		public SettingController()
		{}

		#region Render View

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Profiles()
        {
			return View();
        }

        public ActionResult Templates()
        {
            return View();
        }

		public ActionResult Underling()
		{
			if (User.Identity.IsAuthenticated)
			{
				ViewBag.HeadPersonNo = User.Identity.Name;
			}
			return View();
		}

		#endregion

		#region Getting Data

		public ActionResult AjaxGetWorkshiftsOptions()
		{
			List<object> results = new List<object>();
			try
			{
				var obj = Setting.ListWorkshifts(User);
				foreach(var rec in obj)
			    {
			        var d = Setting.ListWorkshift(User, rec.WorkshiftsID);
					if (d.Count > 0)
					{
						results.Add(new
						{
							value = rec.WorkshiftsID,
							label = rec.Description,
							detailed = new List<object>()
								{
									new 
									{
										type  = d[0].Type,
										label = d[0].Description,
										begin = d[0].TimeBegin.Value.ToString("HH:mm"),
										until = d[0].TimeUntil.Value.ToString("HH:mm")
									
									},
									new
									{
										type  = d[1].Type,
										label = d[1].Description,
										begin = d[1].TimeBegin.Value.ToString("HH:mm"),
										until = d[1].TimeUntil.Value.ToString("HH:mm")
									}
								}
						});
					}
			    }

			} finally {}

			var JsonData = new { results = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		public ActionResult AjaxGetPersonAutoComplete()
		{
			List<object> persons = new List<object>();
			List<object> companies = new List<object>();
			List<object> departments = new List<object>();
			List<object> sections = new List<object>();
			try
			{
				PersonRecord rec;
				string ID = Person.List(User, DateTime.Now.Year);
				while ((rec = Person.Next(ID)) != null)
				{
					persons.Add(new
					{
						value = rec.PersonNo,
						label = rec.PrefixTH + rec.NameFirstTH + " " + rec.NameLastTH + " (" + rec.Employee.EmployeeNo + ")"
					});
					if (!string.IsNullOrEmpty(rec.Employee.CompanyCode.Trim()))
					{
						companies.Add(new
						{
							value = rec.Employee.CompanyCode.Trim(),
							label = rec.Employee.CompanyCode.Trim()
						});
					}
					if (!string.IsNullOrEmpty(rec.Employee.Department.Trim()))
					{
						departments.Add(new
						{
							value = rec.Employee.Department.Trim(),
							label = rec.Employee.Department.Trim()
						});
					}
					if (!string.IsNullOrEmpty(rec.Employee.Section.Trim()))
					{
						sections.Add(new
						{
							value = rec.Employee.Section.Trim(),
							label = rec.Employee.Section.Trim()
						});
					}
				}
			} finally {}

			var JsonData = new { results = new { persons = persons,
				companies = companies.Distinct().OrderBy(e => e.GetType().GetProperty("label").GetValue(e, null)),
				departments = departments.Distinct().OrderBy(e => e.GetType().GetProperty("label").GetValue(e, null)),
				sections = sections.Distinct().OrderBy(e => e.GetType().GetProperty("label").GetValue(e, null)) }};
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		#endregion

		#region Profiles Setting

		[HttpPost]
		public ActionResult AjaxGetPerson(int page, int rows, bool _search, string sidx, string sord, string filters)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			int userpage = 0;
			int userselid = 0;
			List<object> results = new List<object>();

			string _PersonNo = "", _EmployeeNo = "";
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
				// when added new
				if (filtered.ContainsKey("_PERSONNO") && filtered.ContainsKey("_EMPLOYEENO"))
				{
					if (filtered.ContainsKey("_PERSONNO")) _PersonNo = filtered["_PERSONNO"];
					if (filtered.ContainsKey("_EMPLOYEENO")) _EmployeeNo = filtered["_EMPLOYEENO"];
				}
			}

			try
			{
				PersonRecord rec;
				PersonRecord identity = null;
				if(User.IsInRole(Const.ROLE_ASSTHR)) identity = Person.GetInfo(User, User.Identity.Name, null);
				string ID = Person.List(User, DateTime.Now.Year);
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
						// when added new
						if (!string.IsNullOrEmpty(_PersonNo) && !string.IsNullOrEmpty(_EmployeeNo))
						    match = rec.PersonNo.Trim().Equals(_PersonNo.Trim()) && 
									rec.Employee.EmployeeNo.Trim().Equals(_EmployeeNo.Trim());
						// when filter toolbar
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
					// Finding selected row
					if (!string.IsNullOrEmpty(_PersonNo) && !string.IsNullOrEmpty(_EmployeeNo))
					{
						if (rec.PersonNo.Trim().Equals(_PersonNo.Trim()) && rec.Employee.EmployeeNo.Trim().Equals(_EmployeeNo.Trim()))
						{
							userpage = (int)Math.Ceiling(num / (float)rows);
							userselid = num;
							page = userpage;
						}
					}
					results.Add(new
					{
						id = num,
						cell = new[]
						{
							rec.PersonNo,
							rec.PrefixTH,
							rec.NameFirstTH,
							rec.NameLastTH,
							rec.Gender,
							rec.Email,
							rec.Password,
							string.Format("{0}", !rec.Employee.StartingDate.HasValue || rec.Employee.StartingDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture) == DateTime.MinValue.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture)
								? string.Empty
								: rec.Employee.StartingDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture)),
							string.Format("{0}", !rec.Employee.UntilDate.HasValue || rec.Employee.UntilDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture) == DateTime.MaxValue.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture)
								? string.Empty
								: rec.Employee.UntilDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture)),
							rec.Employee.PositionTH,
							rec.Employee.CompanyCode,
							rec.Employee.Department,
							rec.Employee.Section,
							rec.Employee.EmployeeNo,
							rec.PrefixTH + rec.NameFirstTH + " " + rec.NameLastTH,
							rec.Employee.DisplaySection,
							rec.Employee.PositionTH
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, 
				records = records, rows = results, userdata = new { page = userpage, selId = userselid }};
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxSetPerson(string Oper, string PersonNo, string OldEmployeeNo, string NewEmployeeNo, string PrefixTH, string NameFirstTH, string NameLastTH, string Gender, string Email, string Password, string StartingDate, string UntilDate, string PositionTH, string CompanyCode, string Department, string Section)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			try
			{
				DateTime temp;
				DateTime _StartingDate = new DateTime(1900,1,1);
				DateTime _UntilDate = DateTime.MaxValue;
				if (DateTime.TryParseExact(StartingDate, "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out temp))
					_StartingDate = temp;
				if (DateTime.TryParseExact(UntilDate, "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out temp))
					_UntilDate = temp;

				if (Oper.Equals("add"))
				{
					records = Person.New(User, PersonNo, NewEmployeeNo, PrefixTH, NameFirstTH, NameLastTH, Gender, Email, Password, _StartingDate, _UntilDate, PositionTH, CompanyCode, Department, Section);
				}
				else if (Oper.Equals("edit"))
				{
					records = Person.Change(User, PersonNo, OldEmployeeNo, NewEmployeeNo, PrefixTH, NameFirstTH, NameLastTH, Gender, Email, Password, _StartingDate, _UntilDate, PositionTH, CompanyCode, Department, Section);
				}
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
		public ActionResult AjaxGetPersonNo(string FirstName, string LastName)
		{
			bool error = false;
			string errorMessage = null;
			bool existing = false;
			string PersonNo = string.Empty;
			try
			{
				PersonNo = Person.GenPersonNo(User, FirstName, LastName);
				existing = Person.CheckPersonNo(User, PersonNo);
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}

			var JsonData = new { error = error, errorMessage = errorMessage, existing = existing, PersonNo = PersonNo };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		[HttpPost]
		public ActionResult AjaxSetUserAccount(string PersonNo, string Email, string Password, int UsePasswordDefault)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			try
			{
				records = Person.SetUserAccount(User, PersonNo, Email, Password, UsePasswordDefault);

                // เก็บ Log การเซ็ต Password ลงตาราง [LV Action Log]
                ActionLog.New(User, LeaveCore.Action.SetPassword, PersonNo, null);
			}
			catch (Exception e)
			{
			    error = true;
			    errorMessage = e.Message;
			}

			var JsonData = new { error = error, errorMessage = errorMessage, records = records };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		public ActionResult AjaxGetUnderling(int page, int rows, bool _search, string sidx, string sord, string filters, string HeadPersonNo)
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
					filtered.Add(Key.ToUpper(), Value.ToLower());
				}
			}

			try
			{
				foreach(var rec in Person.ListUnderling(User, HeadPersonNo))
				{
					bool match = true;
					if(filtered != null)
					{
						match = false;
						if (filtered.ContainsKey("EMPLOYEENO") && !string.IsNullOrEmpty(rec.Employee.EmployeeNo))
							match = rec.Employee.EmployeeNo.ToLower().IndexOf(filtered["EMPLOYEENO"]) > -1;
						if (filtered.ContainsKey("PERSONNAME") && !string.IsNullOrEmpty(string.Concat(rec.NameFirstTH," ",rec.NameLastTH)))
							match = string.Concat(rec.NameFirstTH," ",rec.NameLastTH).ToLower().IndexOf(filtered["PERSONNAME"]) > -1;
						if (filtered.ContainsKey("SECTIONNAME") && !string.IsNullOrEmpty(rec.Employee.DisplaySection))
							match = rec.Employee.DisplaySection.ToLower().IndexOf(filtered["SECTIONNAME"]) > -1;
						if (filtered.ContainsKey("POSITIONNAME") && !string.IsNullOrEmpty(rec.Employee.PositionTH))
							match = rec.Employee.PositionTH.ToLower().IndexOf(filtered["POSITIONNAME"]) > -1;
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
							rec.PrefixTH + "" + rec.NameFirstTH + " " + rec.NameLastTH,
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

		public ActionResult AjaxGetPersonInfo(string PersonNo)
		{
			bool error = false;
			string errorMessage = null;
			object results = new object();
			try
			{
				PersonRecord obj = Person.GetInfo(User, PersonNo, null);
				var eAddr = obj.Email.Split(';');
				results = new
				{
					PersonNo = obj.PersonNo,
					EmployeeNo = obj.Employee.EmployeeNo,
					PersonName = obj.PrefixTH + obj.NameFirstTH + " " + obj.NameLastTH,
					PositionName = obj.Employee.PositionTH,
					SectionName = obj.Employee.DisplaySection,
					StartingDate = obj.Employee.StartingDate.Value.Date == DateTime.MinValue.Date ? "" :
								   obj.Employee.StartingDate.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
					Email = eAddr[0],
					EmailSecond = eAddr.Length > 1 ? eAddr[1] : string.Empty,
					ADAccount = obj.ADAccount,
					Password = obj.Password,
					UsePasswordDefault = obj.UsePasswordDefault
				};
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}

			var JsonData = new { error = error, errorMessage = errorMessage, records = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		public ActionResult AjaxGetProfileQuota(int page, int rows, bool _search, string sidx, string sord, string PersonNo)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			int userpage = 0;
			int userselid = 0;
			List<object> results = new List<object>();
			try
			{
				Quota obj = new Quota(User, PersonNo);
				string[] filtered = new[] { Const.TYPE_NO_SICK, Const.TYPE_NO_BUSINESS, Const.TYPE_NO_VACATION };
				obj.OfLeaveType = obj.OfLeaveType.Where(e => filtered.Contains(e.LeaveType.TypeNo)).ToList();
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						obj.OfLeaveType = obj.OfLeaveType.OrderBy(e => Tool.OrderByProperty(sidx, e) ?? Tool.OrderByProperty(sidx, e.LeaveType)).ToList();
					else
						obj.OfLeaveType = obj.OfLeaveType.OrderByDescending(e => Tool.OrderByProperty(sidx, e) ?? Tool.OrderByProperty(sidx, e.LeaveType)).ToList();
				}
				//if (!string.IsNullOrEmpty(KeyPair) && !string.IsNullOrEmpty(ValuePair))
				//{
				//    int fIndex = obj.OfLeaveType.FindIndex(e => Tool.FindIndexProperty(KeyPair, ValuePair, e));
				//    if (fIndex > -1)
				//    {
				//        userpage = (int)Math.Ceiling((fIndex + 1) / (float)rows);
				//        userselid = (int)((fIndex + 1) % (float)rows);
				//        userselid = userselid == 0 ? rows : userselid;
				//        page = userpage;
				//    }
				//}
				records = obj.OfLeaveType.Count;
				decimal h = Const.DEFAULT_WORKHOURS_OF_DAY;
				foreach (var rec in obj.OfLeaveType.Skip((page - 1) * rows).Take(rows))
				{
					var a = TimeSpan.FromDays((double)rec.TakenAmount);
					var b = TimeSpan.FromDays((double)rec.ApproveAmount);
					var c = TimeSpan.FromDays((double)rec.BalanceAmount);
					var d = TimeSpan.FromDays((double)(rec.QuotaAmount + rec.QuotaPrevAmount));
					results.Add(new
					{
						id = ++num,
						cell = new[]
						{
							rec.LeaveType.TypeNo,
							rec.LeaveType.NameTH,
							d.TotalMinutes > 0 && d.TotalDays < Const.QUOTA_UNLIMITED
								? string.Format("{0} ({1}h)", d.Days.ToString(), (d.Days * h).ToString("0.#####"))
								: (d.TotalDays < Const.QUOTA_UNLIMITED ? "-" : "มีสิทธิลา"),
							a.TotalMinutes > 0 && a.TotalDays < Const.QUOTA_UNLIMITED
								? new TimeSpan(a.Days,
									Convert.ToInt32(((a.Hours % h) * 60) / 60),
									Convert.ToInt32(((a.Hours % h) * 60) % 60), 0).ToString(@"dd\.hh\:mm")
								: "-",
							b.TotalMinutes > 0 && b.TotalDays < Const.QUOTA_UNLIMITED
								? new TimeSpan(b.Days,
									Convert.ToInt32(((b.Hours % h) * 60) / 60),
									Convert.ToInt32(((b.Hours % h) * 60) % 60), 0).ToString(@"dd\.hh\:mm")
								: "-",
							Math.Abs(c.TotalMinutes) > 0 && Math.Abs(c.TotalDays) < Const.QUOTA_UNLIMITED
								? new TimeSpan(Math.Abs(c.Days),
									Convert.ToInt32(((Math.Abs(c.Hours) % h) * 60) / 60),
									Convert.ToInt32(((Math.Abs(c.Hours) % h) * 60) % 60), 0)
									.ToString(string.Concat((c < TimeSpan.Zero ? "\\-" : ""), "dd\\.hh\\:mm"))
								: "-"
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total,
				records = records, rows = results, userdata = new { page = userpage, selId = userselid }};
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxSetProfileQuota(string oper, string id, string PersonNo, string TypeNo, decimal QuotaAmount)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			try
			{
				//if (oper.Equals("add"))
				//{
				//    records = Quota.AddQuota(User, PersonNo);
				//}
				//else if (oper.Equals("edit"))
				//{
				//    records = Quota.EditQuota(User, id, PersonNo);
				//}
				//else if (oper.Equals("del"))
				//{
				//    records = Quota.DeleteQuota(User, PersonNo);
				//}
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}

			var JsonData = new { error = error, errorMessage = errorMessage, records = records };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		public ActionResult AjaxGetProfileGrantors(int page, int rows, bool _search, string sidx, string sord, string PersonNo, string KeyPair, string ValuePair)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			int userpage = 0;
			int userselid = 0;
			List<object> results = new List<object>();
			try
			{
				Grantors obj = new Grantors(User, PersonNo);
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						obj.Heads = obj.Heads.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						obj.Heads = obj.Heads.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				if (!string.IsNullOrEmpty(KeyPair) && !string.IsNullOrEmpty(ValuePair))
				{
					int fIndex = obj.Heads.FindIndex(e => Tool.FindIndexProperty(KeyPair, ValuePair, e));
					if (fIndex > -1)
					{
						userpage = (int)Math.Ceiling((fIndex + 1) / (float)rows);
						userselid = (int)((fIndex + 1) % (float)rows);
						userselid = userselid == 0 ? rows : userselid;
						page = userpage;
					}
				}
				records = obj.Heads.Count;
				foreach (var rec in obj.Heads.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[]
						{
							rec.PersonNo,
							rec.HeadPersonNo,
							rec.Priority.ToString(),
							rec.Employee.EmployeeNo,
							rec.HeadPrefixTH + rec.HeadNameFirstTH + " " + rec.HeadNameLastTH,
							rec.Employee.DisplaySection,
							rec.Employee.PositionTH
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, 
				records = records, rows = results, userdata = new { page = userpage, selId = userselid }};
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxSetProfileGrantors(string oper, string id, string PersonNo, string HeadPersonNo, int Priority)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			try
			{
				if (oper.Equals("add"))
				{
					records = Grantors.AddGrantor(User, PersonNo, HeadPersonNo, Priority);
				}
				else if (oper.Equals("edit"))
				{
					records = Grantors.EditGrantor(User, id, PersonNo, HeadPersonNo, Priority);
				}
				else if (oper.Equals("del"))
				{
					records = Grantors.DeleteGrantor(User, PersonNo, HeadPersonNo, Priority);
				}
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}

			var JsonData = new { error = error, errorMessage = errorMessage, records = records };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		public ActionResult AjaxGetProfileVetoes(int page, int rows, bool _search, string sidx, string sord, string PersonNo, string KeyPair, string ValuePair)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			int userpage = 0;
			int userselid = 0;
			List<object> results = new List<object>();
			try
			{
				Vetoes obj = new Vetoes(User, PersonNo);
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						obj.Heads = obj.Heads.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						obj.Heads = obj.Heads.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				if (!string.IsNullOrEmpty(KeyPair) && !string.IsNullOrEmpty(ValuePair))
				{
					int fIndex = obj.Heads.FindIndex(e => Tool.FindIndexProperty(KeyPair, ValuePair, e));
					if (fIndex > -1)
					{
						userpage = (int)Math.Ceiling((fIndex + 1) / (float)rows);
						userselid = (int)((fIndex + 1) % (float)rows);
						userselid = userselid == 0 ? rows : userselid;
						page = userpage;
					}
				}
				records = obj.Heads.Count;
				foreach (var rec in obj.Heads.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[]
						{
							obj.PersonNo,
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
			var JsonData = new { page = page, total = total, 
				records = records, rows = results, userdata = new { page = userpage, selId = userselid }};
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxSetProfileVetoes(string oper, string id, string PersonNo, string HeadPersonNo)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			try
			{
				if (oper.Equals("add"))
				{
					records = Vetoes.AddVetoes(User, PersonNo, HeadPersonNo);
				}
				else if (oper.Equals("edit"))
				{
					records = Vetoes.EditVetoes(User, id, PersonNo, HeadPersonNo);
				}
				else if (oper.Equals("del"))
				{
					records = Vetoes.DeleteVetoes(User, PersonNo, HeadPersonNo);
				}
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}

			var JsonData = new { error = error, errorMessage = errorMessage, records = records };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		public ActionResult AjaxGetProfileWorkshifts(int page, int rows, bool _search, string sidx, string sord, string PersonNo, string KeyPair, string ValuePair)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			int userpage = 0;
			int userselid = 0;
			List<object> results = new List<object>();
			try
			{
				Workshifts obj = new Workshifts(User, PersonNo, false);
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						obj.Times = obj.Times.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						obj.Times = obj.Times.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				if (!string.IsNullOrEmpty(KeyPair) && !string.IsNullOrEmpty(ValuePair))
				{
					int fIndex = obj.Times.FindIndex(e => Tool.FindIndexProperty(KeyPair, ValuePair, e));
					if (fIndex > -1)
					{
						userpage = (int)Math.Ceiling((fIndex + 1) / (float)rows);
						userselid = (int)((fIndex + 1) % (float)rows);
						userselid = userselid == 0 ? rows : userselid;
						page = userpage;
					}
				}
				records = obj.Times.Count;
				foreach (var rec in obj.Times.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[]
						{
							rec.PersonNo,
							rec.Type.ToString(),
							rec.Remark,
							rec.TimeBegin.Value.ToString("HH:mm"),
							rec.TimeUntil.Value.ToString("HH:mm"),
							string.Format("{0}h", rec.TimeUntil.Value.Subtract(rec.TimeBegin.Value).Hours.ToString("0.#####"))
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, 
				records = records, rows = results, userdata = new { page = userpage, selId = userselid }};
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxSetProfileWorkshifts(string oper, string id, string PersonNo, string Times)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;

			try
			{
				WorkshiftRecord obj;
				JObject jObject = null;
				JToken  tObject = null;
				if (!string.IsNullOrEmpty(Times))
				{
					jObject = JObject.Parse(Times);
					tObject = jObject["rules"];
				}

				if (oper.Equals("add"))
				{			
					List<WorkshiftRecord> rec = new List<WorkshiftRecord>();
					foreach (var token in tObject.ToList())
					{
						obj = new WorkshiftRecord();
						obj.Type = token.SelectToken("type").Value<Int32>();
						obj.Remark = token.SelectToken("label").Value<string>();
						obj.TimeBegin = token.SelectToken("begin").Value<DateTime?>();
						obj.TimeUntil = token.SelectToken("until").Value<DateTime?>();
						rec.Add(obj);
					}
					records = Workshifts.AddWorkshift(User, PersonNo, rec);
				}
				else if (oper.Equals("edit"))
				{
					obj = new WorkshiftRecord();
					obj.Type = tObject.SelectToken("type").Value<Int32>();
					obj.TimeBegin = tObject.SelectToken("begin").Value<DateTime?>();
					obj.TimeUntil = tObject.SelectToken("until").Value<DateTime?>();
					records = Workshifts.EditWorkshift(User, PersonNo, obj.Type, (DateTime)obj.TimeBegin, (DateTime)obj.TimeUntil);
				}
				else if (oper.Equals("del"))
				{
					records = Workshifts.DeleteWorkshift(User, PersonNo);
				}
			}
			catch (Exception e)
			{
			    error = true;
			    errorMessage = e.Message;
			}

			var JsonData = new { error = error, errorMessage = errorMessage, records = records };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		public ActionResult AjaxGetProfileHolidays(int page, int rows, bool _search, string sidx, string sord, string PersonNo, string KeyPair, string ValuePair)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			int userpage = 0;
			int userselid = 0;
			List<object> results = new List<object>();
			try
			{
				Holidays obj = new Holidays(User, PersonNo);
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						obj.Dates = obj.Dates.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						obj.Dates = obj.Dates.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				if (!string.IsNullOrEmpty(KeyPair) && !string.IsNullOrEmpty(ValuePair))
				{
				    int fIndex = obj.Dates.FindIndex(e => Tool.FindIndexProperty(KeyPair, ValuePair, e));
				    if (fIndex > -1)
				    {
				        userpage = (int)Math.Ceiling((fIndex + 1) / (float)rows);
				        userselid = (int)((fIndex + 1) % (float)rows);
				        userselid = userselid == 0 ? rows : userselid;
				        page = userpage;
				    }
				}
				records = obj.Dates.Count;
				foreach (var rec in obj.Dates.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[]
						{
							rec.PersonNo,
							rec.Date.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
							rec.NameTH,
							rec.NameEN,
							rec.Remark
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, 
				records = records, rows = results, userdata = new { page = userpage, selId = userselid }};
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		public ActionResult AjaxSetProfileHolidays(string oper, string id, string PersonNo, string Date, string NameTH, string NameEN, string Remark)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			try
			{
				DateTime? aDate = null;
				DateTime? dDate = null;
				if(!string.IsNullOrEmpty(id))
					aDate = DateTime.ParseExact(id, "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None);  
				if(!string.IsNullOrEmpty(Date))
					dDate = DateTime.ParseExact(Date, "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None);  
				
				if (oper.Equals("add"))
				{
				    records = Holidays.AddHolidays(User, PersonNo, (DateTime)dDate, NameTH, NameEN, Remark);
				}
				else if (oper.Equals("edit"))
				{
				    records = Holidays.EditHolidays(User, (DateTime)aDate, PersonNo, (DateTime)dDate, NameTH, NameEN, Remark);
				}
				else if (oper.Equals("del"))
				{
				    records = Holidays.DeleteHolidays(User, PersonNo, (DateTime)aDate);
				}
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}

			var JsonData = new { error = error, errorMessage = errorMessage, records = records };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		public ActionResult AjaxGetProfileWeekends(int page, int rows, bool _search, string sidx, string sord, string PersonNo, string KeyPair, string ValuePair)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			int userpage = 0;
			int userselid = 0;
			List<object> results = new List<object>();
			try
			{
				Weekends obj = new Weekends(User, PersonNo);
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						obj.Rules = obj.Rules.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						obj.Rules = obj.Rules.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				if (!string.IsNullOrEmpty(KeyPair) && !string.IsNullOrEmpty(ValuePair))
				{
				    int fIndex = obj.Rules.FindIndex(e => Tool.FindIndexProperty(KeyPair, ValuePair, e));
				    if (fIndex > -1)
				    {
				        userpage = (int)Math.Ceiling((fIndex + 1) / (float)rows);
				        userselid = (int)((fIndex + 1) % (float)rows);
				        userselid = userselid == 0 ? rows : userselid;
				        page = userpage;
				    }
				}
				records = obj.Rules.Count;
				foreach (var rec in obj.Rules.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[]
						{
							rec.PersonNo,
							Convert.ToInt32(rec.DayOfWeek).ToString(),
							rec.EveryNWeekOfMonth.ToString(),
							rec.StartingWeekOfMonth.ToString(),
							rec.ExcludeWeekOfMonth.ToString(),
							string.Format("วัน{0}", Convert.ToInt32(
								rec.DayOfWeek).GetDateOfWeek().ToString("dddd", new CultureInfo("th-TH"))),
							null,
							null,
							null,
							rec.Remark
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, 
				records = records, rows = results, userdata = new { page = userpage, selId = userselid }};
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxSetProfileWeekends(string oper, int id, string PersonNo, int DayOfWeek, int StartingWeekOfMonth, int EveryNWeekOfMonth, int ExcludeWeekOfMonth, string NameTH, string Remark)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			try
			{
				if (oper.Equals("add"))
				{
					records = Weekends.AddWeekend(User, PersonNo, DayOfWeek, StartingWeekOfMonth, EveryNWeekOfMonth, ExcludeWeekOfMonth, NameTH, Remark);
				}
				else if (oper.Equals("edit"))
				{
					records = Weekends.EditWeekend(User, id, PersonNo, DayOfWeek, StartingWeekOfMonth, EveryNWeekOfMonth, ExcludeWeekOfMonth, NameTH, Remark);
				}
				else if (oper.Equals("del"))
				{
					records = Weekends.DeleteWeekend(User, PersonNo, DayOfWeek);
				}
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}

			var JsonData = new { error = error, errorMessage = errorMessage, records = records };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		public ActionResult AjaxGetProfileWeekendsMovement(int page, int rows, bool _search, string sidx, string sord, string PersonNo, string KeyPair, string ValuePair)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			int userpage = 0;
			int userselid = 0;
			List<object> results = new List<object>();
			try
			{
				Weekends obj = new Weekends(User, PersonNo);
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
				    if (!sord.Equals("desc"))
				        obj.Moves = obj.Moves.OrderBy(e => Tool.OrderByProperty(sidx, e.Value.Value)).ToList();
				    else
				        obj.Moves = obj.Moves.OrderByDescending(e => Tool.OrderByProperty(sidx, e.Value.Value)).ToList();
				}
				if (!string.IsNullOrEmpty(KeyPair) && !string.IsNullOrEmpty(ValuePair))
				{
				    int fIndex = obj.Moves.FindIndex(e => Tool.FindIndexProperty(KeyPair, ValuePair, e.Value.Value));
				    if (fIndex > -1)
				    {
				        userpage = (int)Math.Ceiling((fIndex + 1) / (float)rows);
				        userselid = (int)((fIndex + 1) % (float)rows);
				        userselid = userselid == 0 ? rows : userselid;
				        page = userpage;
				    }
				}
				records = obj.Moves.Count;
				foreach (var rec in obj.Moves.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[]
						{
							obj.PersonNo,
							rec.Value.Key.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
							rec.Key.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture),
							rec.Value.Value
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, 
				records = records, rows = results, userdata = new { page = userpage, selId = userselid }};
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxSetProfileWeekendsMovement(string oper, string id, string id1, string PersonNo, string FromDate, string ToDate, string Remark)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			try
			{
				DateTime? aDate = null;
				DateTime? dDate = null;
				if(!string.IsNullOrEmpty(id))
					aDate = DateTime.ParseExact(id, "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None);  
				if(!string.IsNullOrEmpty(id1))
					dDate = DateTime.ParseExact(id1, "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None);  

				DateTime aFromDate = DateTime.ParseExact(FromDate, "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None);
				DateTime aToDate = DateTime.ParseExact(ToDate, "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None);

				if (oper.Equals("del"))
				{
					records = Weekends.DeleteWeekendMovement(User, PersonNo, aFromDate, aToDate);
				}
				else
				{
				    Holidays Holidays = new Holidays(User, PersonNo);
				    Weekends Weekends = new Weekends(User, PersonNo);
				    if (oper.Equals("add"))
				    {
						// ตรวจสอบวันที่ย้ายมา เป็นวันที่หยุดหรือไม่
						if (!Holidays.IsHoliday(aFromDate) && !Weekends.IsWeekend(aFromDate))
							throw new Exception("FROMDATE_INVALID");
						// ตรวจสอบวันที่จะย้ายไป เป็นวันทำงานหรือไม่
						if (Holidays.IsHoliday(aToDate) || Weekends.IsWeekend(aToDate))
							throw new Exception("TODATE_INVALID");

						records = Weekends.AddWeekendMovement(User, PersonNo, aFromDate, aToDate, Remark);
				    }
				    else if (oper.Equals("edit"))
				    {
						// ตรวจสอบวันที่ย้ายมา เป็นวันที่หยุดหรือไม่
						if (!Holidays.IsHoliday(aFromDate) && !Weekends.IsWeekend(aFromDate))
						{
							// ตรวจสอบว่าอยู่ในรายการเปลี่ยนวันหยุดหรือไม่
							if (!Weekends.Moves.Exists(e => e.Value.Key.Date == aFromDate))
								throw new Exception("FROMDATE_INVALID");
						}
						// ตรวจสอบวันที่จะย้ายไป เป็นวันทำงานหรือไม่
						if (Holidays.IsHoliday(aToDate) || Weekends.IsWeekend(aToDate))
						{
							// ตรวจสอบว่าอยู่ในรายการเปลี่ยนวันหยุดหรือไม่
							if (!Weekends.Moves.Exists(e => e.Key.Date == aToDate))
								throw new Exception("TODATE_INVALID");
						}
						
						records = Weekends.EditWeekendMovement(User, PersonNo, (DateTime)aDate, (DateTime)dDate, aFromDate, aToDate, Remark);
				    }
				}
			}
			catch (Exception e)
			{
			    error = true;
			    errorMessage = e.Message;
			}

			var JsonData = new { error = error, errorMessage = errorMessage, records = records };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}

		#endregion

		#region Templates Setting

		[HttpPost]
		public ActionResult AjaxGetHolidays(int page, int rows, bool _search, string sidx, string sord, string KeyPair, string ValuePair)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			int userpage = 0;
			int userselid = 0;
			List<object> results = new List<object>();
			try
			{
				var obj = Setting.ListHolidays(User);
				//obj = obj.OrderBy(s => s.Description).Skip((page - 1) * rows).Take(rows).ToList();
				//obj = obj.OrderBy(e => e.CompanyCode).ThenBy(e => Tool.OrderByProperty(sidx, e))
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						obj = obj.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						obj = obj.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				if (!string.IsNullOrEmpty(KeyPair) && !string.IsNullOrEmpty(ValuePair))
				{
					int fIndex = obj.FindIndex(e => Tool.FindIndexProperty(KeyPair, ValuePair, e));
					if (fIndex > -1)
					{
						userpage = (int)Math.Ceiling((fIndex + 1) / (float)rows);
						userselid = (int)((fIndex + 1) % (float)rows);
						userselid = userselid == 0 ? rows : userselid;
						page = userpage;
					}
				}
				records = obj.Count;
				foreach (var rec in obj.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new { id = ++num, cell = new[] { rec.HolidaysID.ToString(), rec.Description, null } });
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, 
				records = records, rows = results, userdata = new { page = userpage, selId = userselid }};
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}
		
		[HttpPost]
		public ActionResult AjaxSetHolidays(string oper, int id, string Description)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			try
			{
				if (oper.Equals("add"))
				{
					records = Setting.AddHolidays(User, Description);
				}
				else if (oper.Equals("edit"))
				{
					records = Setting.EditHolidays(User, id, Description);
				}
				else if (oper.Equals("del"))
				{
					records = Setting.DeleteHolidays(User, id);
				}
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
		public ActionResult AjaxGetHoliday(int page, int rows, bool _search, string sidx, string sord, int HolidaysID, string KeyPair, string ValuePair)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			int userpage = 0;
			int userselid = 0;
			List<object> results = new List<object>();
			try
			{
				var obj = Setting.ListHoliday(User, null, HolidaysID);
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						obj = obj.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						obj = obj.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				if (!string.IsNullOrEmpty(KeyPair) && !string.IsNullOrEmpty(ValuePair))
				{
					int fIndex = obj.FindIndex(e => Tool.FindIndexProperty(KeyPair, ValuePair, e));
					if (fIndex > -1)
					{
						userpage = (int)Math.Ceiling((fIndex + 1) / (float)rows);
						userselid = (int)((fIndex + 1) % (float)rows);
						userselid = userselid == 0 ? rows : userselid;
						page = userpage;
					}
				}
				records = obj.Count;
				foreach (var rec in obj.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[]
						{
							rec.HolidayID.ToString(),
							rec.Of.HolidaysID.ToString(),
							string.Format("{0}", rec.Date.HasValue ?
								   rec.Date.Value.ToString("dd/MM/yyyy", Thread.CurrentThread.CurrentCulture) : "" ),
							rec.NameTH,
							rec.NameEN
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, 
				records = records, rows = results, userdata = new { page = userpage, selId = userselid }};
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxSetHoliday(string oper, int id, int HolidaysID, string Date, string NameTH, string NameEN)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			try
			{
				DateTime? aDate = null;
				if(!string.IsNullOrEmpty(Date))
					aDate = DateTime.ParseExact(Date, "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None); 

				if (oper.Equals("add"))
				{
					records = Setting.AddHoliday(User, HolidaysID, (DateTime)aDate, NameTH, NameEN);
				}
				else if (oper.Equals("edit"))
				{
					records = Setting.EditHoliday(User, id, (DateTime)aDate, NameTH, NameEN);
				}
				else if (oper.Equals("del"))
				{
					records = Setting.DeleteHoliday(User, 0, id);
				}
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
		public ActionResult AjaxGetWorkshifts(int page, int rows, bool _search, string sidx, string sord, string KeyPair, string ValuePair)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			int userpage = 0;
			int userselid = 0;
			List<object> results = new List<object>();
			try
			{
				var obj = Setting.ListWorkshifts(User);
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						obj = obj.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						obj = obj.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				if (!string.IsNullOrEmpty(KeyPair) && !string.IsNullOrEmpty(ValuePair))
				{
					int fIndex = obj.FindIndex(e => Tool.FindIndexProperty(KeyPair, ValuePair, e));
					if (fIndex > -1)
					{
						userpage = (int)Math.Ceiling((fIndex + 1) / (float)rows);
						userselid = (int)((fIndex + 1) % (float)rows);
						userselid = userselid == 0 ? rows : userselid;
						page = userpage;
					}
				}
				records = obj.Count;
				foreach (var rec in obj.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[] { rec.WorkshiftsID.ToString(), rec.OfficeStaff == 1 ? "true" : "false", rec.Description }
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, 
				records = records, rows = results, userdata = new { page = userpage, selId = userselid }};
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxSetWorkshifts(string oper, int id, int OfficeStaff, string Description)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			try
			{
				if (oper.Equals("add"))
				{
					records = Setting.AddWorkshifts(User, OfficeStaff, Description);
				}
				else if (oper.Equals("edit"))
				{
					records = Setting.EditWorkshifts(User, id, OfficeStaff, Description);
				}
				else if (oper.Equals("del"))
				{
					records = Setting.DeleteWorkshifts(User, id);
				}
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
		public ActionResult AjaxGetWorkshift(int page, int rows, bool _search, string sidx, string sord, int WorkshiftsID, string KeyPair, string ValuePair)
		{
			int num = 0;
			int total = 0;
			int records = 0;
			int userpage = 0;
			int userselid = 0;
			List<object> results = new List<object>();
			try
			{
				var obj = Setting.ListWorkshift(User, WorkshiftsID);
				if (!string.IsNullOrEmpty(sord) && !string.IsNullOrEmpty(sidx))
				{
					if (!sord.Equals("desc"))
						obj = obj.OrderBy(e => Tool.OrderByProperty(sidx, e)).ToList();
					else
						obj = obj.OrderByDescending(e => Tool.OrderByProperty(sidx, e)).ToList();
				}
				if (!string.IsNullOrEmpty(KeyPair) && !string.IsNullOrEmpty(ValuePair))
				{
					int fIndex = obj.FindIndex(e => Tool.FindIndexProperty(KeyPair, ValuePair, e));
					if (fIndex > -1)
					{
						userpage = (int)Math.Ceiling((fIndex + 1) / (float)rows);
						userselid = (int)((fIndex + 1) % (float)rows);
						userselid = userselid == 0 ? rows : userselid;
						page = userpage;
					}
				}
				records = obj.Count;
				foreach (var rec in obj.Skip((page - 1) * rows).Take(rows))
				{
					results.Add(new
					{
						id = ++num,
						cell = new[]
						{
							rec.WorkshiftID.ToString(),
							rec.Of.WorkshiftsID.ToString(),
							rec.Type.ToString(),
							rec.Description,
							string.Format("{0}", rec.TimeBegin.HasValue ?
								   rec.TimeBegin.Value.ToString("HH:mm", Thread.CurrentThread.CurrentCulture) : "" ),
							string.Format("{0}", rec.TimeUntil.HasValue ?
								   rec.TimeUntil.Value.ToString("HH:mm", Thread.CurrentThread.CurrentCulture) : "" )
						}
					});
				}
			} finally {}

			total = (int)Math.Ceiling(records / (float)rows);
			var JsonData = new { page = page, total = total, 
				records = records, rows = results, userdata = new { page = userpage, selId = userselid }};
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public ActionResult AjaxSetWorkshift(string oper, int id, string TimeBegin, string TimeUntil)
		{
			int records = 0;
			bool error = false;
			string errorMessage = null;
			try
			{
				TimeSpan Begin = TimeSpan.ParseExact(TimeBegin, "hh\\:mm", new CultureInfo("en-US"), TimeSpanStyles.None);
				TimeSpan Until = TimeSpan.ParseExact(TimeUntil, "hh\\:mm", new CultureInfo("en-US"), TimeSpanStyles.None);
				
				//if (oper.Equals("add"))
				//{
				//    records = Setting.AddWorkshift(User, 0);
				//}
				if (oper.Equals("edit"))
				{
					records = Setting.EditWorkshift(User, id, Begin, Until);
				}
				//if (oper.Equals("del"))
				//{
				//    records = Setting.DeleteWorkshift(User, 0, id);
				//}
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}

			var JsonData = new { error = error, errorMessage = errorMessage, records = records };
			return Json(JsonData, JsonRequestBehavior.DenyGet);
		}
		
		#endregion

	}
}
