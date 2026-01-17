using MasterStack.Data; // Ajuste para o seu namespace de dados
using MasterStack.Models;
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
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

        public BlogPostsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, IStringLocalizer<BlogPostsController> localizer)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _localizer = localizer; // Atribua ao campo privado
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
                .Include(p => p.Translations)
                .Where(p => p.Translations.Any(t => t.Culture == currentCulture))
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
        public async Task<IActionResult> Create(BlogPostCreateViewModel model, string? culture)
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
                        // Apenas o nome do arquivo
                        fileName = Guid.NewGuid().ToString() + ".webp";

                        // CAMINHO DA PASTA (Sem o nome do arquivo aqui!)
                        var uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "blog");

                        // Cria a pasta se não existir
                        if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                        // CAMINHO DO ARQUIVO FINAL
                        var filePath = Path.Combine(uploadFolder, fileName);

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
                                using (var data = image.Encode(SKEncodedImageFormat.Webp, 75))
                                using (var saveStream = System.IO.File.OpenWrite(filePath))
                                {
                                    data.SaveTo(saveStream); // Grava o arquivo WebP real
                                }
                            }
                        }
                    }

                    // 4. Criar a Tradução vinculada
                    //culture ??= System.Globalization.CultureInfo.CurrentCulture.Name;
                    culture ??= Request.RouteValues["culture"]?.ToString() ?? "pt-BR"; // Prioriza a cultura da URL
                    var translation = new BlogPostTranslation
                    {
                        BlogPostId = post.Id,
                        Culture = culture,
                        Title = model.Title,
                        Content = model.Content,
                        Slug = uniqueSlug,
                        ImageUrl = "/uploads/blog/" + fileName // Nome salvo já com .webp
                    };

                    _context.BlogPostTranslations.Add(translation);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Post criado com sucesso!";
                    //return RedirectToAction(nameof(Index));
                    // Pegamos a cultura da rota atual para garantir o redirecionamento correto
                    //var currentCulture = System.Globalization.CultureInfo.CurrentCulture.Name;
                    return RedirectToAction("Dashboard", "Admin", new { culture = culture });
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

        [HttpGet]
        public async Task<IActionResult> EditTranslation(int id)
        {
            var translation = await _context.BlogPostTranslations
                .FirstOrDefaultAsync(t => t.Id == id);

            if (translation == null) return NotFound();

            var model = new EditTranslationViewModel
            {
                TranslationId = translation.Id,
                Culture = translation.Culture,
                Title = translation.Title,
                Content = translation.Content,
                CurrentImageUrl = translation.ImageUrl // Agora pegamos da tradução!
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTranslation(EditTranslationViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var translation = await _context.BlogPostTranslations
                .FirstOrDefaultAsync(t => t.Id == model.TranslationId);

            if (translation == null) return NotFound();

            translation.Title = model.Title;
            translation.Content = model.Content;

            // Decisão realista: Só mude o slug se o título realmente mudou
            // translation.Slug = GenerateSlug(model.Title); 

            if (model.NewImage != null && model.NewImage.Length > 0)
            {
                // 1. Guardamos o caminho da imagem antiga antes de mudar o banco
                string oldImagePath = translation.ImageUrl;

                // 2. Processo de salvar a NOVA imagem (seu código atual)
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.NewImage.FileName);
                string newPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads/blog", fileName);

                // Salva o novo
                using (var stream = new FileStream(newPath, FileMode.Create))
                {
                    await model.NewImage.CopyToAsync(stream);
                }
                // 3. Atualiza o banco com o novo caminho
                translation.ImageUrl = "/uploads/blog/" + fileName;

                // 4. LÓGICA DE LIMPEZA: Apaga a antiga do disco
                if (!string.IsNullOrEmpty(oldImagePath) && !oldImagePath.Contains("default-post.jpg"))
                {
                    var fullOldPath = Path.Combine(_webHostEnvironment.WebRootPath, oldImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(fullOldPath))
                    {
                        System.IO.File.Delete(fullOldPath);
                    }
                }

                // SALVE O CAMINHO COMPLETO
                //translation.ImageUrl = "/uploads/blog/" + uniqueFileName;
            }

            try
            {
                // O SaveChanges já entende as mudanças no objeto 'translation'
                await _context.SaveChangesAsync();
                return RedirectToAction("Dashboard", "Admin", new { culture = model.Culture });
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("", "Este registro foi alterado por outro usuário. Recarregue a página.");
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

        //[HttpPost]
        //[Route("{culture}/Admin/AddTranslation/{postId}")]
        //public async Task<IActionResult> AddTranslation(AddTranslationViewModel model)
        //{
        //    // Verificação de Realismo: O post já tem esse idioma?
        //    bool alreadyExists = await _context.BlogPostTranslations
        //        .AnyAsync(t => t.BlogPostId == model.BlogPostId && t.Culture == model.SelectedCulture);

        //    if (alreadyExists)
        //    {
        //        ModelState.AddModelError("SelectedCulture", "Este post já possui uma tradução para o idioma selecionado.");
        //        return View(model);
        //    }
        //    // 1. Onde está o arquivo? Precisamos recebê-lo do model (ViewModel)
        //    string? fileName = null;
        //    string? dbPath = null; // Criamos uma variável para o caminho do banco

        //    if (model.ImageFile != null && model.ImageFile.Length > 0)
        //    {
        //        // Gerar o nome do arquivo
        //        fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageFile.FileName);
        //        var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/blog", fileName);

        //        // Salvar fisicamente na pasta
        //        using (var stream = new FileStream(path, FileMode.Create))
        //        {
        //            await model.ImageFile.CopyToAsync(stream);
        //        }
        //        dbPath = "/uploads/blog/" + fileName;
        //    }

        //    // 2. Agora sim, criamos o objeto com dados REAIS e o nome da imagem
        //    var translation = new BlogPostTranslation
        //    {
        //        BlogPostId = model.BlogPostId,
        //        Culture = model.SelectedCulture, // Use a cultura que vem do formulário, não trave em "pt-BR"
        //        Title = model.Title,
        //        Content = model.Content,
        //        Slug = model.Title?.ToLower().Trim().Replace(" ", "-") ?? "slug-" + DateTime.Now.Ticks,
        //        ImageUrl = dbPath // Agora a variável existe e tem o nome do arquivo!
        //    };

        //    _context.BlogPostTranslations.Add(translation);
        //    await _context.SaveChangesAsync();

        //    TempData["SuccessMessage"] = "Tradução e imagem salvas com sucesso!";
        //    return RedirectToAction("Dashboard", "Admin");
        //}
        [HttpPost]
        [Route("{culture}/Admin/AddTranslation/{postId}")]
        public async Task<IActionResult> AddTranslation(AddTranslationViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Verificação de existência
            bool alreadyExists = await _context.BlogPostTranslations
                .AnyAsync(t => t.BlogPostId == model.BlogPostId && t.Culture == model.SelectedCulture);

            if (alreadyExists)
            {
                ModelState.AddModelError("SelectedCulture", "Este idioma já existe para este post.");
                return View(model);
            }

            // Processa a imagem usando o novo método
            string? dbImagePath = null;
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                dbImagePath = await ProcessAndSaveWebP(model.ImageFile);
            }

            var translation = new BlogPostTranslation
            {
                BlogPostId = model.BlogPostId,
                Culture = model.SelectedCulture,
                Title = model.Title,
                Content = model.Content,
                Slug = model.Title?.ToLower().Trim().Replace(" ", "-") ?? "slug-" + DateTime.Now.Ticks,
                ImageUrl = dbImagePath // Salva o caminho completo: /uploads/blog/arquivo.webp
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
            try
            {
                // 1. Define o nome e caminhos
                string fileName = Guid.NewGuid().ToString() + ".webp";
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "blog");

                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string physicalPath = Path.Combine(uploadsFolder, fileName);

                // 2. Conversão Real de Bytes para WebP
                using (var inputStream = imageFile.OpenReadStream())
                using (var managedStream = new SKManagedStream(inputStream))
                using (var bitmap = SKBitmap.Decode(managedStream))
                {
                    if (bitmap == null) return null; // Arquivo corrompido ou formato inválido

                    // Opcional: Redimensionar se for muito grande (ex: max 1200px largura)
                    // Isso economiza MUITO espaço e banda
                    using (var image = SKImage.FromBitmap(bitmap))
                    using (var data = image.Encode(SKEncodedImageFormat.Webp, 80)) // Qualidade 80 (Equilíbrio perfeito)
                    using (var outputStream = System.IO.File.OpenWrite(physicalPath))
                    {
                        data.SaveTo(outputStream);
                    }
                }

                // Retorna o caminho virtual para o banco
                return "/uploads/blog/" + fileName;
            }
            catch
            {
                return null;
            }
        }
    }

    


    }