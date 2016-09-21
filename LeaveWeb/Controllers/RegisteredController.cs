using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.Security.Application;
using LeaveCore;

namespace Leave.Controllers
{
    public class RegisteredController : Controller
    {
        //
        // GET: /Registered/

        public ActionResult Index()
        {
            return View();
        }

        public new ActionResult Profile()
        {
			if (User.Identity.IsAuthenticated)
			{
				ViewBag.PersonNo = User.Identity.Name;
			}
			return View();
        }

        public ActionResult Policy()
        {
			if (User.Identity.IsAuthenticated)
			{
				LoginIdentity user = (LoginIdentity)User.Identity;
				ViewBag.PersonNo = user.Name;
				ViewBag.PersonName = user.Prefix + user.FirstName + " " + user.LastName;
			}
			return View();
        }

		public ActionResult Holiday()
		{
			ViewBag.Year = DateTime.Now.Year;
			return View(Setting.ListHoliday(User, DateTime.Now.Year, 0));
		}

    }
}
