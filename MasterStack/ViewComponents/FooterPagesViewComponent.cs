using MasterStack.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MasterStack.ViewComponents
{
    public class FooterPagesViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public FooterPagesViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Pega a cultura atual da URL (ex: pt-BR)
            var culture = RouteData.Values["culture"]?.ToString() ?? "pt-BR";

            // Busca apenas páginas ATIVAS que tenham tradução para o idioma atual
            var pages = await _context.StaticPages
                .Include(p => p.Translations)
                .Where(p => !p.IsDeleted)
                .ToListAsync();

            // Passamos a cultura para a View para montar os links corretamente
            ViewBag.CurrentCulture = culture;

            return View(pages);
        }
    }
}