using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MasterStack.Data; // Ajuste para o seu namespace de dados
using MasterStack.Models;
using System.Globalization;

namespace MasterStack.Controllers
{
    [Route("{culture}/[controller]")] // Mantém a cultura e o nome do controller (BlogPosts)
    public class BlogPostsController : Controller
    {
        private readonly List<string> _supportedCultures = new List<string> { "pt-BR", "en-US", "fr-FR" };
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public BlogPostsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: /pt-BR/BlogPosts
        [HttpGet]
        public async Task<IActionResult> Index(string culture)
        {
            // 1. Obtemos a cultura atual (ex: "pt-BR", "en-US")
            var currentCulture = System.Globalization.CultureInfo.CurrentCulture.Name;

            // Carregamos o Post e TODAS as suas traduções
            var languages = await _context.Languages.Where(l => l.IsActive).ToListAsync();
            ViewBag.Languages = languages; // Passa a lista para a View

            // 3. (Opcional) Se quiser que a Index SÓ mostre posts que JÁ TÊM tradução no idioma atual:
            var posts = await _context.BlogPosts
                .Include(p => p.Translations)
                .ToListAsync();
            posts = posts.Where(p => p.Translations.Any(t => t.Culture == currentCulture)).ToList();

            // 4. Passamos a lista de idiomas para os botões continuarem a funcionar
            ViewBag.Languages = await _context.Languages.Where(l => l.IsActive).ToListAsync();

            return View(posts);
        }

        // GET: /pt-BR/BlogPosts/post/5
        [HttpGet("post/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            // 1. Buscamos o Post incluindo as suas traduções
            var blogPost = await _context.BlogPosts
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (blogPost == null) return NotFound();

            // 2. Identificamos a cultura atual do site
            var currentCulture = System.Globalization.CultureInfo.CurrentCulture.Name;

            // 3. Tentamos pegar a tradução específica para o idioma atual
            var translation = blogPost.Translations
                .FirstOrDefault(t => t.Culture == currentCulture)
                // Se não houver tradução no idioma atual, pega a primeira disponível (Fallback)
                ?? blogPost.Translations.FirstOrDefault();

            if (translation == null)
            {
                // Caso extremo: o post existe mas não tem nenhuma tradução vinculada
                return NotFound("Conteúdo não disponível para este post.");
            }

            // Passamos a tradução via ViewBag ou usamos uma ViewModel específica
            ViewBag.CurrentTranslation = translation;

            return View(blogPost);
        }

        // GET: /pt-BR/BlogPosts/Create
        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: BlogPosts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string culture, BlogPostCreateViewModel model)
        {
            // Remova validações de propriedades que serão preenchidas manualmente
            ModelState.Remove("Translations");

            if (ModelState.IsValid)
            {
                string uniqueFileName = null;

                // Lógica para salvar o arquivo na pasta wwwroot/uploads
                if (model.ImageFile != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");

                    // Cria a pasta caso não exista
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ImageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ImageFile.CopyToAsync(fileStream);
                    }
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var post = new BlogPost
                    {
                        ImageUrl = uniqueFileName != null ? "/uploads/" + uniqueFileName : null,
                        CreatedAt = DateTime.Now
                    };
                    
                    _context.BlogPosts.Add(post);
                    await _context.SaveChangesAsync();

                    // 3. SALVAR AUTOMATICAMENTE A PRIMEIRA TRADUÇÃO
                    // Pegamos a cultura atual do sistema (ex: fr-FR, pt-BR)
                    var currentCulture = System.Globalization.CultureInfo.CurrentCulture.Name;

                    var translation = new BlogPostTranslation
                    {
                        BlogPostId = post.Id,
                        Culture = culture,
                        Title = model.Title,
                        Content = model.Content,
                        Slug = GenerateSlug(model.Title)
                    };

                    _context.BlogPostTranslations.Add(translation);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    await transaction.RollbackAsync();
                }
            }
            // Se chegou aqui, algo falhou. Vamos descobrir o que:
            var errors = ModelState.Values.SelectMany(v => v.Errors);
            return View(model);
        }

        private string GenerateSlug(string title)
        {
            var slug = title.ToLower().Trim();
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-").Trim('-');
            return slug;
        }

        // GET: BlogPosts/Edit/5
        [Route("Edit/{id}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var blogPost = await _context.BlogPosts.FindAsync(id);
            if (blogPost == null)
            {
                return NotFound();
            }
            return View(blogPost);
        }

        // POST: BlogPosts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id, string culture)
        {
            // Busca a tradução específica para editar
            var translation = await _context.BlogPostTranslations
                .Include(t => t.BlogPost)
                .FirstOrDefaultAsync(t => t.BlogPostId == id && t.Culture == culture);

            if (translation == null) return NotFound();

            return View(translation);
        }

        // GET: BlogPosts/Delete/5
        [HttpGet("Delete/{id}")]
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
        [Route("Delete/{id}")] // Certifique-se que esta rota existe aqui
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var blogPost = await _context.BlogPosts.FindAsync(id);
            if (blogPost != null)
            {
                // Opcional: Se você estiver salvando arquivos locais, delete o arquivo da pasta uploads aqui
                // 1. Verificar se existe um caminho de imagem salvo
                if (!string.IsNullOrEmpty(blogPost.ImageUrl))
                {
                    // 2. Construir o caminho físico completo no servidor
                    // ImageUrl geralmente é "/uploads/nome.jpg", precisamos remover a primeira "/"
                    var caminhoRelativo = blogPost.ImageUrl.TrimStart('/');
                    var caminhoCompleto = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", caminhoRelativo);

                    try
                    {
                        // 3. Verificar se o arquivo realmente existe na pasta e deletar
                        if (System.IO.File.Exists(caminhoCompleto))
                        {
                            System.IO.File.Delete(caminhoCompleto);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Opcional: Logar erro se o arquivo estiver sendo usado por outro processo
                        // Mas não paramos a execução para garantir que o post saia do banco
                    }
                }

            }

            // 4. Remover o registro do banco de dados
            _context.BlogPosts.Remove(blogPost);
            await _context.SaveChangesAsync();
            // Importante: Redirecionar mantendo a cultura
            return RedirectToAction(nameof(Index), new { culture = RouteData.Values["culture"] });
        }

        private bool BlogPostExists(int id)
        {
            return _context.BlogPosts.Any(e => e.Id == id);
        }

        // 1. O GET (Para abrir o formulário)
        [HttpGet("AddTranslation/{id}")]
        public async Task<IActionResult> AddTranslation(int id, string targetCulture)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null) return NotFound();

            // EM VEZ DE: var model = new BlogPostTranslation...
            // USE A VIEWMODEL:
            var viewModel = new BlogPostTranslationViewModel
            {
                BlogPostId = id,
                Culture = targetCulture
                // Title e Content começam vazios para o usuário preencher
            };

            return View(viewModel); // Agora o tipo coincide com o @model da View
        }

        [HttpPost("AddTranslation/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTranslation(int id, BlogPostTranslationViewModel model)
        {
            // Verifica se já existe esta tradução antes de tentar salvar
            var existe = await _context.BlogPostTranslations
                .AnyAsync(t => t.BlogPostId == model.BlogPostId && t.Culture == model.Culture);

            if (existe)
            {
                ModelState.AddModelError("", "Já existe uma tradução para este idioma neste post.");
                return View(model);
            }

            // 1. Segurança: Verifica se o ID da URL coincide com o do formulário
            if (id != model.BlogPostId)
            {
                return BadRequest();
            }

            // VERIFICAÇÃO DE SEGURANÇA: Já existe tradução para este idioma neste post?
            bool alreadyExists = await _context.BlogPostTranslations
                .AnyAsync(t => t.BlogPostId == id && t.Culture == model.Culture);

            if (alreadyExists)
            {
                ModelState.AddModelError("", $"Já existe uma tradução em {model.Culture} para este post. Edite a tradução existente em vez de criar uma nova.");
            }

            if (ModelState.IsValid)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 2. Processamento da Imagem (Opcional na tradução)
                    if (model.ImageFile != null)
                    {
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ImageFile.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.ImageFile.CopyToAsync(fileStream);
                        }

                        // Atualiza a imagem no BlogPost (Pai)
                        var parentPost = await _context.BlogPosts.FindAsync(model.BlogPostId);
                        if (parentPost != null)
                        {
                            parentPost.ImageUrl = "/uploads/" + uniqueFileName;
                            _context.Update(parentPost);
                        }
                    }

                    // 3. Criação da Entidade de Tradução (Banco de Dados)
                    var translation = new BlogPostTranslation
                    {
                        BlogPostId = model.BlogPostId,
                        Culture = model.Culture,
                        Title = model.Title,
                        Content = model.Content,
                        // Aqui geramos o Slug a partir do Title da ViewModel
                        Slug = GenerateSlug(model.Title)
                    };

                    _context.BlogPostTranslations.Add(translation);

                    // 4. Salva todas as alterações (Tradução nova + Imagem no Pai)
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return RedirectToAction(nameof(Index), new { culture = model.Culture });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Erro ao salvar tradução: " + ex.Message);
                }
            }

            // 5. Se algo falhou, volta para a View com a ViewModel original (evita erro de tipos)
            return View(model);
        }
    }

    public class BlogPostCreateViewModel
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public IFormFile? ImageFile { get; set; }
    }

    public class BlogPostTranslationViewModel
    {
        public int BlogPostId { get; set; }
        public string Culture { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public IFormFile? ImageFile { get; set; }
    }
}