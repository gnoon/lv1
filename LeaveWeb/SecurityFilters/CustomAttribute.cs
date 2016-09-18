using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Leave.SecurityFilters
{
    public class CustomAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            //Log("OnActionExecuting", filterContext.RouteData);
            var pb = filterContext.Controller.ViewBag.Postback;
            if(pb == null)
                filterContext.Controller.ViewBag.Postback = new Leave.Models.CommonPostbackData();

            base.OnActionExecuting(filterContext);

            SetPageExpire(filterContext.HttpContext.Response);
        }

        private void SetPageExpire(HttpResponseBase Response)
        {
            Response.Expires = -1;
            Response.Cache.SetNoServerCaching();
            Response.Cache.SetAllowResponseInBrowserHistory(false);
            Response.CacheControl = "no-cache";
            Response.Cache.SetNoStore();
        }

        private void Log(string methodName, System.Web.Routing.RouteData routeData)
        {
            var controllerName = routeData.Values["controller"];
            var actionName = routeData.Values["action"];
            var message = String.Format("{0} controller:{1} action:{2}", methodName, controllerName, actionName);
            System.Diagnostics.Debug.WriteLine(message, "Action Filter Log");
        }
    }
}