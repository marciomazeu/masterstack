using MasterStack.Data; // Ajuste para o seu namespace de dados
using MasterStack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
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
        [Route("{culture}/blog/{slug}")]
        public async Task<IActionResult> Details(string culture, string slug)
        {
            // 1. Busca a tradução exata pelo slug fornecido
            var currentTranslation = await _context.BlogPostTranslations
                .Include(t => t.BlogPost)
                .ThenInclude(p => p.Translations)
                .FirstOrDefaultAsync(t => t.Slug == slug);

            if (currentTranslation == null) return NotFound();

            bool isFallback = false;

            // 2. REALISMO: O slug existe, mas é da cultura certa?
            // Se o slug for "receita-de-bolo" (PT) mas a URL pedir "en-US"
            if (currentTranslation.Culture.ToLower() != culture.ToLower())
            {
                // Tenta ver se o "Pai" deste post tem uma tradução para a cultura pedida (en-US)
                var targetTranslation = currentTranslation.BlogPost.Translations
                    .FirstOrDefault(t => t.Culture.ToLower() == culture.ToLower());

                if (targetTranslation != null)
                {
                    // Redireciona para o slug correto daquela língua! (SEO Friendly)
                    return RedirectToAction(nameof(Details), new
                    {
                        culture = targetTranslation.Culture,
                        slug = targetTranslation.Slug
                    });
                }

                // Se o pai não tem tradução para a língua pedida, aí sim usamos o Fallback
                isFallback = true;
            }

            ViewBag.CurrentTranslation = currentTranslation;
            ViewBag.TranslationWarning = isFallback;

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
        public async Task<IActionResult> Create(BlogPostCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 1. Gerar o Slug UNICO
                    string uniqueSlug = await GetUniqueSlugAsync(model.Title);

                    // 2. Criar o registro Pai
                    var post = new BlogPost { CreatedAt = DateTime.Now };
                    _context.BlogPosts.Add(post);
                    await _context.SaveChangesAsync();

                    // 3. Processar o Upload com Conversão para WebP
                    string? fileName = null;
                    if (model.ImageFile != null && model.ImageFile.Length > 0)
                    {
                        // Forçamos a extensão .webp para o banco e para o arquivo
                        fileName = Guid.NewGuid().ToString() + ".webp";
                        var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "blog");

                        if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                        var filePath = Path.Combine(uploadPath, fileName);

                        // --- INÍCIO DA LÓGICA SKIASHARP ---
                        using (var inputMemoryStream = new MemoryStream())
                        {
                            await model.ImageFile.CopyToAsync(inputMemoryStream);
                            inputMemoryStream.Position = 0;

                            using (var managedStream = new SKManagedStream(inputMemoryStream))
                            using (var bitmap = SKBitmap.Decode(managedStream))
                            {
                                if (bitmap == null) throw new Exception("Formato de imagem inválido.");

                                using (var image = SKImage.FromBitmap(bitmap))
                                using (var data = image.Encode(SKEncodedImageFormat.Webp, 75)) // Qualidade 75 (Lighthouse ama isso)
                                using (var saveStream = System.IO.File.OpenWrite(filePath))
                                {
                                    data.SaveTo(saveStream);
                                }
                            }
                        }
                        // --- FIM DA LÓGICA SKIASHARP ---
                    }

                    // 4. Criar a Tradução vinculada
                    var translation = new BlogPostTranslation
                    {
                        BlogPostId = post.Id,
                        Culture = System.Globalization.CultureInfo.CurrentCulture.Name,
                        Title = model.Title,
                        Content = model.Content,
                        Slug = uniqueSlug,
                        ImageUrl = fileName // Nome salvo já com .webp
                    };

                    _context.BlogPostTranslations.Add(translation);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Post criado com sucesso!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    // Realismo: Logar o erro internamente aqui (ex: _logger.LogError(ex, "Erro ao criar post"))
                    ModelState.AddModelError("", "Erro ao processar imagem ou salvar dados. Verifique o formato do arquivo.");
                }
            }

            return View(model);
        }

        private string GenerateSlug(string phrase)
        {
            // Exemplo de lógica de verificação (Simplificada)
            int count = 1;
            string originalSlug = phrase;
            while (_context.BlogPostTranslations.Any(t => t.Slug == phrase))
            {
                phrase = $"{originalSlug}-{count}";
                count++;
            }
            if (string.IsNullOrEmpty(phrase)) return "";

            // 1. Converte para minúsculo
            string str = phrase.ToLower();

            // 2. Remove acentos (Normalização)
            str = System.Text.Encoding.ASCII.GetString(System.Text.Encoding.GetEncoding("Cyrillic").GetBytes(str));

            // 3. Remove caracteres inválidos (Regex)
            // Substitui qualquer coisa que não seja letra, número ou espaço por nada
            str = System.Text.RegularExpressions.Regex.Replace(str, @"[^a-z0-9\s-]", "");

            // 4. Converte múltiplos espaços em um único espaço
            str = System.Text.RegularExpressions.Regex.Replace(str, @"\s+", " ").Trim();

            // 5. Corta o tamanho máximo (opcional, ex: 60 caracteres)
            str = str.Substring(0, str.Length <= 60 ? str.Length : 60).Trim();

            // 6. Troca espaços por hífens
            str = System.Text.RegularExpressions.Regex.Replace(str, @"\s", "-");

            return str;
        }

        private async Task<string> GetUniqueSlugAsync(string title, int? currentId = null)
        {
            string slug = GenerateSlug(title); // Sua função que remove acentos e símbolos
            string uniqueSlug = slug;
            int count = 1;

            // O loop continua enquanto houver alguém usando esse slug (exceto o próprio post que estamos editando)
            while (await _context.BlogPostTranslations
                .AnyAsync(t => t.Slug == uniqueSlug && (!currentId.HasValue || t.Id != currentId.Value)))
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,BlogPostId,Culture,Title,Content,ImageUrl,Slug")] BlogPostTranslation translation, IFormFile? novaImagem)
        {
            if (id != translation.Id) return NotFound();

            // 1. Gerar o Slug de forma definitiva
            //translation.Slug = GenerateSlug(translation.Title);
            // Passamos o ID para ignorar o registro atual na verificação
            translation.Slug = await GetUniqueSlugAsync(translation.Title, translation.Id);
            ModelState.Remove("Slug");

            if (ModelState.IsValid)
            {
                try
                {
                    if (novaImagem != null && novaImagem.Length > 0)
                    {
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "blog");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                        // --- AQUI ESTÁ A CORREÇÃO CRÍTICA ---
                        // Buscamos o nome real da imagem antiga que está no banco AGORA (Sem rastreamento)
                        var imagemAntigaNoBanco = await _context.BlogPostTranslations
                            .Where(t => t.Id == id)
                            .Select(t => t.ImageUrl)
                            .AsNoTracking()
                            .FirstOrDefaultAsync();

                        // Criar a nova
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + novaImagem.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await novaImagem.CopyToAsync(fileStream);
                        }

                        // Deletar a antiga SOMENTE se o nome for diferente e ela existir
                        if (!string.IsNullOrEmpty(imagemAntigaNoBanco))
                        {
                            string oldPath = Path.Combine(uploadsFolder, imagemAntigaNoBanco);
                            if (System.IO.File.Exists(oldPath))
                            {
                                System.IO.File.Delete(oldPath);
                            }
                        }

                        translation.ImageUrl = uniqueFileName;
                    }

                    _context.Update(translation);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Tradução atualizada com sucesso!";
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