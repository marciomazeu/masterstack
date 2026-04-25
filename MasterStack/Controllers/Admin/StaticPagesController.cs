using MasterStack.Data;
using MasterStack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Admin")]
[Route("{culture}/Admin/[controller]")]
public class StaticPagesController : Controller
{
    private readonly ApplicationDbContext _context;

    public StaticPagesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Lista as páginas criadas
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // Removi o filtro .Where para que a View receba TUDO 
        // e ela mesma separe o que é Ativo e o que é Lixeira nas abas
        var pages = await _context.StaticPages
            .Include(p => p.Translations)
            .ToListAsync();
        return View(pages);
    }

[AllowAnonymous]
[HttpGet("/{culture}/p/{slug}")]
public async Task<IActionResult> Page(string culture, string slug)
{
    var page = await _context.StaticPages
    .Include(p => p.Translations)
    .FirstOrDefaultAsync(p => p.Slug == slug && !p.IsDeleted); // <-- Filtro essencial

    if (page == null) return NotFound();

    var translation = page.Translations.FirstOrDefault(t => t.Culture == culture)
                      ?? page.Translations.FirstOrDefault(t => t.Culture.StartsWith(culture.Split('-')[0]))
                      ?? page.Translations.FirstOrDefault();

    if (translation == null) return NotFound();

    // --- IMPORTANTE: Alimenta as Meta Tags para o Layout ---
    ViewData["Title"] = !string.IsNullOrWhiteSpace(translation.SeoTitle) 
                        ? translation.SeoTitle 
                        : translation.Title;

    ViewData["Description"] = translation.SeoDescription;

    return View(translation);
}

    // GET: Criar Nova Página
    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View();
    }

    // POST: Salvar Página e as Traduções
[HttpPost("Create")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(StaticPage staticPage, List<StaticPageTranslation> translations)
{
    // 1. Filtra apenas as traduções que REALMENTE têm título e conteúdo
    var validTranslations = translations
        .Where(t => !string.IsNullOrWhiteSpace(t.Title) && !string.IsNullOrWhiteSpace(t.Content))
        .ToList();

    if (validTranslations.Any())
    {
        staticPage.Translations = validTranslations;
        _context.StaticPages.Add(staticPage);
        await _context.SaveChangesAsync();
        
        var culture = RouteData.Values["culture"]?.ToString() ?? "pt-BR";
        return RedirectToAction(nameof(Index), new { culture = culture }); // Adicione isso
    }

    // Se chegou aqui, nada foi preenchido corretamente
    ModelState.AddModelError("", "Preencha ao menos um idioma completamente.");
    return View(staticPage);
}
    [HttpGet("Edit/{id}")] // Rota específica: /pt-BR/Admin/StaticPages/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var staticPage = await _context.StaticPages
            .Include(p => p.Translations)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (staticPage == null) return NotFound();

        return View(staticPage);
    }

[HttpPost("Edit/{id}")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(int id, StaticPage staticPage)
{
    if (id != staticPage.Id) return NotFound();

    // 1. Busca a página original com as traduções atuais do banco
    var pageInDb = await _context.StaticPages
        .Include(p => p.Translations)
        .FirstOrDefaultAsync(m => m.Id == id);

    if (pageInDb == null) return NotFound();

    try
    {
        // 2. Atualiza os dados da página principal
        pageInDb.Slug = staticPage.Slug;

        // 3. Atualiza as traduções uma por uma
        foreach (var submittedTrans in staticPage.Translations)
        {
            var dbTrans = pageInDb.Translations
                .FirstOrDefault(t => t.Culture == submittedTrans.Culture);

            if (dbTrans != null)
            {
                // Atualiza os campos existentes, incluindo os novos de SEO
                dbTrans.Title = submittedTrans.Title;
                dbTrans.Content = submittedTrans.Content;
                dbTrans.SeoTitle = submittedTrans.SeoTitle; // <--- NOVO
                dbTrans.SeoDescription = submittedTrans.SeoDescription; // <--- NOVO
            }
            else if (!string.IsNullOrWhiteSpace(submittedTrans.Title))
            {
                // Se for um idioma novo que não existia no banco antes
                pageInDb.Translations.Add(submittedTrans);
            }
        }

        await _context.SaveChangesAsync();
        
        var culture = RouteData.Values["culture"]?.ToString() ?? "pt-BR";
        return RedirectToAction(nameof(Index), new { culture = culture });
    }
    catch (Exception ex)
    {
        ModelState.AddModelError("", "Erro ao salvar: " + ex.Message);
        return View(staticPage);
    }
}

   [HttpPost("Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var page = await _context.StaticPages.FindAsync(id);
        if (page != null)
        {
            page.IsDeleted = true;
            page.DeletedAt = DateTime.Now;
            _context.Entry(page).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        // Pega a cultura atual da rota para não quebrar o redirecionamento
        var culture = RouteData.Values["culture"]?.ToString() ?? "pt-BR";
        
        // Redireciona passando a cultura de volta
        return RedirectToAction(nameof(Index), new { culture = culture });
    }

    [HttpGet("Trash")]
    public async Task<IActionResult> Trash()
    {
        // Mostra apenas o que foi deletado
        var deletedPages = await _context.StaticPages
            .Include(p => p.Translations)
            .Where(p => p.IsDeleted)
            .ToListAsync();
            
        return View(deletedPages);
    }

    // POST: Admin/StaticPages/Restore/5
    [HttpPost("Restore/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var page = await _context.StaticPages.FindAsync(id);
        if (page == null) return NotFound();

        page.IsDeleted = false;
        page.DeletedAt = null;

        _context.Entry(page).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        // Captura a cultura da rota atual
        var culture = RouteData.Values["culture"]?.ToString() ?? "pt-BR";

        // Força o retorno para a listagem principal do Admin
        return LocalRedirect($"/{culture}/Admin/StaticPages");
    }

    // Opcional: Hard Delete (Apagar do banco de vez)
   [HttpPost("HardDelete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(int id)
    {
        // Busca a página incluindo as traduções para garantir que o EF apague tudo em cascata
        var page = await _context.StaticPages
            .Include(p => p.Translations)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (page != null)
        {
            _context.StaticPages.Remove(page);
            await _context.SaveChangesAsync();
        }
        
        // Captura a cultura para o redirecionamento
        var culture = RouteData.Values["culture"]?.ToString() ?? "pt-BR";
        
        // Redirecionamento blindado contra 404
        return LocalRedirect($"/{culture}/Admin/StaticPages");
    }
}