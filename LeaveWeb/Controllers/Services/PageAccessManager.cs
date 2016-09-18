using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Security.Principal;

namespace Leave.Controllers.Services
{
    public class PageAccessManager
    {
        public static bool IsAccessAllowed(string Controller, string Action, IPrincipal User, string IP)
        {
            if (Controller == "Public" || Controller == "EmailTemplates")
                return true;
			if (Controller == "Veto" && User.Identity != null && User.Identity.IsAuthenticated)
				return true;
			if (Controller == "Grant" && User.Identity != null && User.Identity.IsAuthenticated)
				return true;
			if (Controller == "Leave" && User.Identity != null && User.Identity.IsAuthenticated)
				return true;
			if (Controller == "Setting" && User.Identity != null && User.Identity.IsAuthenticated)
				return true;
			if (Controller == "MenuSuite" && User.Identity != null && User.Identity.IsAuthenticated)
				return true;
            if (Controller == "Registered" && User.Identity != null && User.Identity.IsAuthenticated)
                return true;
            if (Controller == "Printing" && User.Identity != null && User.Identity.IsAuthenticated)
                return true;
            //if (Controller == "Admin" && User.IsInRole("Admin"))
            //    return true;

            return false;
        }

    }
}