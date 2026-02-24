using MasterStack.Data; // Ajuste para o seu namespace
using MasterStack.Models;
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Security.Claims;
using static System.Net.Mime.MediaTypeNames;


namespace MasterStack.Controllers
{

  [Authorize]
public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<AdminController> _localizer;
       private readonly IWebHostEnvironment _webHostEnvironment;
       private readonly UserManager<IdentityUser> _userManager; // Adicione o UserManager

        public AdminController(IStringLocalizer<AdminController> localizer, ApplicationDbContext context,IWebHostEnvironment webHostEnvironment, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _localizer = localizer;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }

        [HttpGet]
        [Route("/pt-BR/Admin/Dashboard")] // Força o reconhecimento imediato
        [Route("{culture}/Admin/Dashboard")]
        public async Task<IActionResult> Dashboard(string searchTerm, string cultureFilter, string status, int page = 1)
{
    int pageSize = 10;
    
    // 1. Iniciamos a query incluindo as traduções
    // 1. ATUALIZADO: Incluímos as traduções E o autor (AuthorProfile)
    var query = _context.BlogPosts
        .Include(p => p.Translations)
        .Include(p => p.Author) // Isso traz o DisplayName e a Foto do autor
        .AsQueryable();

    // 2. Filtro por Termo de Busca (Título em qualquer tradução)
    if (!string.IsNullOrEmpty(searchTerm))
    {
        searchTerm = searchTerm.ToLower();
        query = query.Where(p => p.Translations.Any(t => t.Title.ToLower().Contains(searchTerm)));
    }

    // 3. Filtro por Cultura
    if (!string.IsNullOrEmpty(cultureFilter))
    {
        query = query.Where(p => p.Translations.Any(t => t.Culture == cultureFilter));
    }

    // 4. Filtro por Status (Publicado/Rascunho)
    // Sendo realista: se o post tem várias línguas, ele é rascunho se QUALQUER tradução for rascunho
    // Ou se a tradução específica filtrada for rascunho.
    if (!string.IsNullOrEmpty(status))
    {
        if (status == "published")
            query = query.Where(p => p.Translations.Any(t => t.IsPublished));
        else if (status == "draft")
            query = query.Where(p => p.Translations.Any(t => !t.IsPublished));
    }

    // 5. Execução da Paginação e Busca
    var totalPosts = await query.CountAsync();
    var posts = await query
        .OrderByDescending(p => p.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    // 6. Dados para as Estatísticas e Filtros na View
    ViewData["CurrentFilter"] = searchTerm;
    ViewData["CurrentCulture"] = cultureFilter;
    ViewData["CurrentStatus"] = status ?? "all";
    
    // Contagens rápidas para os cards de estatística
    ViewBag.PostsPT = await _context.BlogPostTranslations.CountAsync(t => t.Culture == "pt-BR");
    ViewBag.PostsEN = await _context.BlogPostTranslations.CountAsync(t => t.Culture == "en-US");
    ViewBag.PostsFR = await _context.BlogPostTranslations.CountAsync(t => t.Culture == "fr-CA");

    var model = new DashboardViewModel
    {
        Posts = posts,
        PaginaAtual = page,
        TotalPaginas = (int)Math.Ceiling(totalPosts / (double)pageSize)
    };

    return View(model);
}

[HttpGet]
[Route("/pt-BR/Admin/Profile")] // Força o reconhecimento imediato
[Route("{culture}/Admin/Profile")]
public async Task<IActionResult> Profile()
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Redirect("/Identity/Account/Login");

    var profile = await _context.AuthorProfiles
        .FirstOrDefaultAsync(p => p.UserId == user.Id);

    // Se o perfil não existir, enviamos uma ViewModel vazia para a View
    // A View decidirá se mostra o formulário de criação ou edição
    var viewModel = new ProfileViewModel();

    if (profile != null)
    {
        viewModel.DisplayName = profile.DisplayName;
        viewModel.Bio = profile.Bio;
        viewModel.TwitterUrl = profile.TwitterUrl;
        viewModel.LinkedInUrl = profile.LinkedInUrl;
        viewModel.GitHubUrl = profile.GitHubUrl;
        viewModel.CurrentImageUrl = profile.ProfileImageUrl;
    }

    return View(viewModel);
}

// REMOVI A DUPLICIDADE AQUI: Apenas um UpdateProfile (POST)
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Challenge();

    // 1. Busca o perfil ou cria um novo vinculado ao ID do usuário logado
    var profile = await _context.AuthorProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
    bool isNew = false;

    if (profile == null)
    {
        profile = new AuthorProfile { UserId = user.Id };
        isNew = true;
    }

    // 2. Lógica de Upload da Imagem (NewImage vem da ProfileViewModel)
    if (model.NewImage != null && model.NewImage.Length > 0)
    {
        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.NewImage.FileName);
        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/profiles");
        
        if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

        var filePath = Path.Combine(uploadPath, fileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await model.NewImage.CopyToAsync(stream);
        }
        profile.ProfileImageUrl = "/uploads/profiles/" + fileName;
    }

    // 3. Atualiza os dados
    profile.DisplayName = model.DisplayName;
    profile.Bio = model.Bio;
    profile.LinkedInUrl = model.LinkedInUrl;
    profile.TwitterUrl = model.TwitterUrl;
    profile.GitHubUrl = model.GitHubUrl;

    if (isNew) _context.AuthorProfiles.Add(profile);
    else _context.Update(profile);

    await _context.SaveChangesAsync();

    TempData["Success"] = "Perfil salvo com sucesso!";
    return RedirectToAction(nameof(Profile));
}

// O método CreateProfile agora apenas redireciona para a View de Profile
[HttpGet]
public IActionResult CreateProfile()
{
    return View("Profile", new ProfileViewModel());
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