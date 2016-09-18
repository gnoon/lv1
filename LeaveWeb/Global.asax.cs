using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using FluentSecurity;
using Leave.Controllers;
using Leave.SecurityFilters;
using System.Web.Security;
using System.Configuration;
using System.Web.Configuration;
using System.Data.SqlClient;
using System.Security.Principal;
using LeaveCore;
using System.Text.RegularExpressions;

namespace Leave
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode,
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
			//filters.Add(new AuthorizeAttribute());
            filters.Add(new HandleErrorAttribute());

            //Uncomment this line to eable custom attrbute security
            filters.Add(new SecurityFilter());

            //Uncomment this line to use custom 
            filters.Add(new CustomAttribute());

            SecurityConfigurator.Configure(configuration =>
            {
                // Let Fluent Security know how to get the authentication status of the current user
                configuration.GetAuthenticationStatusFrom(() => HttpContext.Current.User.Identity.IsAuthenticated);

                // Let Fluent Security know how to get the roles for the current user
                configuration.GetRolesFrom(() =>
                            {
                                var authCookie = HttpContext.Current.Request.Cookies[FormsAuthentication.FormsCookieName];
                                string[] arr = new[] { "" };
                                if (authCookie == null)
                                    return arr;

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
                                        return arr;
                                    }

                                    Configuration conf = WebConfigurationManager.OpenWebConfiguration("~");
                                    LoginIdentity id = LoginIdentity.CreateIdentity(ticket.Name, "Forms", Tool.GetConnectionString(conf, "LEAVE"));

                                    if (!string.IsNullOrEmpty(id.EmployeeNo))
                                    {
                                        arr = id.Roles.ToArray();
                                    }
                                }
                                return arr;
                            });

                // This is where you set up the policies you want Fluent Security to enforce
                configuration.For<PublicController>().Ignore();
                configuration.For<PrintingController>().Ignore();
                configuration.For<EmailTemplatesController>().Ignore();

				configuration.For<VetoController>().DenyAnonymousAccess();
				configuration.For<GrantController>().DenyAnonymousAccess();
                configuration.For<LeaveController>().DenyAnonymousAccess();
                configuration.For<SettingController>().DenyAnonymousAccess();
                configuration.For<MenuSuiteController>().DenyAnonymousAccess();
                configuration.For<RegisteredController>().DenyAnonymousAccess();

                //configuration.For<PublicController>(x => x.Dashboard()).RequireRole("Public");

                //configuration.For<RegisteredController>(x => x.Index()).DenyAnonymousAccess();
                //configuration.For<RegisteredController>(x => x.Dashboard()).RequireRole("Registered", "Admin");
                //configuration.For<RegisteredController>(x => x.Home()).RequireRole("Registered", "Admin");
                //configuration.For<RegisteredController>(x => x.MyAge()).RequireRole("Registered", "Admin");
                //configuration.For<RegisteredController>(x => x.Profile()).RequireRole("Registered", "Admin");

                //configuration.For<AdminController>(x => x.Index()).DenyAnonymousAccess();
                //configuration.For<AdminController>(x => x.Dashboard()).RequireRole("Admin");
                //configuration.For<AdminController>(x => x.Home()).RequireRole("Admin");
                //configuration.For<AdminController>(x => x.Denied()).RequireRole("Admin");
                
            });

            //Uncomment this line to enable fluent security
            //GlobalFilters.Filters.Add(new HandleSecurityAttribute(), 0);
        }

        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

			routes.MapRoute(
				"Veto", // Route name
				"Veto/Interrupt/{RequestID}/{LeaveID}/{HeadPersonNo}/{Digest}/{DollarSign}",
				new { controller = "Veto", action = "Interrupt",
					  RequestID = UrlParameter.Optional, LeaveID = 0,
					  HeadPersonNo = UrlParameter.Optional, Digest = UrlParameter.Optional,
                      DollarSign = UrlParameter.Optional }
			);

			routes.MapRoute(
				"Grant", // Route name
                "Grant/Approve/{RequestID}/{LeaveID}/{HeadPersonNo}/{RequestCompare}/{Digest}/{DollarSign}",
				new { controller = "Grant", action = "Approve",
					  RequestID = UrlParameter.Optional, LeaveID = 0,
					  HeadPersonNo = UrlParameter.Optional, RequestCompare = 0, Digest = UrlParameter.Optional,
                      DollarSign = UrlParameter.Optional }
			);
			
			routes.MapRoute(
				"LeaveTodo", // Route name
				"Leave/Todo/{RequestID}/{PersonNo}/{RequestCompare}",
				new { controller = "Leave", action = "Todo",
					  RequestID = UrlParameter.Optional, PersonNo = UrlParameter.Optional, RequestCompare = 0 }
			);

			routes.MapRoute(
				"LeaveViewer", // Route name
                "Leave/Viewer/{RequestID}/{PersonNo}/{RequestCompare}/{Digest}/{DollarSign}",
				new { controller = "Leave", action = "Viewer",
					  RequestID = UrlParameter.Optional, PersonNo = UrlParameter.Optional,
					  RequestCompare = 0, Digest = UrlParameter.Optional,
                      DollarSign = UrlParameter.Optional }
			);

			routes.MapRoute(
				"LeaveAdjusting", // Route name
                "Leave/Adjusting/{RequestID}/{PersonNo}/{RequestCompare}/{Digest}",
				new { controller = "Leave", action = "Adjusting",
					  RequestID = UrlParameter.Optional, PersonNo = UrlParameter.Optional,
					  RequestCompare = 0, Digest = UrlParameter.Optional }
			);

			routes.MapRoute(
				"Default", // Route name
				"{controller}/{action}/{id}", // URL with parameters
				//new { controller = "Public", action = "Login", id = UrlParameter.Optional } // Parameter defaults
				new { controller = "Leave", action = "Index", id = UrlParameter.Optional } // Parameter defaults
			);

        }

        protected void Application_Start()
        {
            #region load policy values from config
            Const.REQUEST_BUSINESS_PREDATE = Leave.Properties.Settings.Default.Policy_PreDays_Business;
            Const.REQUEST_VACATION_PREDATE = Leave.Properties.Settings.Default.Policy_PreDays_Vacation;
            Const.REQUEST_MATERNITY_PREDATE = Leave.Properties.Settings.Default.Policy_PreDays_Maternity;
            Const.REQUEST_INITIATION_PREDATE = Leave.Properties.Settings.Default.Policy_PreDays_Initiation;
            Const.REQUEST_CELEBRATEHULLVALLEY_PREDATE = Leave.Properties.Settings.Default.Policy_PreDays_Celebratehullvalley;
            Const.REQUEST_EDUCATION_PREDATE = Leave.Properties.Settings.Default.Policy_PreDays_Education;
            Const.REQUEST_STERILIZATION_PREDATE = Leave.Properties.Settings.Default.Policy_PreDays_Sterilization;
            Const.REQUEST_WEDDING_PREDATE = Leave.Properties.Settings.Default.Policy_PreDays_Wedding;

            Const.DEFAULT_MORNING_BEGINOCLOCK = Leave.Properties.Settings.Default.Policy_Default_MorningBeginOClock;
            Const.DEFAULT_MORNING_UNTILOCLOCK = Leave.Properties.Settings.Default.Policy_Default_MorningUntilOClock;
            Const.DEFAULT_NOON_BEGINOCLOCK = Leave.Properties.Settings.Default.Policy_Default_NoonBeginOClock;
            Const.DEFAULT_NOON_UNTILOCLOCK = Leave.Properties.Settings.Default.Policy_Default_NoonUntilOClock;

            Const.REQUEST_SICK_CONTINUALLY_HOURS = Leave.Properties.Settings.Default.Policy_SickHours_MaxContinuous;

            Const.QUOTA_MONTHSTART = Leave.Properties.Settings.Default.Policy_Quota_StartingMonth;
            Const.QUOTA_MINYEARS4VACATION = Leave.Properties.Settings.Default.Policy_MinWorkYears_Vacation;

            Const.DEFAULT_WORKHOURS_OF_DAY = Leave.Properties.Settings.Default.Policy_WorkHours_PerDay;
            Const.REQUEST_APPROVED_BCC = Leave.Properties.Settings.Default.Policy_Bcc_LeaveApproved;

            // load default quota of sick, business and vacation leaves from database
            Const.LoadQuotaAllocate(User);
            #endregion

            log4net.Config.XmlConfigurator.Configure();

            AreaRegistration.RegisterAllAreas();

            RegisterGlobalFilters(GlobalFilters.Filters);
            RegisterRoutes(RouteTable.Routes);
        }

	}
}