using MasterStack.Data;
using MasterStack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

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

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            // Grava o cookie que o ASP.NET usa para definir o idioma
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index");
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }

        // Certifique-se de que N�O existe atributo [Authorize] nesta Action
        // Remova qualquer atributo [Authorize] do topo da classe se houver, 
        // ou coloque [AllowAnonymous] nesta Action espec�fica.
        // Remova o atributo [Route] antigo e use este que aceita a chamada sem cultura na URL
[Route("Home/NotFound/{statusCode}")]
public async Task<IActionResult> NotFoundPage(int statusCode)
{
    // O middleware de localização já definiu a cultura do sistema aqui
    var currentCulture = System.Globalization.CultureInfo.CurrentCulture.Name;

    // Busca os posts sugeridos no idioma que o usuário está navegando
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
