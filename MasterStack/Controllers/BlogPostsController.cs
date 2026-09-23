using Ganss.Xss;
using MasterStack.Data;
using MasterStack.Models;
using MasterStack.Services;
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Text;
using System.Text.RegularExpressions;

namespace MasterStack.Controllers
{
    public class BlogPostsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IStringLocalizer<BlogPostsController> _localizer;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BlogPostsController> _logger;
        private readonly ICloudStorageService _cloudStorageService;

        public BlogPostsController(
            ApplicationDbContext context, 
            IWebHostEnvironment webHostEnvironment, 
            IStringLocalizer<BlogPostsController> localizer, 
            UserManager<ApplicationUser> userManager, 
            ICloudStorageService cloudStorageService,
            ILogger<BlogPostsController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _localizer = localizer;
            _userManager = userManager;
            _cloudStorageService = cloudStorageService;
            _logger = logger;            
        }

        // GET: /{culture}/BlogPosts
        [HttpGet]
        public async Task<IActionResult> Index(string culture, int page = 1, string searchTerm = "", bool notfound = false)
        {
            if (notfound)
            {
                TempData["Warning"] = _localizer["TranslationNotFoundMessage"].Value;
            }

            int pageSize = 6;
            var currentCulture = System.Globalization.CultureInfo.CurrentCulture.Name;

            var query = _context.BlogPosts
                .AsNoTracking()
                .Include(p => p.Author)
                .Include(p => p.Translations.Where(t => t.Culture == currentCulture && t.IsPublished))
                .Where(p => p.Translations.Any(t => t.Culture == currentCulture && t.IsPublished))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();
                query = query.Where(p => p.Translations.Any(t =>
                    t.Culture == currentCulture &&
                    (t.Title.Contains(searchTerm) || t.Content.Contains(searchTerm))
                ));
            }

            query = query.OrderByDescending(p => p.CreatedAt);

            var totalPosts = await query.CountAsync();

            var posts = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new BlogPostListViewModel
            {
                Posts = posts,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalPosts / (double)pageSize),
                Culture = culture
            };

            ViewBag.Languages = await _context.Languages.Where(l => l.IsActive).ToListAsync();
            ViewBag.SearchTerm = searchTerm;

            return View(viewModel);
        }

        // GET: /{culture}/blog/{slug}
        [HttpGet]
        [Route("{culture}/blog/{slug}")]
        public async Task<IActionResult> Details(string culture, string slug)
        {
            var entryPoint = await _context.BlogPostTranslations
                .Include(t => t.BlogPost).ThenInclude(p => p.Author)
                .Include(t => t.BlogPost).ThenInclude(p => p.Translations)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == slug);

            if (entryPoint == null) return NotFound();

            var currentTranslation = entryPoint.BlogPost.Translations
                .FirstOrDefault(t => t.Culture.ToLower() == culture.ToLower() && !t.IsDeleted);

            if (currentTranslation == null)
            {
                currentTranslation = entryPoint.BlogPost.Translations
                    .FirstOrDefault(t => !t.IsDeleted);

                if (currentTranslation == null) return NotFound();

                ViewBag.ShowTranslationUnavailable = true;
            }
            else if (currentTranslation.Slug != slug)
            {
                return RedirectToActionPermanent(nameof(Details), new { culture = culture, slug = currentTranslation.Slug });
            }

            // Tratamento unificado de imagem (CDN / Nuvem / Local)
            var imageFileName = "/images/default-post.jpg";
            if (!string.IsNullOrEmpty(currentTranslation.ImageUrl))
            {
                imageFileName = currentTranslation.ImageUrl;
            }

            ViewData["MetaImage"] = imageFileName.StartsWith("http") ? imageFileName : $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}{imageFileName}";
            ViewBag.FinalImagePath = imageFileName;
            ViewBag.CurrentTranslation = currentTranslation;

            return View(currentTranslation.BlogPost);
        }

        // GET: /{culture}/BlogPosts/Create
        [HttpGet]
        [Authorize(Roles = "Admin,Author")]
        public IActionResult Create()
        {
            var languages = _context.Languages.Where(l => l.IsActive).ToList();
            ViewBag.Languages = new SelectList(languages, "Culture", "Name");
            return View();
        }

        // POST: /{culture}/BlogPosts/Create
        [HttpPost]
        [Authorize(Roles = "Admin,Author")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(52428800)] // 50MB
        public async Task<IActionResult> Create([FromRoute] string culture, BlogPostCreateViewModel model)
        {
            if (!ModelState.IsValid) 
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) 
            {
                return RedirectToAction("Login", "Account", new { culture = culture });
            }

            // Upload via DigitalOcean Spaces
            string? imagePath = null;
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                imagePath = await _cloudStorageService.UploadFileAsync(model.ImageFile, "blog");
            }

            var postCulture = !string.IsNullOrWhiteSpace(model.SelectedCulture) ? model.SelectedCulture : culture;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sanitizer = new HtmlSanitizer();
                sanitizer.AllowedAttributes.Add("class");
                string cleanHtml = sanitizer.Sanitize(model.Content ?? string.Empty);

                string baseSlug = !string.IsNullOrWhiteSpace(model.Slug) ? model.Slug : model.Title;
                string uniqueSlug = await GetUniqueSlugAsync(baseSlug, postCulture);

                var post = new BlogPost 
                { 
                    CreatedAt = DateTime.UtcNow,
                    AuthorId = user.Id 
                };
                
                _context.BlogPosts.Add(post);
                await _context.SaveChangesAsync();

                var translation = new BlogPostTranslation
                {
                    BlogPostId = post.Id,
                    Culture = postCulture,
                    Title = model.Title,
                    Content = cleanHtml,
                    MetaDescription = model.MetaDescription,
                    MetaKeywords = model.MetaKeywords,
                    Slug = uniqueSlug,
                    IsPublished = model.IsPublished,
                    ImageUrl = !string.IsNullOrEmpty(imagePath) ? imagePath : "/images/default-post.jpg"
                }; 

                _context.BlogPostTranslations.Add(translation);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["Success"] = "Post criado com sucesso!";
                return RedirectToAction("Dashboard", "Admin", new { culture = postCulture });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                if (!string.IsNullOrEmpty(imagePath) && imagePath.Contains("digitaloceanspaces.com"))
                {
                    _ = _cloudStorageService.DeleteFileAsync(imagePath);
                }

                _logger.LogError(ex, "Erro ao criar BlogPost");
                ModelState.AddModelError("", $"Erro ao salvar o post: {ex.Message}");
                
                return View(model);
            }
        }

        // GET: /{culture}/BlogPosts/EditTranslation/{id}
        [HttpGet]
        [Authorize(Roles = "Admin,Author")]
        [Route("{culture}/[controller]/[action]/{id}")]
        public async Task<IActionResult> EditTranslation(int id)
        {
            var translation = await _context.BlogPostTranslations.FirstOrDefaultAsync(t => t.Id == id);
            if (translation == null) return NotFound();

            var model = new EditTranslationViewModel {
                TranslationId = translation.Id,
                Culture = translation.Culture,
                Title = translation.Title,
                Content = translation.Content,
                Slug = translation.Slug, 
                MetaDescription = translation.MetaDescription,
                MetaKeywords = translation.MetaKeywords, 
                CurrentImageUrl = translation.ImageUrl
            };
            return View(model);
        }

[HttpPost("{culture}/blogposts/EditTranslation/{id}")]
[Authorize(Roles = "Admin,Author")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EditTranslation(int id, EditTranslationViewModel model)
{
    // 💡 Procura diretamente pelo ID da tradução
    var translation = await _context.BlogPostTranslations
        .FirstOrDefaultAsync(t => t.Id == model.TranslationId);

    if (translation == null) return NotFound();

    ModelState.Remove("ImageFile");
    ModelState.Remove("CurrentImageUrl");

    if (!ModelState.IsValid)
    {
        model.CurrentImageUrl = translation.ImageUrl;
        return View(model);
    }

    var slugExists = await _context.BlogPostTranslations
        .AnyAsync(t => t.Slug == model.Slug && t.Culture == model.Culture && t.Id != model.TranslationId);

    if (slugExists)
    {
        model.CurrentImageUrl = translation.ImageUrl;
        ModelState.AddModelError("Slug", "Este Slug já está a ser utilizado noutro artigo nesta língua.");
        return View(model);
    }

    var sanitizer = new Ganss.Xss.HtmlSanitizer();
    translation.Content = sanitizer.Sanitize(model.Content);

    if (!string.IsNullOrEmpty(model.MetaDescription))
    {
        translation.MetaDescription = Regex.Replace(model.MetaDescription, "<.*?>", string.Empty);
    }

    translation.Title = model.Title;
    translation.Slug = model.Slug?.Trim().ToLower();
    translation.MetaKeywords = model.MetaKeywords;
    translation.IsPublished = model.IsPublished;

    var fileToProcess = model.ImageFile;

    if (fileToProcess != null && fileToProcess.Length > 0)
    {
        try
        {
            var oldImageUrl = translation.ImageUrl;
            var uploadedUrl = await _cloudStorageService.UploadFileAsync(fileToProcess, "blog");

            if (!string.IsNullOrEmpty(uploadedUrl))
            {
                // 💡 Apaga a imagem antiga do Spaces se for uma URL remota válida
                if (!string.IsNullOrEmpty(oldImageUrl) && oldImageUrl.StartsWith("http"))
                {
                    await _cloudStorageService.DeleteFileAsync(oldImageUrl);
                }

                // 💡 Procura TODAS as traduções vinculadas ao mesmo BlogPost
                var siblingTranslations = await _context.BlogPostTranslations
                    .Where(t => t.BlogPostId == translation.BlogPostId)
                    .ToListAsync();

                foreach (var sibling in siblingTranslations)
                {
                    sibling.ImageUrl = uploadedUrl;
                    // Força a alteração do estado da entidade no DbContext
                    _context.Entry(sibling).Property(x => x.ImageUrl).IsModified = true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no envio do ficheiro para o DigitalOcean Spaces.");
            ModelState.AddModelError("ImageFile", "Falha ao enviar a nova imagem.");
            model.CurrentImageUrl = translation.ImageUrl;
            return View(model);
        }
    }

    try
    {
        // 💡 Garante a persistência síncrona na base de dados
        await _context.SaveChangesAsync();
        TempData["Success"] = "Tradução atualizada com sucesso!";
        
        return RedirectToAction("Dashboard", "Admin", new { culture = model.Culture });
    }
    catch (DbUpdateConcurrencyException)
    {
        model.CurrentImageUrl = translation.ImageUrl;
        ModelState.AddModelError("", "Erro de concorrência: o registo foi alterado por outro utilizador.");
        return View(model);
    }
}

       // GET: /{culture}/Admin/AddTranslation/{postId}
        [HttpGet]
        [Authorize(Roles = "Admin,Author")]
        [Route("{culture}/Admin/AddTranslation/{postId}")]
        public async Task<IActionResult> AddTranslation(int postId, string culture)
        {
            var post = await _context.BlogPosts.FindAsync(postId);
            if (post == null) return NotFound();

            var viewModel = new AddTranslationViewModel
            {
                BlogPostId = postId,
                SelectedCulture = culture // 💡 Corrigido: usa o parâmetro 'culture' vindo da rota
            };

            await PopulateLanguagesViewBagAsync(culture);

            return View(viewModel);
        }

        // POST: /{culture}/Admin/AddTranslation/{postId}
        [HttpPost]
        [Authorize(Roles = "Admin,Author")]
        [Route("{culture}/Admin/AddTranslation/{postId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTranslation(AddTranslationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateLanguagesViewBagAsync(model.SelectedCulture);
                return View(model);
            }
            
            var sanitizer = new HtmlSanitizer();
            string cleanHtml = sanitizer.Sanitize(model.Content);

            bool alreadyExists = await _context.BlogPostTranslations
                .AnyAsync(t => t.BlogPostId == model.BlogPostId && t.Culture == model.SelectedCulture);

            if (alreadyExists)
            {
                ModelState.AddModelError("SelectedCulture", "Este idioma já existe para este post.");
                await PopulateLanguagesViewBagAsync(model.SelectedCulture);
                return View(model);
            }

            string? dbImagePath = null;
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                dbImagePath = await _cloudStorageService.UploadFileAsync(model.ImageFile, "blog");
            }

            // 💡 Respeita o slug digitado pelo usuário na View. Se estiver vazio, gera a partir do título.
            string rawSlug = string.IsNullOrWhiteSpace(model.Slug) ? model.Title : model.Slug;
            string uniqueSlug = await GetUniqueSlugAsync(rawSlug, model.SelectedCulture);

            var translation = new BlogPostTranslation
            {
                BlogPostId = model.BlogPostId,
                Culture = model.SelectedCulture,
                Title = model.Title,
                Content = cleanHtml,
                Slug = uniqueSlug,
                
                // 💡 SALVANDO A META DESCRIPTION NO BANCO DE DADOS
                MetaDescription = model.MetaDescription,
                
                ImageUrl = dbImagePath ?? "/img/home/default-post.jpg",
                IsPublished = model.IsPublished,
                MetaKeywords = model.MetaKeywords
            };

            _context.BlogPostTranslations.Add(translation);
            await _context.SaveChangesAsync();

            return RedirectToAction("Dashboard", "Admin", new { culture = model.SelectedCulture });
        }

        // Helper para manter a SelectList de idiomas consistente
        private async Task PopulateLanguagesViewBagAsync(string selectedCulture)
        {
            var idiomas = await _context.Languages.Where(l => l.IsActive).ToListAsync();
            ViewBag.Languages = new SelectList(idiomas, "Culture", "Name", selectedCulture);
        }

        // POST: DeleteTranslation
        [HttpPost]
        [Authorize(Roles = "Admin,Author")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTranslation(int id)
        {
            var translation = await _context.BlogPostTranslations.FindAsync(id);
            if (translation == null) return NotFound();

            var blogPostId = translation.BlogPostId;
            var totalTraducoes = await _context.BlogPostTranslations.CountAsync(t => t.BlogPostId == blogPostId);

            if (totalTraducoes <= 1)
            {
                TempData["Error"] = "Você não pode apagar a última tradução. Apague o Post completo se desejar.";
                return RedirectToAction("Dashboard", "Admin");
            }

            if (!string.IsNullOrEmpty(translation.ImageUrl) && translation.ImageUrl.Contains("digitaloceanspaces.com"))
            {
                _ = _cloudStorageService.DeleteFileAsync(translation.ImageUrl);
            }

            _context.BlogPostTranslations.Remove(translation);
            await _context.SaveChangesAsync();

            return RedirectToAction("Dashboard", "Admin");
        }

        // POST: DeletePost
        [HttpPost]
        [Authorize(Roles = "Admin,Author")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePost(int id)
        {
            var post = await _context.BlogPosts
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();

            foreach (var translation in post.Translations)
            {
                if (!string.IsNullOrEmpty(translation.ImageUrl) && translation.ImageUrl.Contains("digitaloceanspaces.com"))
                {
                    _ = _cloudStorageService.DeleteFileAsync(translation.ImageUrl);
                }
            }

            _context.BlogPosts.Remove(post);
            await _context.SaveChangesAsync();

            return RedirectToAction("Dashboard", "Admin");
        }

        // Upload de Imagens do Editor Quill para o DigitalOcean Spaces
        [HttpPost]
        [Authorize(Roles = "Admin,Author")]
        [Route("{culture}/BlogPosts/UploadEditorImage")]
        public async Task<IActionResult> UploadEditorImage(IFormFile image)
        {
            if (image == null || image.Length == 0) return BadRequest("Imagem inválida.");

            var cdnUrl = await _cloudStorageService.UploadFileAsync(image, "blog/editor");

            if (!string.IsNullOrEmpty(cdnUrl))
            {
                return Ok(new { url = cdnUrl });
            }

            return BadRequest("Falha ao processar imagem.");
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> Sitemap()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var posts = await _context.BlogPosts
                .Include(p => p.Translations)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            foreach (var post in posts)
            {
                foreach (var trans in post.Translations)
                {
                    var url = $"{baseUrl}/{trans.Culture}/blog/{trans.Slug}";
                    sb.AppendLine("  <url>");
                    sb.AppendLine($"    <loc>{url}</loc>");
                    sb.AppendLine($"    <lastmod>{post.UpdatedAt:yyyy-MM-dd}</lastmod>");
                    sb.AppendLine("    <changefreq>monthly</changefreq>");
                    sb.AppendLine("    <priority>0.8</priority>");
                    sb.AppendLine("  </url>");
                }
            }

            sb.AppendLine("</urlset>");

            return Content(sb.ToString(), "application/xml");
        }

        #region Helpers
        private string GenerateSlug(string phrase)
        {
            string str = RemoveAccents(phrase).ToLower();
            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
            str = Regex.Replace(str, @"\s+", " ").Trim();
            str = str.Substring(0, str.Length <= 45 ? str.Length : 45).Trim();
            str = Regex.Replace(str, @"\s", "-");
            return str;
        }

        private string RemoveAccents(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        private async Task<string> GetUniqueSlugAsync(string title, string culture, int? currentId = null)
        {
            string slug = GenerateSlug(title);
            string uniqueSlug = slug;
            int count = 1;

            while (await _context.BlogPostTranslations
                .AnyAsync(t => t.Slug == uniqueSlug
                               && t.Culture == culture
                               && (!currentId.HasValue || t.Id != currentId.Value)))
            {
                uniqueSlug = $"{slug}-{count}";
                count++;
            }

            return uniqueSlug;
        }
        #endregion
    }
}