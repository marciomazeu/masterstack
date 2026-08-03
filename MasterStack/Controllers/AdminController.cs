using MasterStack.Data;
using MasterStack.Models;
using MasterStack.Services;
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
[Authorize(Roles = "Admin,Author")]
[Route("{culture?}/Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly GeminiAiService _geminiAiService; // 🔥 Mantido aqui

        public AdminController(
            IStringLocalizer<SharedResource> localizer, 
            ApplicationDbContext context, 
            IWebHostEnvironment webHostEnvironment, 
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            GeminiAiService geminiAiService
        )
        {
            _context = context;
            _localizer = localizer;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _roleManager = roleManager;
            _geminiAiService = geminiAiService; // 🔥 Agora ele NUNCA será nulo
        }

        [HttpGet]
[Route("Dashboard")]
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

        [HttpGet("Users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Users(string searchTerm, string roleFilter, string status, int page = 1)
        {
            int pageSize = 15;
            
            // 1. Iniciamos a query básica
            var query = _userManager.Users.AsQueryable();

            // 2. Filtro por Busca (Nome/Email)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(u => u.Email.ToLower().Contains(searchTerm) || 
                                        u.DisplayName.ToLower().Contains(searchTerm));
            }

            // 3. Filtro por Status (Ativo/Bloqueado)
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "locked")
                {
                    query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
                }
                else if (status == "active")
                {
                    query = query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow);
                }
            }

            // 4. Filtro por Role (Nível de Acesso)
            // Usamos o Join com UserRoles para filtrar direto no Banco de Dados
            if (!string.IsNullOrEmpty(roleFilter))
            {
                var role = await _roleManager.FindByNameAsync(roleFilter);
                if (role != null)
                {
                    query = query.Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == role.Id));
                }
            }

            var totalUsers = await query.CountAsync();
            
            // 5. Paginação e Projeção (Buscamos apenas o necessário)
            var usersData = await query
                .OrderBy(u => u.DisplayName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 6. Preenchimento do ViewModel
            var userList = new List<UserListViewModel>();
            foreach (var user in usersData)
            {
                userList.Add(new UserListViewModel
                {
                    Id = user.Id,
                    DisplayName = user.DisplayName ?? "N/A",
                    Email = user.Email ?? string.Empty,
                    // Roles e Lockout ainda precisam ser verificados via UserManager
                    Roles = await _userManager.GetRolesAsync(user),
                    IsEmailConfirmed = user.EmailConfirmed,
                    IsLockedOut = await _userManager.IsLockedOutAsync(user)
                });
            }

            // 7. Dados para a View (Preservando filtros nos links de paginação)
            ViewBag.TotalPaginas = (int)Math.Ceiling(totalUsers / (double)pageSize);
            ViewBag.PaginaAtual = page;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.StatusFilter = status;
            ViewBag.RoleFilter = roleFilter;
            
            // Lista de Roles para o Dropdown do Filtro
            ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();

            return View(userList);
        }
        

        // --- SISTEMA DE LIMPEZA DE IMAGENS ---

        [HttpGet("/Admin/scan-orphaned-images")]
        [Authorize(Roles = "Admin")]
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
        [IgnoreAntiforgeryToken]
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

        [HttpPost("ToggleLock/{userId}")] // Adicione o parâmetro na rota aqui também!
    [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            
            if (user == null) return NotFound();

            // Impede que você bloqueie a si mesmo (auto-lockout)
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == user.Id)
            {
                TempData["Error"] = "Você não pode bloquear sua própria conta.";
                return RedirectToAction(nameof(Users));
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                // Desbloqueia definindo a data de fim para agora
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow);
                TempData["Success"] = $"Usuário {user.Email} desbloqueado.";
            }
            else
            {
                // Bloqueia por 100 anos
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                TempData["Success"] = $"Usuário {user.Email} bloqueado com sucesso.";
            }

            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
[Route("EditUser/{id}")]
public async Task<IActionResult> EditUser(string id)
{
    var user = await _userManager.FindByIdAsync(id);
    if (user == null) return NotFound();

    var model = new EditUserViewModel
    {
        Id = user.Id,
        DisplayName = user.DisplayName ?? string.Empty,
        Email = user.Email ?? string.Empty,
        UserRoles = (await _userManager.GetRolesAsync(user)).ToList(),
        // Usando a solução que remove o erro de nullability:
        AllRoles = _roleManager.Roles
            .Select(r => r.Name)
            .Where(n => n != null)
            .Cast<string>()
            .ToList()
    };

    // Forçamos o nome da View para não haver erro de localização de arquivo
    return View("EditUser", model); 
}

        [HttpPost("EditUser/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model, List<string> selectedRoles)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.DisplayName = model.DisplayName;
            // user.Email = model.Email; // Geralmente admin não muda email, mas pode habilitar se quiser

            var userRoles = await _userManager.GetRolesAsync(user);
            
            // Atualiza os dados básicos
            var updateResult = await _userManager.UpdateAsync(user);

            // Verificação sugerida para o seu POST de EditUser:
            if (user.Id == _userManager.GetUserId(User) && !selectedRoles.Contains("Admin"))
            {
                TempData["Error"] = "Operação negada: Você não pode remover seu próprio nível de administrador.";
                return RedirectToAction(nameof(Users));
            }
            
            if (updateResult.Succeeded)
            {
                // Gerenciamento de Roles: Remove as antigas e adiciona as novas
                await _userManager.RemoveFromRolesAsync(user, userRoles);
                await _userManager.AddToRolesAsync(user, selectedRoles);

                TempData["Success"] = "Usuário atualizado com sucesso.";
                return RedirectToAction(nameof(Users));
            }

            return View(model);
        }
    

    [HttpPost]
    [Route("[action]")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> GenerateAiContent()
    {
        // 🔥 CORREÇÃO: Lendo direto do formulário para evitar quebras de binding ou XSS do .NET
        string topic = Request.Form["topic"].ToString();
        string culture = Request.Form["culture"].ToString();
        string length = Request.Form["length"].ToString();
        string? opinion = Request.Form["opinion"].ToString();

        if (string.IsNullOrWhiteSpace(topic))
            return BadRequest("O tema não pode estar vazio.");

        try
        {
            // 🔥 Chamando o serviço correto (_geminiAiService) com os 4 parâmetros alinhados
            var result = await _geminiAiService.GeneratePostSuggestionAsync(topic, culture, length, opinion);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CRITICAL ERROR IN GENERATE]: {ex.Message} -> {ex.StackTrace}");
            return StatusCode(500, $"Erro interno no serviço de IA: {ex.Message} -> {ex.InnerException?.Message}");
        }
    }

    [HttpPost]
    [Route("[action]")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RefineAiContent()
    {
        string culture = Request.Form["culture"].ToString();
        string opinion = Request.Form["opinion"].ToString();
        string currentContent = Request.Form["currentContent"].ToString();

        if (string.IsNullOrEmpty(currentContent) || string.IsNullOrEmpty(opinion))
        {
            return BadRequest("O conteúdo atual e a opinião são obrigatórios para o refinamento.");
        }

        try
        {
            // 🔥 Ajustado para usar o _geminiAiService correto também
            var result = await _geminiAiService.RefinePostAsync(currentContent, opinion, culture);
            
            if (result == null)
            {
                return StatusCode(500, "A IA retornou uma resposta inválida.");
            }

            return Ok(new {
                title = result.Title ?? "",
                slug = result.Slug ?? "",
                metaDescription = result.MetaDescription ?? "",
                content = result.Content ?? ""
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CRITICAL ERROR IN REFINE]: {ex.Message} -> {ex.StackTrace}");
            return StatusCode(500, $"Erro interno no refinamento: {ex.Message}");
        }
    }
    }
}