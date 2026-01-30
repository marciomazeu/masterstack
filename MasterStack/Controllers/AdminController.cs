using MasterStack.Data; // Ajuste para o seu namespace
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Globalization;
using static System.Net.Mime.MediaTypeNames;

namespace MasterStack.Controllers
{

    [Authorize]
    [Route("{culture}/Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<AdminController> _localizer;

        public AdminController(IStringLocalizer<AdminController> localizer, ApplicationDbContext context)
        {
            _context = context;
            _localizer = localizer;
        }

        [Route("Dashboard")]
        public async Task<IActionResult> Dashboard(string searchTerm, string cultureFilter, int page = 1)
        {
            int pageSize = 10;
            var query = _context.BlogPosts.Include(p => p.Translations).AsQueryable();

            // 1. Filtro por Termo de Busca (Título)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(p => p.Translations.Any(t => t.Title.ToLower().Contains(searchTerm)));
            }

            // 2. Filtro por Cultura (Idioma específico)
            if (!string.IsNullOrEmpty(cultureFilter))
            {
                query = query.Where(p => p.Translations.Any(t => t.Culture == cultureFilter));
            }

            // 3. Contagem e Paginação
            var totalPosts = await query.CountAsync();
            var posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 4. Persistência de Filtros para a View
            ViewData["CurrentFilter"] = searchTerm;
            ViewData["CurrentCulture"] = cultureFilter;
            ViewData["Title"] = _localizer["Welcome"];

            var model = new DashboardViewModel
            {
                Posts = posts,
                PaginaAtual = page,
                TotalPaginas = (int)Math.Ceiling(totalPosts / (double)pageSize)
            };

            // Conta posts por cultura
            ViewBag.PostsPT = await _context.BlogPostTranslations.CountAsync(t => t.Culture == "pt-BR");
            ViewBag.PostsEN = await _context.BlogPostTranslations.CountAsync(t => t.Culture == "en-US");
            ViewBag.PostsFR = await _context.BlogPostTranslations.CountAsync(t => t.Culture == "fr-CA");

            return View(model);
        }
    }
}