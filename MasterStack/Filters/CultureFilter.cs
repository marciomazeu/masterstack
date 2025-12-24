using Microsoft.AspNetCore.Mvc.Filters;
using System.Globalization;

public class CultureFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var culture = context.RouteData.Values["culture"]?.ToString();
        if (!string.IsNullOrEmpty(culture))
        {
            var cultureInfo = new CultureInfo(culture);
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}