using MasterStack.Data; // Ajuste para o seu namespace
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
       private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminController(IStringLocalizer<AdminController> localizer, ApplicationDbContext context,IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _localizer = localizer;
            _webHostEnvironment = webHostEnvironment;
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

        [HttpGet("/admin/scan-orphaned-images")]
public async Task<IActionResult> ScanOrphanedImages()
{
    var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "blog");
    if (!Directory.Exists(uploadPath)) return Ok(new { count = 0 });

    var physicalFiles = Directory.GetFiles(uploadPath).Select(Path.GetFileName).ToList();
    var translations = await _context.BlogPostTranslations.ToListAsync();
    var dbReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var t in translations)
    {
        if (!string.IsNullOrEmpty(t.ImageUrl)) dbReferences.Add(Path.GetFileName(t.ImageUrl));
        
        var matches = System.Text.RegularExpressions.Regex.Matches(t.Content ?? "", @"<img.+?src=[""'](.+?)[""'].*?>");
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            dbReferences.Add(Path.GetFileName(match.Groups[1].Value));
        }
    }

    // Apenas conta quantos arquivos da pasta não estão no banco
    var count = physicalFiles.Count(f => !dbReferences.Contains(f));
    return Ok(new { count });
}

        [HttpPost("/admin/cleanup-images")] // Rota absoluta, sem depender do padrão global no registro
[ValidateAntiForgeryToken] // Boa prática já que é um POST
public async Task<IActionResult> CleanupImages()
{
    try 
    {
        var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "blog");
        
        if (!Directory.Exists(uploadPath)) 
            return Json(new { success = false, message = "Pasta de uploads não encontrada." });

        // 1. Arquivos físicos
        var physicalFiles = Directory.GetFiles(uploadPath).Select(Path.GetFileName).ToList();

        // 2. Referências no Banco
        var translations = await _context.BlogPostTranslations.ToListAsync();
        var dbReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in translations)
        {
            if (!string.IsNullOrEmpty(t.ImageUrl))
                dbReferences.Add(Path.GetFileName(t.ImageUrl));

            // Regex para pegar imagens no HTML
            var matches = System.Text.RegularExpressions.Regex.Matches(t.Content ?? "", @"<img.+?src=[""'](.+?)[""'].*?>");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var imgPath = match.Groups[1].Value;
                dbReferences.Add(Path.GetFileName(imgPath));
            }
        }

        // 3. Deletar órfãos
        int deletedCount = 0;
        foreach (var fileName in physicalFiles)
        {
            if (!dbReferences.Contains(fileName))
            {
                var fullPath = Path.Combine(uploadPath, fileName);
                System.IO.File.Delete(fullPath);
                deletedCount++;
            }
        }

        return Json(new { success = true, message = $"{deletedCount} imagens removidas com sucesso." });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = "Erro: " + ex.Message });
    }
}
        
    }
}