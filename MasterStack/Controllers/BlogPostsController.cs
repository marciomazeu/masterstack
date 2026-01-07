using MasterStack.Data; // Ajuste para o seu namespace de dados
using MasterStack.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MasterStack.Controllers
{
    
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
        [HttpGet]
        public async Task<IActionResult> Details(int? id, string culture)
        {
            if (id == null) return NotFound();

            var blogPost = await _context.BlogPosts
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (blogPost == null) return NotFound();

            // Normalizamos a cultura pedida (ex: "en-US" vira "en-us")
            string requested = (culture ?? "pt-BR").Trim().ToLower();

            // 1. Tenta achar a tradução exata (ignorando Case Sensitive)
            var translation = blogPost.Translations
                .FirstOrDefault(t => t.Culture.Trim().ToLower() == requested);

            if (translation == null)
            {
                // 2. FALLBACK: Se não achou a pedida, pega a pt-BR ou a primeira que existir
                translation = blogPost.Translations.FirstOrDefault(t => t.Culture.ToLower() == "pt-br")
                              ?? blogPost.Translations.FirstOrDefault();

                ViewBag.TranslationWarning = true;
                ViewBag.RequestedCulture = culture ?? "pt-BR"; // O que o usuário viu na URL
                ViewBag.ActualCulture = translation?.Culture; // O que o banco entregou
            }
            else
            {
                ViewBag.TranslationWarning = false;
            }

            ViewBag.CurrentTranslation = translation;
            return View(blogPost);
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
        [HttpPost] // Certifique-se que este atributo existe
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlogPostCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 1. Criar o registro Pai (BlogPost)
                    var post = new BlogPost { CreatedAt = DateTime.Now };
                    _context.BlogPosts.Add(post);
                    await _context.SaveChangesAsync();

                    // 2. Processar o Upload da Imagem
                    string? fileName = null;
                    if (model.ImageFile != null && model.ImageFile.Length > 0)
                    {
                        // Gera um nome único para o arquivo
                        fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageFile.FileName);
                        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/blog");

                        // Garante que a pasta física existe no servidor
                        if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                        var filePath = Path.Combine(uploadPath, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.ImageFile.CopyToAsync(stream);
                        }
                    }

                    // 3. Criar a Tradução (BlogPostTranslation) vinculada ao post
                    var translation = new BlogPostTranslation
                    {
                        BlogPostId = post.Id,
                        Culture = System.Globalization.CultureInfo.CurrentCulture.Name,
                        Title = model.Title,
                        Content = model.Content,
                        Slug = model.Title?.ToLower().Trim().Replace(" ", "-") ?? "sem-slug",

                        // CORREÇÃO AQUI:
                        // ImageUrl é a string que vai para o banco.
                        // fileName é a string que você gerou lá em cima.
                        ImageUrl = fileName
                    };

                    _context.BlogPostTranslations.Add(translation);
                    await _context.SaveChangesAsync();

                    // 4. Confirmar no banco de dados
                    await transaction.CommitAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Erro ao salvar no banco: " + ex.Message);
                }
            }

            // Se houver erro de validação, volta para a tela de criação
            return View(model);
        }

        private string GenerateSlug(string title)
        {
            var slug = title.ToLower().Trim();
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-").Trim('-');
            return slug;
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
        //public async Task<IActionResult> Edit(int id, [Bind("Id,BlogPostId,Culture,Title,Content,ImageUrl,Slug")] BlogPostTranslation translation, IFormFile? novaImagem)
        public async Task<IActionResult> Edit(int id, [Bind("Id,BlogPostId,Culture,Title,Content,ImageUrl,Slug")] BlogPostTranslation translation, IFormFile? novaImagem)
        {
           
            if (id != translation.Id) return NotFound();
            // Forçamos a criação do Slug para o ModelState não reclamar
            if (string.IsNullOrEmpty(translation.Slug) && !string.IsNullOrEmpty(translation.Title))
            {
                translation.Slug = translation.Title.ToLower().Replace(" ", "-"); // Lógica simples de slug
            }

            // Remova o erro do Slug do ModelState manualmente se necessário
            ModelState.Remove("Slug");

            if (ModelState.IsValid)
            {
                try
                {
                    // 1. Verificar se uma nova imagem foi enviada
                    if (novaImagem != null && novaImagem.Length > 0)
                    {
                        // No Controller Edit POST:
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "blog");

                        // Garanta que a subpasta existe
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                        // Criar nome único para o novo arquivo
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + novaImagem.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        // Salvar o novo arquivo no disco
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await novaImagem.CopyToAsync(fileStream);
                        }

                        // 2. DELETAR a imagem antiga se ela existir (Limpeza)
                        if (!string.IsNullOrEmpty(translation.ImageUrl))
                        {
                            string oldPath = Path.Combine(uploadsFolder, translation.ImageUrl);
                            if (System.IO.File.Exists(oldPath))
                            {
                                System.IO.File.Delete(oldPath);
                            }
                        }

                        // 3. Atualizar o caminho da imagem no objeto
                        translation.ImageUrl = uniqueFileName;
                        _context.Entry(translation).Property(x => x.ImageUrl).IsModified = true;
                    }

                    // Atualizar o Slug caso o título tenha mudado
                    translation.Slug = GenerateSlug(translation.Title);

                    _context.Update(translation);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TranslationExists(translation.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Admin));
            }
            return View(translation);
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
        public async Task<IActionResult> AddTranslation(int id, string targetCulture)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null) return NotFound();

            // MUDANÇA AQUI: Use BlogPostTranslationViewModel em vez de Create
            var viewModel = new BlogPostTranslation
            {
                BlogPostId = id,
                Culture = targetCulture
            };

            var idiomas = await _context.Languages.Where(l => l.IsActive).ToListAsync();
            ViewBag.Languages = new SelectList(idiomas, "Culture", "Name", targetCulture);

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AddTranslation(BlogPostTranslation model)
        {
            // 1. Onde está o arquivo? Precisamos recebê-lo do model (ViewModel)
            string? fileName = null;

            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                // Gerar o nome do arquivo
                fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageFile.FileName);
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/blog", fileName);

                // Salvar fisicamente na pasta
                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }
            }

            // 2. Agora sim, criamos o objeto com dados REAIS e o nome da imagem
            var translation = new BlogPostTranslation
            {
                BlogPostId = model.BlogPostId,
                Culture = model.Culture, // Use a cultura que vem do formulário, não trave em "pt-BR"
                Title = model.Title,
                Content = model.Content,
                Slug = model.Title?.ToLower().Trim().Replace(" ", "-") ?? "slug-" + DateTime.Now.Ticks,
                ImageUrl = fileName // Agora a variável existe e tem o nome do arquivo!
            };

            _context.BlogPostTranslations.Add(translation);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Tradução e imagem salvas com sucesso!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTranslation(int id)
        {
            // 1. Busca os dados com rastreamento completo
            var translation = await _context.BlogPostTranslations
                .Include(t => t.BlogPost)
                .ThenInclude(p => p.Translations)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (translation == null)
            {
                TempData["Error"] = "Tradução não encontrada.";
                return RedirectToAction(nameof(Index));
            }

            var blogPost = translation.BlogPost;
            var cultureName = translation.Culture;
            string imagePath = null;

            // Guardamos o caminho mas não apagamos ainda!
            if (!string.IsNullOrEmpty(translation.ImageUrl))
            {
                imagePath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "blog", translation.ImageUrl);
            }

            // Iniciamos uma Transação de Banco de Dados
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 2. Remove a tradução do banco primeiro
                    _context.BlogPostTranslations.Remove(translation);

                    // 3. Se for a última tradução, remove o pai
                    if (blogPost.Translations.Count <= 1)
                    {
                        _context.BlogPosts.Remove(blogPost);
                    }

                    await _context.SaveChangesAsync();

                    // 4. SÓ AGORA tentamos apagar o arquivo físico
                    if (imagePath != null && System.IO.File.Exists(imagePath))
                    {
                        try
                        {
                            System.IO.File.Delete(imagePath);
                        }
                        catch (IOException ex)
                        {
                            // Erro realista: O arquivo está sendo usado por outro processo
                            // Logamos o erro mas permitimos que a transação do banco continue? 
                            // Melhor avisar o usuário que o registro sumiu mas o arquivo ficou "preso".
                            TempData["Error"] = "O registro foi apagado, mas a imagem está em uso pelo sistema e não pôde ser removida do disco.";
                        }
                    }

                    // Se chegou aqui sem exceção fatal, confirma tudo no banco
                    await transaction.CommitAsync();

                    if (TempData["Error"] == null)
                        TempData["Success"] = "Exclusão realizada com sucesso.";

                    return RedirectToAction(nameof(Index), new { culture = cultureName });
                }
                catch (Exception ex)
                {
                    // Se houver qualquer erro no banco, desfaz TUDO (Rollback)
                    await transaction.RollbackAsync();
                    TempData["Error"] = "Erro crítico de banco de dados. Nada foi apagado. Detalhes: " + ex.Message;
                    return RedirectToAction(nameof(Index));
                }
            }
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
    }

       
}