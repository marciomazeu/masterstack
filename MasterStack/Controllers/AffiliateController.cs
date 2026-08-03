using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MasterStack.Data;
using MasterStack.Models;
using System;
using System.Threading.Tasks;

namespace MasterStack.Controllers
{
    [Authorize(Roles = "Admin")] // Garante que apenas administradores acessem
    public class AffiliateController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AffiliateController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Listagem de Produtos
        public async Task<IActionResult> Index()
        {
            var products = await _context.AffiliateProducts
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return View(products);
        }

        // GET: Criar
        public IActionResult Create()
        {
            return View(new AffiliateProduct());
        }

        // POST: Criar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AffiliateProduct product)
        {
            if (ModelState.IsValid)
            {
                product.CreatedAt = DateTime.UtcNow;
                _context.Add(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Produto de afiliado cadastrado com sucesso!";
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: Editar
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.AffiliateProducts.FindAsync(id);
            if (product == null) return NotFound();
            return View(product);
        }

        // POST: Editar / Renovar Link Expirado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AffiliateProduct product)
        {
            if (id != product.Id) return NotFound();

            if (ModelState.IsValid)
            {
                product.UpdatedAt = DateTime.UtcNow;

                // Se o usuário estendeu a data de expiração, reativa automaticamente
                if (product.ExpirationDate.HasValue && product.ExpirationDate.Value > DateTime.UtcNow)
                {
                    product.IsActive = true;
                }

                _context.Update(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Produto atualizado com sucesso!";
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }
    }
}