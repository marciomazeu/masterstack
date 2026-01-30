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
        // 1. Variável privada para armazenar o contexto
        private readonly ApplicationDbContext _context;

        // 2. O construtor recebe o contexto via Injeção de Dependência
        
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // Certifique-se de que NÃO existe atributo [Authorize] nesta Action
        // Remova qualquer atributo [Authorize] do topo da classe se houver, 
        // ou coloque [AllowAnonymous] nesta Action específica.
        [AllowAnonymous]
        [Route("Home/Error/{statusCode?}")]
        [Route("{culture}/Home/Error/{statusCode?}")]
        public async Task<IActionResult> Error(int? statusCode)
        {
            // 1. Buscamos os 3 posts mais recentes para sugerir ao usuário
            // Filtramos pela cultura atual da rota ou padrão pt-BR
            var culture = RouteData.Values["culture"]?.ToString()
                  ?? Request.Query["culture"].ToString()
                  ?? "pt-BR";

            //var sugestoes = await _context.BlogPostTranslations
            //    .Include(t => t.BlogPost)
            //    .Where(t => t.Culture == culture)
            //    .OrderByDescending(t => t.BlogPost.CreatedAt)
            //    .Take(3)
            //    .ToListAsync();
            // Teste radical: pega os 3 primeiros registros da tabela, sem filtro nenhum
            var sugestoes = await _context.BlogPostTranslations.Take(3).ToListAsync();

            // Adicione este Log para ver no console do Visual Studio (Janela de Saída)
            Console.WriteLine($"DEBUG: Total de posts encontrados no banco: {sugestoes.Count}");
            // 2. Se não achou nada, busca os 3 posts mais recentes de QUALQUER idioma
            if (!sugestoes.Any())
            {
                sugestoes = await _context.BlogPostTranslations
                    .Include(t => t.BlogPost)
                    .OrderByDescending(t => t.BlogPost.CreatedAt)
                    .Take(3)
                    .ToListAsync();
            }

            // Se chegar aqui, sabemos que a rota funciona
            if (statusCode == 404)
            {
                return View("NotFound", sugestoes);
            }
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier }); // Certifique-se que Error.cshtml existe em /Views/Shared

        }
    }
}
