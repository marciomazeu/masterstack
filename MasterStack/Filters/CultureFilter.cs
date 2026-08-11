using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Globalization;

public class CultureFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var httpContext = context.HttpContext;
        string? culture = context.RouteData.Values["culture"]?.ToString();

        if (string.IsNullOrEmpty(culture))
        {
            culture = httpContext.Request.Query["culture"].ToString();
        }

        // Tenta extrair da URL original caso seja uma reexecução de erro (ex: 404)
        if (string.IsNullOrEmpty(culture))
        {
            var reExecuteFeature = httpContext.Features.Get<IStatusCodeReExecuteFeature>();
            if (reExecuteFeature != null)
            {
                var segments = reExecuteFeature.OriginalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0 && new[] { "pt-BR", "en-US", "fr-CA" }.Contains(segments[0]))
                {
                    culture = segments[0];
                }
            }
        }

        if (!string.IsNullOrEmpty(culture))
        {
            var cultureInfo = new CultureInfo(culture);

            // 1. Atualiza as Threads
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;

            // 2. Sobrescreve a Feature de Localização do ASP.NET Core
            // É isso que o _Layout e os ViewComponents usam para identificar o idioma ativo e exibir a bandeira
            httpContext.Features.Set<IRequestCultureFeature>(
                new RequestCultureFeature(new RequestCulture(cultureInfo), null)
            );
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}