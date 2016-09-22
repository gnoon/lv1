using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Leave.Controllers.Services;
using System.Web.Security;
using System.Security.Principal;
using System.Configuration;
using LeaveCore;
using System.Web.Configuration;
using Leave.Controllers;
using System.Web.Routing;
using System.Text.RegularExpressions;
//using System.Runtime.Remoting.Contexts;

namespace Leave.SecurityFilters
{
    public class SecurityFilter : FilterAttribute, IAuthorizationFilter, IExceptionFilter
    ////public class SecurityFilter : FilterAttribute, IAuthorizationFilter, IExceptionFilter, IActionFilter, IResultFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            HttpContext.Current.User = new GenericPrincipal(LoginIdentity.Default, new string[] { "" });

            var Controller = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            //var Action = filterContext.ActionDescriptor.ActionName;
            //var User = filterContext.HttpContext.User;
            //var IP = filterContext.HttpContext.Request.UserHostAddress;

            var Request = filterContext.HttpContext.Request;
            var Response = filterContext.HttpContext.Response;
            var Server = filterContext.HttpContext.Server;

            HttpCookie authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null)
            {
                if (Controller != "Public")
                {
                    FormsAuthentication.RedirectToLoginPage("");
                    return;
                }
            }

            #region ตรวจสอบการเปิดใบลาจาก Outlook หรือ Webmail ซึ่งต้องป้อนรหัสผ่านที่ถูกต้องก่อน

            // ความรู้: หากเปิดใบลาโดย https ... browser ส่วนใหญ่จะไม่ส่ง referer มาให้ เช่นเดียวกับเปิดจาก Outlook
            //
            // วิธีตรวจสอบการเปิดใบลาจากนอกระบบ คือ
            //      * หากมีการส่ง referer เข้ามา ... ตรวจสอบว่ามาจากหน้าเว็บ hosting เดียวกันหรือไม่
            //          เช่น เปิดมาจาก webmail.verasu.com ถือว่ามาจาก host ที่ต่างกัน ก็เด้งไปหน้า login
            //      * หากไม่มีส่ง referer เข้ามา ... ตรวจสอบว่า URL ที่ Request มาลงท้ายด้วย /$ หรือไม่
            //          โดย /$ มีการเติมไว้ก่อนแล้วในเมลล์ที่ส่งออกจากระบบ
            //          เช่น /Grant/Approve/2625/0/9C83598A6BC0EFAFFC694CC3A79F2E06/0/5554CD0DF4E258D9820BE255B7D7C4C4/$
            //
            //  ทั้งนี้ มี URL แค่ 3 Patterns เท่านั้น ที่จะเปิดใบลาจากนอกระบบ คือ
            //      1. /Leave/Viewer - กรณีส่งเมลล์ให้พนักงาน
            //      2. /Grant/Approve - กรณีส่งเมลล์ให้หัวหน้างาน
            //      3. /Veto/Interrupt - กรณีส่งเมลล์ให้ผู้มีสิทธิระงับใบลา
            //
            //  ทั้งหมดนี้ต้องเด้งไปหน้าจอ Login ก่อน เมื่อ Login เสร็จค่อยเปิดใบลาได้ (ตัด /$ ออกจาก URL แล้ว)
            //
            //  ที่สำคัญที่สุด คือ ทั้ง 3 Controllers ได้มีการเช็คแล้วว่า User ที่ Login ต้องเป็นบุคคลที่มีสิทธิเปิดจริงๆเท่านั้น

            string AbsPath = Request.Url.AbsoluteUri;
            string Protocol = Request.Url.Scheme;
            Uri Referer = Request.UrlReferrer;

            string BasedAbsPath = Leave.Properties.Settings.Default.InternetBasedUrl;
            if (BasedAbsPath.EndsWith("/"))
                BasedAbsPath = BasedAbsPath.Remove(BasedAbsPath.Length - 1);
            if (string.IsNullOrWhiteSpace(BasedAbsPath) || AbsPath.Length < BasedAbsPath.Length)
                BasedAbsPath = VirtualPathUtility.ToAbsolute("~/");

            const string ChangePatternUrl = "/Leave/Viewer/([0-9]+)/([0-9A-Z]+)/([0-9]+)/([0-9A-Z]+)(.*)";
            const string NotifyPatternUrl = "/Grant/Approve/([0-9]+)/([0-9]+)/([0-9A-Z]+)/([0-9A-Z]+)(.*)";
            const string VetoesPatternUrl = "/Veto/Interrupt/([0-9]+)/([0-9]+)/([0-9A-Z]+)/([0-9A-Z]+)(.*)";

            bool IsRequestFromEmail = false;

            string RelPath = AbsPath.Remove(0, BasedAbsPath.Length);
            if (Regex.IsMatch(RelPath, ChangePatternUrl, RegexOptions.IgnoreCase) ||
                Regex.IsMatch(RelPath, NotifyPatternUrl, RegexOptions.IgnoreCase) ||
                Regex.IsMatch(RelPath, VetoesPatternUrl, RegexOptions.IgnoreCase))
            {
                IsRequestFromEmail = true;
            }

            bool IsRequestRequiresLogin = false;
            if (IsRequestFromEmail)
            {
                // https จะไม่มีการส่ง referer มา
                if ("https".Equals(Protocol, StringComparison.InvariantCultureIgnoreCase))
                {
                    IsRequestRequiresLogin = AbsPath.EndsWith("/$");
                }
                else
                {
                    if (Referer == null)
                        IsRequestRequiresLogin = AbsPath.EndsWith("/$");
                    else
                        IsRequestRequiresLogin = !Referer.Host.Equals(new Uri(BasedAbsPath).Host,
                            StringComparison.InvariantCultureIgnoreCase);
                }
            }

            if (IsRequestRequiresLogin)
            {
                string ReturnURL = Request.RawUrl;
                if (ReturnURL.EndsWith("/$")) ReturnURL = ReturnURL.Remove(ReturnURL.Length - 2);
                //FormsAuthentication.RedirectToLoginPage("ReturnURL=" + Server.UrlEncode(ReturnURL));
                string LoginPage = string.Format("{0}://{1}{2}{3}?ReturnURL={4}", (Request.IsSecureConnection) ? "https" : "http",
                    Request.Url.Host, (Request.Url.Port == 80) ? "" : ":" + Request.Url.Port.ToString(),
                    VirtualPathUtility.ToAbsolute("~/Public/Login"), Uri.EscapeDataString(ReturnURL));
                Response.Redirect(LoginPage, true);
                return;
            }

            #endregion

            if (authCookie == null) // กรณี Controller == "Public"
                return;

            string encTicket = authCookie.Value;
            if (!String.IsNullOrEmpty(encTicket))
            {
                FormsAuthenticationTicket ticket = null;
                try
                {
                    ticket = FormsAuthentication.Decrypt(encTicket);
                }
                catch
                {
                    HttpContext.Current.Request.Cookies.Remove(FormsAuthentication.FormsCookieName);
                    return;
                }

                string PersonNo = ticket.Name.Split(',')[0];
                bool isImpersonate = ticket.Name.EndsWith(",1");

                Configuration conf = WebConfigurationManager.OpenWebConfiguration("~");
                LoginIdentity id = LoginIdentity.CreateIdentity(PersonNo, "Forms", Tool.GetConnectionString(conf, "LEAVE"));

                if (!string.IsNullOrEmpty(id.EmployeeNo))
                {
                    if (isImpersonate && !id.Roles.Exists(x => x.Equals(Const.ROLE_IMPERSONATE)))
                        id.Roles.Add(Const.ROLE_IMPERSONATE);

                    //string[] arr = LoginIdentity.SplitRoles(ticket.UserData);
                    string[] arr = id.Roles.ToArray();
                    GenericPrincipal prin = new GenericPrincipal(id, arr);
                    HttpContext.Current.User = prin;
                }
            }
        }

        public void OnException(ExceptionContext filterContext)
        {

            if (filterContext.Exception != null && filterContext.Exception is System.Security.SecurityException)
            {
                var result = new ViewResult();
                result.ViewName = "SecurityError";
                filterContext.Result = result;
                filterContext.ExceptionHandled = true;
            }
			if (filterContext.Exception != null && filterContext.Exception is System.Web.HttpException)
			{
                var result = new ViewResult();
                result.ViewName = "HttpError";
                filterContext.Result = result;
                filterContext.ExceptionHandled = true;

				//filterContext.ExceptionHandled = true;
				//var httpException = (HttpException)filterContext.Exception;
				//var statusCode = httpException.GetHttpCode();
				
				//var routeData = new RouteData();
				//routeData.Values.Add("controller", "Error");
				//routeData.Values.Add("action", "Index");
				//routeData.Values.Add("statusCode", statusCode);

				//IController errorController = new ErrorController();
				//errorController.Execute(new RequestContext(new HttpContextWrapper(HttpContext.Current), routeData));
			}
        }

		//public void OnActionExecuting(ActionExecutingContext filterContext)
		//{
		//    var viewResult = filterContext.Result as ViewResult;
		//    var controller = filterContext.RouteData.Values["controller"];
		//    var action = filterContext.RouteData.Values["action"];
		//    var view = viewResult.ViewName;
		//    if (controller == "Admin" &&
		//        action == "Index" &&
		//        view == "Blocked")
		//        filterContext.Cancel = true;
		//}

		//public void OnActionExecuted(ActionExecutedContext filterContext)
		//{
		//    if (filterContext.ActionDescriptor.ControllerDescriptor.ControllerName == "Admin"
		//        && filterContext.ActionDescriptor.ActionName == "Home"
		//        && User.IsInRole("Registered"))
		//        filterContext.Result = null;
		//}

        //public void OnResultExecuting(ResultExecutingContext filterContext)
        //{
        //    var viewResult = filterContext.Result as ViewResult;
        //    var controller = filterContext.RouteData.Values["controller"];
        //    var action = filterContext.RouteData.Values["action"];
        //    var view = viewResult.ViewName;
        //    if (controller == "Admin" &&
        //        action == "Index" &&
        //        view == "Blocked")
        //        filterContext.Cancel = true;
        //}

        //public void OnResultExecuted(ResultExecutedContext filterContext)
        //{
        
        //}
    }
}