using MasterStack.Data;
using MasterStack.Models;
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.IO;

namespace MasterStack.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<SharedResource> _localizer; // Use SharedResource para bater com as views
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(
            IStringLocalizer<SharedResource> localizer, 
            ApplicationDbContext context, 
            IWebHostEnvironment webHostEnvironment, 
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _localizer = localizer;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }

        [HttpGet("{culture}/Admin/Dashboard")]
        [Authorize(Roles = "Admin,Author")]
        public async Task<IActionResult> Dashboard(string culture, string searchTerm, string cultureFilter, string status, int page = 1)
        {
            int pageSize = 10;
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var query = _context.BlogPosts
                .Include(p => p.Translations)
                .Include(p => p.Author)
                .AsQueryable();

            if (!User.IsInRole("Admin"))
                query = query.Where(p => p.AuthorId == currentUser.Id);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(p => p.Translations.Any(t => t.Title.ToLower().Contains(searchTerm)));
            }

            var totalPosts = await query.CountAsync();
            var posts = await query.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.PostsPT = await _context.BlogPostTranslations.CountAsync(t => t.Culture == "pt-BR");
            ViewBag.PostsEN = await _context.BlogPostTranslations.CountAsync(t => t.Culture == "en-US");
            ViewBag.PostsFR = await _context.BlogPostTranslations.CountAsync(t => t.Culture == "fr-CA");

            return View(new DashboardViewModel { Posts = posts, PaginaAtual = page, TotalPaginas = (int)Math.Ceiling(totalPosts / (double)pageSize) });
        }

        [HttpGet("{culture}/Admin/Profile")]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            return View(new ProfileViewModel { 
                DisplayName = user.DisplayName, 
                Bio = user.Bio, 
                TwitterUrl = user.TwitterUrl, 
                LinkedInUrl = user.LinkedInUrl, 
                GitHubUrl = user.GitHubUrl, 
                CurrentImageUrl = user.ProfileImageUrl 
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

           if (!ModelState.IsValid) 
            {
                var userForError = await _userManager.GetUserAsync(User);
                
                // IMPORTANTE: Recarregue os dados que o usuário JÁ TINHA se ele mandou vazio
                // Isso evita que o objeto volte "capado" para a View
                model.CurrentImageUrl = userForError?.ProfileImageUrl;
                
                // Se o erro for apenas na Bio ou redes sociais, mas o nome estava certo,
                // o ModelState deveria aceitar. Se ele trava em tudo, force a limpeza:
                // ModelState.Clear(); // <-- Use apenas se o erro for persistente mesmo com campo cheio
                
                return View("Profile", model);
            }

            string? oldImagePath = null; // Guardamos para deletar APENAS se o banco salvar com sucesso

            if (model.NewImage != null && model.NewImage.Length > 0)
            {
                var uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "profiles");
                if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                // Geramos o nome e o caminho físico
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(model.NewImage.FileName)}";
                var filePath = Path.Combine(uploadFolder, fileName);

                // Salvamos a nova imagem
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.NewImage.CopyToAsync(stream);
                }

                // Se o usuário já tinha uma imagem (e não era a padrão), guardamos o caminho para limpar depois
                if (!string.IsNullOrEmpty(user.ProfileImageUrl) && !user.ProfileImageUrl.Contains("default"))
                {
                    oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, user.ProfileImageUrl.TrimStart('/'));
                }

                user.ProfileImageUrl = "/uploads/profiles/" + fileName;
            }

            user.DisplayName = model.DisplayName;
            user.Bio = model.Bio;
            user.LinkedInUrl = model.LinkedInUrl;
            user.TwitterUrl = model.TwitterUrl;
            user.GitHubUrl = model.GitHubUrl;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded) 
            {
                // SÓ AGORA deletamos a imagem antiga fisicamente
                if (oldImagePath != null && System.IO.File.Exists(oldImagePath))
                {
                    try { System.IO.File.Delete(oldImagePath); } catch { /* Logar erro de IO se necessário */ }
                }

                TempData["Success"] = _localizer["ProfileUpdatedSuccess"].Value;
                return RedirectToAction(nameof(Profile));
            }
            

            // Se falhou o UpdateAsync, temos que remover a imagem nova que foi salva no disco para não virar lixo
            // (Opcional, mas profissional)

            return View("Profile", model);
        }

        // --- SISTEMA DE LIMPEZA DE IMAGENS ---

        [HttpGet("/Admin/scan-orphaned-images")]
        public async Task<IActionResult> ScanOrphanedImages()
        {
            var usedNames = await GetUsedImagesList();
            var physicalPaths = GetAllPhysicalFiles();
            
            // Compara o NOME do arquivo físico com a lista de NOMES usados no banco
            var count = physicalPaths.Count(path => !usedNames.Contains(Path.GetFileName(path)));
            return Ok(new { count });
        }

        [HttpPost("/Admin/cleanup-images")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CleanupImages()
        {
            var usedNames = await GetUsedImagesList();
            var physicalPaths = GetAllPhysicalFiles();
            int deletedCount = 0;

            foreach (var path in physicalPaths)
            {
                var fileName = Path.GetFileName(path);
                if (!usedNames.Contains(fileName))
                {
                    try {
                        System.IO.File.Delete(path);
                        deletedCount++;
                    } catch (Exception ex) {
                        Console.WriteLine($"Falha ao deletar {fileName}: {ex.Message}");
                    }
                }
            }

            return Json(new { success = true, message = $"Sucesso! {deletedCount} imagens órfãs foram removidas." });
        }

        private async Task<HashSet<string>> GetUsedImagesList()
{
    // Pegamos apenas o NOME do arquivo (ex: foto.webp), independente da pasta no banco
    var postImages = await _context.BlogPostTranslations
        .Where(t => t.ImageUrl != null)
        .Select(t => Path.GetFileName(t.ImageUrl))
        .ToListAsync();

    var profileImages = await _context.Users
        .Where(u => u.ProfileImageUrl != null)
        .Select(u => Path.GetFileName(u.ProfileImageUrl))
        .ToListAsync();
    
    var used = postImages.Concat(profileImages)
        .Where(name => !string.IsNullOrEmpty(name))
        .Distinct()
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Proteção de arquivos padrão
    used.Add("default-profile.png");
    used.Add("default-post.jpg");
    used.Add("404.svg"); // Não esqueça dos assets da sua página de erro
    return used;
}

        private List<string> GetAllPhysicalFiles()
{
    // Lista todas as pastas onde você armazena mídia
    var folders = new[] { 
        Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "blog"), // Pasta dos posts
        Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "profiles") // Pasta de perfis
    };
    
    var allFiles = new List<string>();
    foreach(var folder in folders) 
    {
        if(Directory.Exists(folder)) 
        {
            // Busca todos os arquivos dentro das pastas
            allFiles.AddRange(Directory.GetFiles(folder));
        }
    }
    return allFiles;
}

        private List<string> GetPhysicalFilesPath()
        {
             var paths = new[] { 
                Path.Combine(_webHostEnvironment.WebRootPath, "uploads"),
                Path.Combine(_webHostEnvironment.WebRootPath, "uploads/profiles")
            };
            var files = new List<string>();
            foreach(var p in paths) if(Directory.Exists(p)) files.AddRange(Directory.GetFiles(p));
            return files;
        }
    }
}