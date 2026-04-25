using Ganss.Xss;
using MasterStack.Data; // Ajuste para o seu namespace de dados
using MasterStack.Models;
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SkiaSharp;
using System.Text;
using System.Text.RegularExpressions;
namespace MasterStack.Controllers
{
    
    public class BlogPostsController : Controller
    {
        private readonly List<string> _supportedCultures = new List<string> { "pt-BR", "en-US", "fr-FR" };
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IStringLocalizer<BlogPostsController> _localizer; // Adicione esta linha
        private readonly UserManager<ApplicationUser> _userManager;
        

        public BlogPostsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, IStringLocalizer<BlogPostsController> localizer, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _localizer = localizer; // Atribua ao campo privado
            _userManager = userManager;
            
        }

        // GET: /pt-BR/BlogPosts
        [HttpGet]
        public async Task<IActionResult> Index(string culture, int page = 1, string searchTerm = "", bool notfound = false)
        {
        
            if (notfound)
            {
                TempData["Warning"] = _localizer["TranslationNotFoundMessage"].Value;
            }

            int pageSize = 6;
            var currentCulture = System.Globalization.CultureInfo.CurrentCulture.Name;

            // 1. Iniciamos a Query básica (apenas posts que tenham o idioma atual)
           var query = _context.BlogPosts
            .AsNoTracking()
            .Include(p => p.Author) // <--- ADICIONE ESTA LINHA AQUI
            .Include(p => p.Translations)
            .Where(p => p.Translations.Any(t => t.Culture == currentCulture && t.IsPublished))
            .AsQueryable(); // Importante para permitir adicionar filtros depois

            // 2. AQUI ESTAVA O PROBLEMA: Aplicar o filtro de busca ANTES de contar e paginar
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();
                query = query.Where(p => p.Translations.Any(t =>
                    t.Culture == currentCulture &&
                    (t.Title.Contains(searchTerm) || t.Content.Contains(searchTerm))
                ));
            }

            // 3. Ordenação (sempre depois dos filtros)
            query = query.OrderByDescending(p => p.CreatedAt);

            // 4. Agora sim: Contagem total baseada no resultado (com ou sem busca)
            var totalPosts = await query.CountAsync();

            // 5. Paginação Real (Skip/Take)
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
            ViewBag.SearchTerm = searchTerm; // Devolve para a View manter o valor no input/links

            return View(viewModel);
        }

        // GET: /pt-BR/BlogPosts/post/5
        [HttpGet]
        [Route("{culture}/blog/{slug}")]
        public async Task<IActionResult> Details(string culture, string slug)
        {
           var currentTranslation = await _context.BlogPostTranslations
            .Include(t => t.BlogPost)
                .ThenInclude(p => p.Author) // <--- O Autor está aqui no Post Pai
            .Include(t => t.BlogPost)
                .ThenInclude(p => p.Translations)
            .FirstOrDefaultAsync(t => t.Slug == slug);

            if (currentTranslation == null) return NotFound();

            // 1. Lógica de Redirecionamento (Já estava ótima)
            if (currentTranslation.Culture.ToLower() != culture.ToLower())
            {
                var targetTranslation = currentTranslation.BlogPost.Translations
                    .FirstOrDefault(t => t.Culture.ToLower() == culture.ToLower());

                if (targetTranslation != null)
                {
                    return RedirectToAction(nameof(Details), new
                    {
                        culture = targetTranslation.Culture,
                        slug = targetTranslation.Slug
                    });
                }
                ViewBag.TranslationWarning = true;
            }

            // 2. LÓGICA DA IMAGEM (Onde estava o erro)
            var imageFileName = "/uploads/default-post.jpg";
            var request = HttpContext.Request;

            if (!string.IsNullOrEmpty(currentTranslation.ImageUrl))
            {
                // Usando o ambiente injetado para validar o arquivo na wwwroot
                var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, currentTranslation.ImageUrl.TrimStart('/'));

                if (System.IO.File.Exists(physicalPath))
                {
                    imageFileName = currentTranslation.ImageUrl;
                }
            }

            // Para as Meta Tags (Social Media)
            ViewData["MetaImage"] = $"{request.Scheme}://{request.Host}{imageFileName.Replace("\\", "/")}";
            // Para a View usar no <img> tag
            ViewBag.FinalImagePath = imageFileName;

            ViewBag.CurrentTranslation = currentTranslation;

            return View(currentTranslation.BlogPost);
        }

        // GET: /pt-BR/BlogPosts/Create
        [HttpGet]
        public IActionResult Create()
        {
            // BUSCA NO BANCO: Se o banco estiver vazio, o menu some.
            var languages = _context.Languages.Where(l => l.IsActive).ToList();

            // Passa para a View
            ViewBag.Languages = new SelectList(languages, "Culture", "Name");
            return View();
        }

        // POST: BlogPosts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
[ValidateAntiForgeryToken]
[RequestSizeLimit(52428800)] // 50MB
public async Task<IActionResult> Create(BlogPostCreateViewModel model, string? culture)
{
    if (!ModelState.IsValid) return View(model);

    var user = await _userManager.GetUserAsync(User);
    if (user == null) return RedirectToAction("Login", "Account");

    // Processa a imagem ANTES da transação para ter o caminho
    string? imagePath = await ProcessAndSaveWebP(model.ImageFile);

    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        // Higienização robusta
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedAttributes.Add("class"); // Mantém formatação do Quill
        string cleanHtml = sanitizer.Sanitize(model.Content);

        string uniqueSlug = await GetUniqueSlugAsync(model.Title, culture ?? "pt-BR");

        var post = new BlogPost 
        { 
            CreatedAt = DateTime.Now,
            AuthorId = user.Id 
        };
        
        _context.BlogPosts.Add(post);
        await _context.SaveChangesAsync();

        var currentCulture = culture ?? model.SelectedCulture ?? "pt-BR";

        var translation = new BlogPostTranslation
        {
            BlogPostId = post.Id,
            Culture = currentCulture,
            Title = model.Title,
            Content = cleanHtml,
            MetaDescription = model.MetaDescription,
            MetaKeywords = model.MetaKeywords,
            Slug = uniqueSlug,
            ImageUrl = imagePath ?? "/uploads/blog/default-post.jpg"
        }; 

        _context.BlogPostTranslations.Add(translation);
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        TempData["Success"] = "Post criado com sucesso!";
        return RedirectToAction("Dashboard", "Admin", new { culture = currentCulture });
    }
    catch (Exception)
    {
        await transaction.RollbackAsync();
        
        // Limpeza de segurança: Se deu erro no banco, deleta a imagem física
        if (!string.IsNullOrEmpty(imagePath) && imagePath != "/uploads/blog/default-post.jpg")
        {
            var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, imagePath.TrimStart('/'));
            if (System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);
        }

        ModelState.AddModelError("", "Erro técnico ao salvar. O arquivo foi removido e os dados protegidos.");
        return View(model);
    }
}
        private string GenerateSlug(string phrase)
{
    // 1. Remove acentos (Transforma 'ã' em 'a', 'é' em 'e')
    string str = RemoveAccents(phrase).ToLower();

    // 2. Remove caracteres inválidos (mantém apenas letras, números e espaços)
    str = System.Text.RegularExpressions.Regex.Replace(str, @"[^a-z0-9\s-]", "");

    // 3. Converte múltiplos espaços em um só
    str = System.Text.RegularExpressions.Regex.Replace(str, @"\s+", " ").Trim();

    // 4. Limita o tamanho (URLs muito longas são ruins para SEO)
    str = str.Substring(0, str.Length <= 45 ? str.Length : 45).Trim();

    // 5. Troca espaços por hifens
    str = System.Text.RegularExpressions.Regex.Replace(str, @"\s", "-");

    return str;
}

// Método auxiliar para tratar os acentos (essencial para PT-BR e FR)
private string RemoveAccents(string text)
{
    var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
    var stringBuilder = new System.Text.StringBuilder();

    foreach (var c in normalizedString)
    {
        var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
        if (unicodeCategory != System.Globalization.CharUnicodeInfo.GetUnicodeCategory('a')) // NonSpacingMark
        {
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }
    }
    return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
}

        private async Task<string> GetUniqueSlugAsync(string title, string culture, int? currentId = null)
        {
            string slug = GenerateSlug(title);
            string uniqueSlug = slug;
            int count = 1;

            // Adicionamos a verificação da Culture no AnyAsync
            while (await _context.BlogPostTranslations
                .AnyAsync(t => t.Slug == uniqueSlug
                               && t.Culture == culture // SÓ conflita se for o mesmo idioma
                               && (!currentId.HasValue || t.Id != currentId.Value)))
            {
                uniqueSlug = $"{slug}-{count}";
                count++;
            }

            return uniqueSlug;
        }

        // GET: BlogPosts/Edit/5?culture=en-US

        // GET: BlogPosts/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(string culture, int? id)
        {
            if (id == null) return NotFound();

            var translation = await _context.BlogPostTranslations
                .Include(t => t.BlogPost)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (translation == null) return NotFound();

            return View(translation);
        }

        // POST: BlogPosts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,BlogPostId,Culture,Title,Content,ImageUrl,Slug,MetaDescription,MetaKeywords,IsPublished")] BlogPostTranslation translation, IFormFile? novaImagem)
        {
            if (id != translation.Id) return NotFound();

            // 1. Geração de Slug Único
            // Assume-se que você tem este método implementado no Controller ou num Service
            translation.Slug = await GetUniqueSlugAsync(translation.Title, translation.Culture, translation.Id);
            ModelState.Remove("Slug");

            if (ModelState.IsValid)
            {
                try
                {
                    // 2. Sanitização de Segurança (Anti-XSS)
                    var sanitizer = new HtmlSanitizer();
                    translation.Content = sanitizer.Sanitize(translation.Content);

                    // 3. Sanitização de SEO (Remover HTML da descrição)
                    if (!string.IsNullOrEmpty(translation.MetaDescription))
                    {
                        translation.MetaDescription = Regex.Replace(translation.MetaDescription, "<.*?>", string.Empty);
                    }

                    // 4. Gerenciamento de Imagem (Físico + Banco)
                    if (novaImagem != null && novaImagem.Length > 0)
                    {
                        // Recupera o caminho antigo para limpeza
                        var oldDbPath = await _context.BlogPostTranslations
                            .Where(t => t.Id == id)
                            .Select(t => t.ImageUrl)
                            .AsNoTracking()
                            .FirstOrDefaultAsync();

                        // Processa e Salva a Nova Imagem (WebP)
                        string? newPath = await ProcessAndSaveWebP(novaImagem);

                        if (!string.IsNullOrEmpty(newPath))
                        {
                            // Deleta o arquivo físico antigo se existir
                            if (!string.IsNullOrEmpty(oldDbPath))
                            {
                                var oldFileName = Path.GetFileName(oldDbPath);
                                string fullOldPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "blog", oldFileName);
                                
                                if (System.IO.File.Exists(fullOldPath))
                                {
                                    System.IO.File.Delete(fullOldPath);
                                }
                            }
                            
                            translation.ImageUrl = newPath;
                        }
                    }

                    _context.Update(translation);
                    await _context.SaveChangesAsync();
                    
                    TempData["Success"] = "Post atualizado com sucesso!";
                    return RedirectToAction("Dashboard", "Admin", new { culture = translation.Culture });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TranslationExists(translation.Id)) return NotFound();
                    else throw;
                }
            }
            return View(translation);
        }

        // 1. Rota para abrir o formulário
[HttpGet]
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTranslation(int id, EditTranslationViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // 1. Busca a tradução
            var translation = await _context.BlogPostTranslations
                .FirstOrDefaultAsync(t => t.Id == model.TranslationId);

            if (translation == null) return NotFound();

            // 2. Validação de Slug Único (Excelente lógica já implementada)
            var slugExists = await _context.BlogPostTranslations
                .AnyAsync(t => t.Slug == model.Slug && t.Culture == model.Culture && t.Id != model.TranslationId);

            if (slugExists)
            {
                ModelState.AddModelError("Slug", "Este Slug já está sendo usado em outro post desta língua.");
                return View(model);
            }

            // --- NOVIDADE: SANITIZAÇÃO (O "Pulo do Gato" para o Roadmap) ---
            var sanitizer = new Ganss.Xss.HtmlSanitizer();
            
            // Protege contra scripts maliciosos no editor Quill
            translation.Content = sanitizer.Sanitize(model.Content);

            // Garante que a descrição do Google não tenha tags HTML residuais
            if (!string.IsNullOrEmpty(model.MetaDescription))
            {
                translation.MetaDescription = System.Text.RegularExpressions.Regex
                    .Replace(model.MetaDescription, "<.*?>", string.Empty);
            }
            // ---------------------------------------------------------------

            // 3. Atualiza os campos
            translation.Title = model.Title;
            translation.Slug = model.Slug?.Trim().ToLower(); 
            translation.MetaKeywords = model.MetaKeywords;
            translation.IsPublished = model.IsPublished;

            // 4. Lógica de Imagem (Mantive sua lógica de deleção física, que está correta)
            if (model.NewImage != null && model.NewImage.Length > 0)
            {
                string? oldImageUrl = translation.ImageUrl;
                string? newWebPPath = await ProcessAndSaveWebP(model.NewImage);

                if (newWebPPath != null)
                {
                    translation.ImageUrl = newWebPPath;
                    
                    // Deleta o arquivo antigo para não entulhar o servidor
                    if (!string.IsNullOrEmpty(oldImageUrl))
                    {
                        var relativePath = oldImageUrl.TrimStart('/');
                        var fullOldPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath);
                        
                        if (System.IO.File.Exists(fullOldPath)) 
                        {
                            System.IO.File.Delete(fullOldPath);
                        }
                    }
                }
                else
                {
                    ModelState.AddModelError("NewImage", "Erro ao processar a imagem.");
                    return View(model);
                }
            }

            // 5. Persistência
            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Tradução atualizada com sucesso!";
                return RedirectToAction("Dashboard", "Admin", new { culture = model.Culture });
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("", "Erro de concorrência: o registro foi alterado por outro usuário.");
                return View(model);
            }
        }

        // GET: BlogPosts/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.BlogPosts
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (post == null) return NotFound();

            return View(post);
        }

        // POST: BlogPosts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // 1. Buscamos o Post Pai incluindo TODAS as traduções vinculadas
            var blogPost = await _context.BlogPosts
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (blogPost != null)
            {
                try
                {
                    // 2. Ciclo de limpeza de arquivos físicos
                    foreach (var translation in blogPost.Translations)
                    {
                        if (!string.IsNullOrEmpty(translation.ImageUrl))
                        {
                            // Monta o caminho completo do arquivo no servidor
                            string filePath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", translation.ImageUrl);

                            // Verifica se o arquivo existe e deleta
                            if (System.IO.File.Exists(filePath))
                            {
                                System.IO.File.Delete(filePath);
                            }
                        }
                    }

                    // 3. Remove o Post Pai (isso vai remover as traduções automaticamente 
                    // se o Cascade Delete estiver ativo no banco)
                    _context.BlogPosts.Remove(blogPost);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Log do erro se necessário
                    TempData["Error"] = "Erro ao excluir: " + ex.Message;
                    return RedirectToAction(nameof(Admin));
                }
            }

            return RedirectToAction(nameof(Admin));
        }

        private bool BlogPostExists(int id)
        {
            return _context.BlogPosts.Any(e => e.Id == id);
        }

        // 1. O GET (Para abrir o formulário)
        [HttpGet]
        [Route("{culture}/Admin/AddTranslation/{postId}")]
        public async Task<IActionResult> AddTranslation(int postId, string targetCulture)
        {
            var post = await _context.BlogPosts.FindAsync(postId);
            if (post == null) return NotFound();

            // MUDANÇA AQUI: Use BlogPostTranslationViewModel em vez de Create
            var viewModel = new AddTranslationViewModel
            {
                BlogPostId = postId,
                SelectedCulture = targetCulture
            };

            var idiomas = await _context.Languages.Where(l => l.IsActive).ToListAsync();
            ViewBag.Languages = new SelectList(idiomas, "Culture", "Name", targetCulture);

            return View(viewModel);
        }


        [HttpPost]
        [Route("{culture}/Admin/AddTranslation/{postId}")]
        public async Task<IActionResult> AddTranslation(AddTranslationViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            
            var sanitizer = new HtmlSanitizer();

            // Opcional: Se você quiser permitir tags específicas (como vídeos do YouTube)
            // sanitizer.AllowedAttributes.Add("frameborder");
            // sanitizer.AllowedAttributes.Add("allowfullscreen");

            // Limpa o conteúdo vindo do ViewModel
            string cleanHtml = sanitizer.Sanitize(model.Content);

            // 1. Verificação de existência (Correto)
            bool alreadyExists = await _context.BlogPostTranslations
                .AnyAsync(t => t.BlogPostId == model.BlogPostId && t.Culture == model.SelectedCulture);

            if (alreadyExists)
            {
                ModelState.AddModelError("SelectedCulture", "Este idioma já existe para este post.");
                return View(model);
            }

            // 2. Processa a imagem usando o método centralizado (WebP garantido)
            string? dbImagePath = await ProcessAndSaveWebP(model.ImageFile);

            // 3. Gerar Slug ÚNICO e LIMPO (Usando o método que já criamos para o Create)
            // Isso garante que "Olá Mundo" vire "ola-mundo" e não "olá-mundo"
            string uniqueSlug = await GetUniqueSlugAsync(model.Title, model.SelectedCulture);

            var translation = new BlogPostTranslation
            {
                BlogPostId = model.BlogPostId,
                Culture = model.SelectedCulture,
                Title = model.Title,
                //Content = model.Content,
                Content = cleanHtml, // Salva o HTML seguro
                Slug = uniqueSlug,
                ImageUrl = dbImagePath ?? "/uploads/blog/default-post.jpg",
                IsPublished = model.IsPublished,
                MetaKeywords = model.MetaKeywords
            };

            _context.BlogPostTranslations.Add(translation);
            await _context.SaveChangesAsync();

            return RedirectToAction("Dashboard", "Admin", new { culture = model.SelectedCulture });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTranslation(int id) // 'id' aqui é o ID da Tradução
        {
            var translation = await _context.BlogPostTranslations.FindAsync(id);

            if (translation == null) return NotFound();

            // 1. Descobrimos o ID do Post pai
            var blogPostId = translation.BlogPostId;

            // 2. Contamos quantas traduções esse Post específico tem
            var totalTraducoes = await _context.BlogPostTranslations
                .CountAsync(t => t.BlogPostId == blogPostId);

            // 3. Trava de segurança: não apagar a última
            if (totalTraducoes <= 1)
            {
                // Se for a última, avisamos o usuário
                TempData["Error"] = "Você não pode apagar a última tradução. Apague o Post completo se desejar.";
                return RedirectToAction("Dashboard", "Admin");
            }

            // 4. Se passou na trava, deleta a imagem física e o registro
            // Apaga o arquivo físico primeiro
            if (!string.IsNullOrEmpty(translation.ImageUrl))
            {
                var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, translation.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);
            }

            _context.BlogPostTranslations.Remove(translation);
            await _context.SaveChangesAsync();

            return RedirectToAction("Dashboard", "Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePost(int id)
        {
            var post = await _context.BlogPosts
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();

            // REALISMO: Antes de apagar do banco, apague as imagens físicas do servidor
            foreach (var translation in post.Translations)
            {
                if (!string.IsNullOrEmpty(translation.ImageUrl))
                {
                    string path = Path.Combine(_webHostEnvironment.WebRootPath, translation.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
            }

            _context.BlogPosts.Remove(post);
            await _context.SaveChangesAsync();

            return RedirectToAction("Dashboard", "Admin");
        }

        public async Task<IActionResult> Admin(string culture = "pt-BR")
        {
            var posts = await _context.BlogPosts
                .Include(p => p.Translations)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Se a lista estiver vazia, a View apenas não mostrará linhas na tabela.
            return View(posts);
        }

        private bool TranslationExists(int id)
        {
            return _context.BlogPostTranslations.Any(e => e.Id == id);
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
                    // Gera a URL para cada tradução do post
                    var url = $"{baseUrl}/{trans.Culture}/blog/{trans.Slug}";
                    sb.AppendLine("  <url>");
                    sb.AppendLine($"    <loc>{url}</loc>");
                    sb.AppendLine($"    <lastmod>{post.UpdatedAt.ToString("yyyy-MM-dd")}</lastmod>");
                    sb.AppendLine("    <changefreq>monthly</changefreq>");
                    sb.AppendLine("    <priority>0.8</priority>");
                    sb.AppendLine("  </url>");
                }
            }

            sb.AppendLine("</urlset>");

            return Content(sb.ToString(), "application/xml");
        }

        private async Task<string?> ProcessAndSaveWebP(IFormFile imageFile)
{
    if (imageFile == null || imageFile.Length == 0) return null;

    try
    {
        string fileName = Guid.NewGuid().ToString() + ".webp";
        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "blog");

        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
        string physicalPath = Path.Combine(uploadsFolder, fileName);

        using (var inputStream = imageFile.OpenReadStream())
        using (var managedStream = new SKManagedStream(inputStream))
        using (var bitmap = SKBitmap.Decode(managedStream))
        {
            if (bitmap == null) return null;

            // 1. Decidir se redimensiona ou usa o original
            SKBitmap finalBitmap = bitmap;
            bool wasResized = false;

            if (bitmap.Width > 1200)
            {
                int newWidth = 1200;
                int newHeight = (int)(bitmap.Height * (1200.0 / bitmap.Width));
                finalBitmap = bitmap.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);
                wasResized = true;
            }

            try 
            {
                // 2. Encode e Salvamento (Fora do IF, para pegar todas as imagens)
                using (var image = SKImage.FromBitmap(finalBitmap))
                using (var data = image.Encode(SKEncodedImageFormat.Webp, 80))
                {
                    using (var outputStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        data.SaveTo(outputStream);
                        await outputStream.FlushAsync(); 
                    }
                }
            }
            finally
            {
                // Limpa a memória do bitmap redimensionado se ele foi criado
                if (wasResized) finalBitmap.Dispose();
            }
        }

        return "/uploads/blog/" + fileName;
    }
    catch (Exception ex)
    {
        Console.WriteLine("ERRO NO UPLOAD: " + ex.Message);
        return null;
    }
}

        [HttpPost]
[Route("{culture}/BlogPosts/UploadEditorImage")]
public async Task<IActionResult> UploadEditorImage(IFormFile image)
{
    if (image == null || image.Length == 0) return BadRequest("Imagem inválida.");

    // Reaproveita seu método que já converte para WebP e salva na pasta /uploads/blog/
    string? path = await ProcessAndSaveWebP(image);

    if (path != null)
    {
        // O Quill espera o link da imagem para substituir o Base64
        return Ok(new { url = path });
    }

    return BadRequest("Falha ao processar imagem.");
}
    }

    


    }