using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using LeaveCore.Email.Services;
using System.Configuration;
using System.Web.Configuration;

namespace Leave.Controllers
{
    public class EmailTemplatesController : Controller
    {
        public ActionResult Veto()
        {
            var model = new LeaveCore.Email.Model.FakeVetoEmailModel()
            {
                InternetBasedUrl = Leave.Properties.Settings.Default.InternetBasedUrl
            };
            EmailService service = new EmailService(HttpContext.User);
            //service.SendEmail("noppadols@verasu.com", null, "แจ้งการยื่นใบลาของพนักงาน", EmailType.Veto, model);

            return View(model);
        }

        public ActionResult Change()
        {
            var model = new LeaveCore.Email.Model.FakeChangeEmailModel()
            {
                InternetBasedUrl = Leave.Properties.Settings.Default.InternetBasedUrl
            };
            EmailService service = new EmailService(HttpContext.User);
            //service.SendEmail("noppadols@verasu.com", "anusorns@verasu.com",
            //    "แจ้งความเคลื่อนไหวใบลาของคุณ" + model.LeaveRequest.Person.NameFirstTH + " " + model.LeaveRequest.Person.NameLastTH,
            //    EmailType.Change, model);

            return View(model);
        }

        public ActionResult Notification()
        {
            var model = new LeaveCore.Email.Model.FakeNotificationEmailModel()
            {
                InternetBasedUrl = Leave.Properties.Settings.Default.InternetBasedUrl
            };
            EmailService service = new EmailService(HttpContext.User);
            //service.SendEmail("noppadols@verasu.com", null, "แจ้งการยื่นใบลาของพนักงาน", EmailType.Notification, model);

            return View(model);
        }
    }
}
