using MasterStack.Data;
using MasterStack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MasterStack.ViewModels;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;

namespace MasterStack.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        // 1. Vari�vel privada para armazenar o contexto
        private readonly ApplicationDbContext _context;

        // 2. O construtor recebe o contexto via Inje��o de Depend�ncia
        
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
{
    // 1. Busca os últimos artigos do blog
            var latestPosts = await _context.BlogPosts
                .Include(p => p.Translations)
                .OrderByDescending(p => p.CreatedAt)
                .Take(3)
                .ToListAsync();

            // 2. Busca as vagas em destaque da entidade JobPosting
            // Busca as vagas do banco de dados
            var featuredJobs = await _context.JobPostings
            .OrderByDescending(j => j.CreatedAt)
            .Take(3)
            .Select(j => new JobItemViewModel
            {
                Id = j.Id,
                Title = j.Title,
                CompanyName = !string.IsNullOrWhiteSpace(j.CompanyName) 
                    ? j.CompanyName 
                    : "Empresa Confidencial",
                    
                Location = !string.IsNullOrWhiteSpace(j.Location) 
                    ? j.Location.Replace("[Adzuna]", "").Trim() 
                    : null,
                    
                // Usa a chave do arquivo de recursos para tradução
                JobType = "Home_Job_Type_FullTime_Remote", 
                
                PostedDate = j.CreatedAt,
                Skills = new List<string>()
            })
            .ToListAsync();

            // 3. Monta e retorna a ViewModel para a View
            var viewModel = new HomeViewModel
            {
                LatestPosts = latestPosts,
                FeaturedJobs = featuredJobs
            };

            return View(viewModel);
}

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult SetLanguage(string culture, string returnUrl)
{
    // Grava o cookie oficial do ASP.NET Core
    Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), Path = "/" }
    );

    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
    {
        // Se o retorno vem de uma rota com cultura no prefixo (ex: /pt-BR/...), substitui o idioma na URL
        var segments = returnUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && new[] { "pt-BR", "en-US", "fr-CA" }.Contains(segments[0]))
        {
            segments[0] = culture;
            returnUrl = "/" + string.Join('/', segments);
        }

        return LocalRedirect(returnUrl);
    }

    return RedirectToAction("Index", "Home", new { culture = culture });
}

 [AllowAnonymous]
[Route("Home/Error/{statusCode?}")]
[Route("{culture}/Home/Error/{statusCode?}")]
public async Task<IActionResult> Error(int? statusCode)
{
    string currentCulture = CultureInfo.CurrentCulture.Name;

    // Captura a URL original do request (ex: "/fr-CA/jhluuvvv")
    var reExecuteFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
    if (reExecuteFeature != null)
    {
        var originalPath = reExecuteFeature.OriginalPath;
        var segments = originalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length > 0)
        {
            var urlCulture = segments[0];
            var supportedCultures = new[] { "pt-BR", "en-US", "fr-CA" };

            if (supportedCultures.Contains(urlCulture))
            {
                currentCulture = urlCulture;

                // Força a cultura para renderizar as traduções
                var cultureInfo = new CultureInfo(currentCulture);
                CultureInfo.CurrentCulture = cultureInfo;
                CultureInfo.CurrentUICulture = cultureInfo;

                HttpContext.Features.Set<IRequestCultureFeature>(
                    new RequestCultureFeature(new RequestCulture(cultureInfo), null)
                );
            }
        }
    }

    // Busca os posts sugeridos no idioma correto
    var sugestoes = await _context.BlogPostTranslations
        .Include(t => t.BlogPost)
        .Where(t => t.Culture == currentCulture)
        .OrderByDescending(t => t.BlogPost.CreatedAt)
        .Take(3)
        .ToListAsync();

    if (!sugestoes.Any())
    {
        sugestoes = await _context.BlogPostTranslations
            .Include(t => t.BlogPost)
            .OrderByDescending(t => t.BlogPost.CreatedAt)
            .Take(3)
            .ToListAsync();
    }

    if (statusCode == 404)
    {
        // Retorna explicitamente o caminho relativo do arquivo da View para evitar erro de busca de arquivo
        return View("~/Views/Home/NotFound.cshtml", sugestoes);
    }

    return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}

        // Certifique-se de que N�O existe atributo [Authorize] nesta Action
        // Remova qualquer atributo [Authorize] do topo da classe se houver, 
        // ou coloque [AllowAnonymous] nesta Action espec�fica.
        // Remova o atributo [Route] antigo e use este que aceita a chamada sem cultura na URL
        [AllowAnonymous]
        [Route("Home/NotFound/{statusCode}")]
        public async Task<IActionResult> NotFoundPage(int statusCode)
        {
            // A cultura ja vira configurada automaticamente via ?culture= da reexecucao
            string currentCulture = CultureInfo.CurrentCulture.Name;

            var suggestedPosts = await _context.BlogPostTranslations
                .Where(t => t.Culture == currentCulture)
                .OrderByDescending(t => t.BlogPost.CreatedAt)
                .Take(3)
                .ToListAsync();

            return View("NotFound", suggestedPosts);
        }

        [Route("{culture}/p/{slug}")] // Usamos "/p/" para encurtar a URL (ex: /pt-BR/p/sobre-nos)
        public async Task<IActionResult> Page(string culture, string slug)
{
    // 1. Busca a página pelo Slug
    var page = await _context.StaticPages
        .Include(p => p.Translations)
        .FirstOrDefaultAsync(p => p.Slug == slug);

    if (page == null) return NotFound(); // Aqui ele daria 404 se o slug estivesse errado

    // 2. Busca a tradução ou a primeira disponível
    var translation = page.Translations.FirstOrDefault(t => t.Culture == culture) 
                      ?? page.Translations.FirstOrDefault();

    if (translation == null) return RedirectToAction("Index", "Home", new { culture = culture });

    return View(translation);
}
    }
}
