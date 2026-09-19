using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Globalization;

public class CultureFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var culture = context.RouteData.Values["culture"]?.ToString() ?? "fr-CA";
            context.HttpContext.Items["CurrentCulture"] = culture;

            if (context.Controller is Controller controller)
            {
                controller.ViewData["CurrentCulture"] = culture;
            }

            await next();
        }
    }