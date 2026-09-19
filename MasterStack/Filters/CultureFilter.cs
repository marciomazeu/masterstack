using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MasterStack
{
    public class CultureFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var culture = context.RouteData.Values["culture"]?.ToString() ?? "fr-CA";
            context.HttpContext.Items["CurrentCulture"] = culture;

            if (context.Controller is Controller controller)
            {
                controller.ViewData["CurrentCulture"] = culture;
            }

            base.OnActionExecuting(context);
        }
    }
}