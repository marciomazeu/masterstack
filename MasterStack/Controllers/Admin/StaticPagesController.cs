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
        return View(await _context.StaticPages.Include(p => p.Translations).ToListAsync());
    }

[AllowAnonymous] // Permite que visitantes vejam, mesmo sem serem Admin
[HttpGet("/{culture}/p/{slug}")]
public async Task<IActionResult> Page(string culture, string slug)
{
    // 1. Busca a página e TODAS as suas traduções
    var page = await _context.StaticPages
        .Include(p => p.Translations)
        .FirstOrDefaultAsync(p => p.Slug == slug);

    // 2. Se a slug não existe no banco (ex: digitou errado), aí sim é 404
    if (page == null) return NotFound();

    // 3. Tenta buscar a tradução exata do idioma da URL
    var translation = page.Translations.FirstOrDefault(t => t.Culture == culture);

    // 4. SE NÃO ACHOU (Plano B): Tenta buscar apenas pelo prefixo (ex: 'en' em vez de 'en-US')
    if (translation == null)
    {
        var shortCulture = culture.Split('-')[0]; // Pega apenas o "en" ou "pt"
        translation = page.Translations.FirstOrDefault(t => t.Culture.StartsWith(shortCulture));
    }

    // 5. SE AINDA NÃO ACHOU (Plano C): Pega a primeira tradução que existir (Geralmente o PT-BR)
    if (translation == null)
    {
        translation = page.Translations.FirstOrDefault();
    }

    // Se a página existe mas não tem NENHUMA tradução (erro de cadastro), 404
    if (translation == null) return NotFound();

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
        return RedirectToAction(nameof(Index));
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
public async Task<IActionResult> Edit(int id, StaticPage staticPage, List<StaticPageTranslation> translations)
{
    if (id != staticPage.Id) return NotFound();

    try
    {
        // 1. Limpa qualquer rastreamento residual para evitar o erro de "already tracked"
        _context.ChangeTracker.Clear();

        // 2. Atualiza a página principal (Slug, etc)
        _context.Entry(staticPage).State = EntityState.Modified;

        // 3. Itera sobre as traduções enviadas no Payload
        foreach (var translation in translations)
        {
            // Vincula ao ID da página pai por segurança
            translation.StaticPageId = id;

            if (translation.Id > 0)
            {
                // Se já existe (como o seu fr-CA que tem Id 3), forçamos o estado de modificado
                _context.Entry(translation).State = EntityState.Modified;
            }
            else
            {
                // Se fosse uma tradução nova (Id 0)
                _context.StaticPageTranslations.Add(translation);
            }
        }

        await _context.SaveChangesAsync();

        // 4. Redireciona para a Home ou Index usando a cultura da rota
        var culture = RouteData.Values["culture"]?.ToString() ?? "pt-BR";
        return RedirectToAction("Index", "Home", new { culture = culture });
    }
    catch (Exception ex)
    {
        // Se der erro, você conseguirá ver o motivo aqui no Debug
        ModelState.AddModelError("", "Erro ao salvar: " + ex.Message);
        return View(staticPage);
    }
}
}