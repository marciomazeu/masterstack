using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MasterStack.Data;
using MasterStack.Models;

namespace MasterStack.Controllers
{
    [Route("{culture}/BlogPosts")] // Define a rota base com o idioma
    public class BlogPostsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BlogPostsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: BlogPosts
        [Route("")] // Faz com que {culture}/BlogPosts caia aqui
        [Route("/{culture}/Blog")] // Atalho amigável: {culture}/Blog
        public async Task<IActionResult> Index(string culture)
        {
            // Pega a cultura atual da URL (ex: pt-BR)
            var currentCulture = System.Globalization.CultureInfo.CurrentCulture.Name;

            // Filtra no banco apenas os posts que combinam com o idioma do site
            var postsFiltrados = _context.BlogPosts
                .Where(p => p.Culture == currentCulture);

            return View(await postsFiltrados.ToListAsync());
        }

        // GET: BlogPosts/Details/5
        [Route("Details/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var blogPost = await _context.BlogPosts
                .FirstOrDefaultAsync(m => m.Id == id);
            if (blogPost == null)
            {
                return NotFound();
            }

            return View(blogPost);
        }

        // GET: BlogPosts/Create
        [Route("Create")] // Garante que a rota de criar seja {culture}/BlogPosts/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: BlogPosts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Create")]
        public async Task<IActionResult> Create([Bind("Id,Culture,Title,Content,CreatedAt")] BlogPost blogPost, IFormFile foto)
        {
            // Removemos o ImageUrl da validação inicial pois vamos preenchê-lo manualmente
            ModelState.Remove("ImageUrl");

            if (ModelState.IsValid)
            {
                try
                {
                    if (foto != null && foto.Length > 0)
                    {
                        string extensao = Path.GetExtension(foto.FileName).ToLower();

                        // Limpeza robusta do título para o nome do arquivo
                        string tituloLimpo = blogPost.Title ?? "post";
                        foreach (char c in Path.GetInvalidFileNameChars())
                        {
                            tituloLimpo = tituloLimpo.Replace(c, '-');
                        }
                        tituloLimpo = tituloLimpo.Replace(" ", "-").ToLower();

                        string novoNomeArquivo = $"{tituloLimpo}-{Guid.NewGuid().ToString().Substring(0, 8)}{extensao}";
                        string caminhoPasta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                        if (!Directory.Exists(caminhoPasta))
                        {
                            Directory.CreateDirectory(caminhoPasta);
                        }

                        string caminhoCompleto = Path.Combine(caminhoPasta, novoNomeArquivo);

                        using (var stream = new FileStream(caminhoCompleto, FileMode.Create))
                        {
                            await foto.CopyToAsync(stream);
                        }

                        blogPost.ImageUrl = "/uploads/" + novoNomeArquivo;
                    }

                    _context.Add(blogPost);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index), new { culture = blogPost.Culture });
                }
                catch (Exception ex)
                {
                    // Adiciona o erro para aparecer na tela se algo falhar no salvamento
                    ModelState.AddModelError("", "Erro ao salvar: " + ex.Message);
                }
            }

            // Se chegou aqui, algo falhou. Vamos devolver a View com os erros.
            return View(blogPost);
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,Culture,Title,Content,CreatedAt")] BlogPost blogPost, IFormFile foto)
        {
            if (id != blogPost.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // 1. Busca a versão que está no banco agora, sem rastreamento para evitar conflito
                    var postNoBanco = await _context.BlogPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

                    if (postNoBanco == null) return NotFound();

                    // 2. Força o Entity Framework a entender que este objeto foi modificado
                    _context.Update(blogPost);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index), new { culture = blogPost.Culture });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BlogPostExists(blogPost.Id)) return NotFound();
                    else throw;
                }
            }
            return View(blogPost);
        }

        // GET: BlogPosts/Delete/5
        [Route("Delete/{id}")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var blogPost = await _context.BlogPosts
                .FirstOrDefaultAsync(m => m.Id == id);
            if (blogPost == null) return NotFound();

            return View(blogPost);
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
                _context.BlogPosts.Remove(blogPost);
            }

            await _context.SaveChangesAsync();
            // Importante: Redirecionar mantendo a cultura
            return RedirectToAction(nameof(Index), new { culture = RouteData.Values["culture"] });
        }

        private bool BlogPostExists(int id)
        {
            return _context.BlogPosts.Any(e => e.Id == id);
        }
    }
}
