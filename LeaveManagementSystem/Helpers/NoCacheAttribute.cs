using System;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LeaveManagementSystem.Helpers
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class NoCacheAttribute : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext context)
        {
            var headers = context.HttpContext.Response.Headers;

            headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            headers["Pragma"] = "no-cache";
            headers["Expires"] = "0";

            base.OnResultExecuting(context);
        }
    }
}
