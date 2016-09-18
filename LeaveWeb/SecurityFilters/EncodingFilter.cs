using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.Security.Application;

namespace Leave.SecurityFilters
{
    public class EncodingFilter : FilterAttribute, IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.ActionDescriptor.GetCustomAttributes(typeof(ValidateInputAttribute), true).Length == 1)
            {
                foreach (var param in filterContext.ActionParameters)
                {
                    filterContext.ActionParameters[param.Key] = Sanitizer.GetSafeHtmlFragment(param.Value.ToString());
                }
            }

        }

        public void OnActionExecuted(ActionExecutedContext filterContext)
        {


        }
    }
}