using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Web.Configuration;
using System.Configuration;
using System.Data.SqlClient;
using LeaveCore;
using System.Security.Principal;
using System.Data;

namespace Leave.Controllers
{
    public class PublicController : Controller
    {
        //
        // GET: /Public/

        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult Login()
        {
            ViewBag.Username = "";
            ViewBag.ErrorMessage = "";
			var RouteUrl = Url.Content("~/Leave/Index");
			var ReturnUrl = Request.Params["ReturnUrl"];
			var Requested = ControllerContext.HttpContext.Request;
			if (!string.IsNullOrEmpty(ReturnUrl) && !ReturnUrl.Equals(Requested.ApplicationPath))
				ViewBag.ReturnUrl = ReturnUrl;
			else
				ViewBag.ReturnUrl = RouteUrl;
            return View();
        }

        [HttpPost]
        public ActionResult Login(string Username, string Password, string Remember, string ReturnUrl, string PersonName)
        {
            string defaultDomain = Leave.Properties.Settings.Default.DefaultEmailDomain;
            if (string.IsNullOrEmpty(Username))
                Username = "Unknown";//Default value that is set if nothing is entered
            else if(!string.IsNullOrWhiteSpace(defaultDomain))
            {

                if (Username.IndexOf("@") == -1)
                {
                    if (defaultDomain.StartsWith("@"))
                        Username = Username + defaultDomain;
                    else
                        Username = Username + "@" + defaultDomain;
                }
            }

            ViewBag.Username = Username;

            /**
             * MY - พนักงาน login เป็นชื่อตัวเอง
             * HR - HR login เป็นชื่อของ HR เอง
             * HEAD - HEAD login เป็นชื่อของ HEAD เอง
             * IMPERSONATE - HR login เป็นชื่อของพนักงาน
             * ASSTHEAD - ASSISTANT login เป็นชื่อตัวเองแต่มีสิทธิเหมือน HEAD (ที่ assign สิทธิให้มา)
             * VETO - HEAD login เป็นชื่อของ HEAD เอง
             * ASSTHR - ชื่อตัวเองแต่มีสิทธิเหมือน HR (ผู้ช่วยจัดการเฉพาะบริษัทตัวเอง)
             **/
            string roles = Const.ROLE_MY, PersonNo = string.Empty, EmployeeNo = string.Empty;
            bool error = false;

            try
            {
                Configuration conf = WebConfigurationManager.OpenWebConfiguration("~");
                using (SqlConnection conn = new SqlConnection(Tool.GetConnectionString(conf, "LEAVE")))
                {
                    conn.Open();

                    //string EmployeeNo = Username;
                    //bool EmployeeResolved = false;
                    // Username from Web can be Employee No. or AD Account
                    //if (!LoginIdentity.ToPersonNo(conn, Username, Password, PersonName,
                    //    System.Text.Encoding.GetEncoding(Leave.Properties.Settings.Default.DbServerEncoding), out PersonNo, out roles, out hash))
                    //{
                    //    if (!LoginIdentity.ToPersonNo(conn, Username, Password, PersonName,
                    //        Leave.Properties.Settings.Default.ADDomain,
                    //        Leave.Properties.Settings.Default.ADConnectionString, out PersonNo, out roles))
                    //    {
                    //        throw new MembershipPasswordException("Username or Password is invalid!");
                    //    }
                    //    else
                    //    {
                    //        EmployeeResolved = true;
                    //        EmployeeNo = new LeaveCore.Leave(User, null).ExecuteScalar<string>(string.Format(
                    //            "select top 1 [Employee No] from [HR Employee] where [Person No]='{0}' " +
                    //            "order by [Starting Date] desc", PersonNo), conn);
                    //    }
                    //}
                    //else EmployeeResolved = true;

                    if (!LoginIdentity.ToPersonNo(conn, Username, Password,
                        System.Text.Encoding.GetEncoding(Leave.Properties.Settings.Default.DbServerEncoding),
                        out PersonNo, out roles, out EmployeeNo))
                    {
                        throw new MembershipPasswordException("Username or Password is invalid!");
                    }

                    //if (EmployeeResolved)
                    //{
                    //    try
                    //    {
                    //        // ดึงเอา User เข้าเครื่องจาก AD มา update ที่ leave online อัตโนมัติ
                    //        LoginIdentity.ValidateSAMAccount(conn, EmployeeNo,
                    //            Leave.Properties.Settings.Default.ADDomain,
                    //            Leave.Properties.Settings.Default.ADConnectionString);
                    //    }
                    //    catch
                    //    {
                    //    }
                    //}
                }
            }
            catch (Exception e)
            {
                error = true;
                ViewBag.ErrorMessage = e.Message;
                ActionLog.File.Error(e);
            }

            if (!error)
            {
				var createPersistentCookie = "yes".Equals(Remember);
                var isImpersonate = roles.IndexOf(Const.ROLE_IMPERSONATE) > -1 ? "1" : "0";

                // ถ้า sessionless ถูกเซ็ตเป็น AutoDetect ใน web.config
                // หาก Browser เปิดใช้งาน cookies ก็จะเก็บ authen ใน cookies
                // แต่หาก Browser ไม่เปิดใช้ cookies ก็จะส่ง authen ไปทาง url แทน
                // ทำให้ใช้งานระบบได้ใน Browser ที่ไม่ได้เปิดใช้ cookie ด้วยนั่นเอง
                // ไม่เช่นนั้นก็จะ Login ไม่เข้า เด้งอยู่หน้า Login นั่นแหละ
                FormsAuthentication.SetAuthCookie(PersonNo + "," + isImpersonate, createPersistentCookie);

                // save login log
                ActionLog.New(User, LeaveCore.Action.Login, Username, null);

				if (!string.IsNullOrEmpty(ReturnUrl))
				{
                    string RedirectUrl = Url.Content(ReturnUrl);
                    return Redirect(RedirectUrl);
				}
                return RedirectToAction("Index", "Leave");
            }
            else
            {
                return View();
            }
        }

		public ActionResult Logout()
        {
            // save log
            ActionLog.New(User, LeaveCore.Action.Logout, null, null);

			Session.Clear();
			Request.Cookies.Clear();
			Response.Cookies.Clear();
			FormsAuthentication.SignOut();
			return RedirectToAction("Login");
		}

		/// <summary>
		/// ตรวจสอบชื่อพนักงาน ที่ใช้ Login กรณีมีรหัสพนักงานเดียวกัน
		/// </summary>
		/// <param name="EmployeeNo">รหัสพนักงาน ที่ระบุในหน้าจอ Login</param>
		/// <returns></returns>
		public ActionResult AjaxCheckIdentity(string EmployeeNo)
		{
			bool error = false;
			string errorMessage = null;
			List<object> results = new List<object>();
			try
			{
				#region Getting Identity
				BaseDB db = new BaseDB(User);
				int Counter = db.ExecuteScalar<int>("SELECT COUNT(*) FROM [HR Employee] WHERE REPLACE(LOWER([Employee No]),'-','')='" + EmployeeNo.ToLower().Replace("-",string.Empty)+"'", null);
				if (Counter > 1)
				{
					using (SqlConnection conn = db.GetConnection())
					{
						conn.Open();
						using (SqlCommand cmd = conn.CreateCommand())
						{
							#region Query
							cmd.CommandText =
								  "SELECT  ps.[Person No], em.[Employee No], ps.[TH Prefix], ps.[TH First Name], ps.[TH Last Name] "
								+ "FROM   [HR Person] AS ps INNER JOIN "
								+ "		  [HR Employee] AS em ON ps.[Person No] = em.[Person No] "
								+ "WHERE  REPLACE(LOWER(em.[Employee No]),'-','')=@empno "
								+ "ORDER BY em.[Starting Date]";
								cmd.Parameters.Clear();
								cmd.Parameters.Add("@empno", SqlDbType.VarChar).Value = EmployeeNo.ToLower().Replace("-",string.Empty);
							using (SqlDataReader rs = cmd.ExecuteReader())
							{
								while (rs.Read())
								{
									results.Add(new
									{
										PersonNo = rs.GetValue<string>("Person No"),
										EmployeeNo = rs.GetValue<string>("Employee No"),
										Prefix = rs.GetValue<string>("TH Prefix"),
										NameFirst = rs.GetValue<string>("TH First Name"),
										NameLast = rs.GetValue<string>("TH Last Name")
									});
								}
							}
							#endregion
						}
					}
				}
				#endregion
			}
			catch (Exception e)
			{
				error = true;
				errorMessage = e.Message;
			}

			var JsonData = new { error = error, errorMessage = errorMessage, records = results };
			return Json(JsonData, JsonRequestBehavior.AllowGet);
		}

    }
}
