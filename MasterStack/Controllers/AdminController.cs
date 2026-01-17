using MasterStack.Data; // Ajuste para o seu namespace
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace MasterStack.Controllers
{

    [Authorize]
    [Route("{culture}/Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Route("Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
                      // Buscamos todos os posts para listar na tabela de gestão
            var posts = await _context.BlogPosts
                .Include(p => p.Translations)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(posts);
        }
    }
}