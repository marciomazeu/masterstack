using MasterStack.Data;
using MasterStack.Models;
using MasterStack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MasterStack.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("{culture}/Admin/[controller]")]
    [Route("Admin/[controller]")]
    public class JobSourcesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDynamicJsonJobCollectorService _collectorService;

        public JobSourcesController(
            ApplicationDbContext context,
            IDynamicJsonJobCollectorService collectorService)
        {
            _context = context;
            _collectorService = collectorService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string culture = "fr-CA")
        {
            var sources = await _context.ExternalJobSources
                .OrderByDescending(s => s.IsActive)
                .ThenBy(s => s.Name)
                .ToListAsync();

            ViewBag.Culture = culture;
            return View(sources);
        }

        [HttpGet("Create")]
        public IActionResult Create(string culture = "fr-CA")
        {
            ViewBag.Culture = culture;
            return View(new ExternalJobSource());
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExternalJobSource model, string culture = "fr-CA")
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Culture = culture;
                return View(model);
            }

            model.CreatedAt = DateTime.UtcNow;
            _context.ExternalJobSources.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Fonte '{model.Name}' adicionada com sucesso!";
            return RedirectToAction(nameof(Index), new { culture });
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id, string culture = "fr-CA")
        {
            var source = await _context.ExternalJobSources.FindAsync(id);
            if (source == null) return NotFound();

            ViewBag.Culture = culture;
            return View(source);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ExternalJobSource model, string culture = "fr-CA")
        {
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Culture = culture;
                return View(model);
            }

            var existing = await _context.ExternalJobSources.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name = model.Name;
            existing.ProviderType = model.ProviderType;
            existing.EndpointOrSlug = model.EndpointOrSlug;
            existing.LocationFilter = model.LocationFilter;
            existing.TechKeywordsFilter = model.TechKeywordsFilter;
            existing.JsonRootArrayPath = model.JsonRootArrayPath;
            existing.MapTitleField = model.MapTitleField;
            existing.MapCompanyField = model.MapCompanyField;
            existing.MapLocationField = model.MapLocationField;
            existing.MapDescriptionField = model.MapDescriptionField;
            existing.MapUrlField = model.MapUrlField;
            existing.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Fonte '{existing.Name}' atualizada!";
            return RedirectToAction(nameof(Index), new { culture });
        }

        [HttpPost("Sync/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncNow(int id, string culture = "fr-CA")
        {
            var result = await _collectorService.SyncSingleSourceAsync(id);
            if (result.Success)
                TempData["Success"] = result.Message;
            else
                TempData["Warning"] = result.Message;

            return RedirectToAction(nameof(Index), new { culture });
        }

        [HttpPost("SyncAll")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncAll(string culture = "fr-CA")
        {
            await _collectorService.SyncAllActiveSourcesAsync();
            TempData["Success"] = "Sincronização em lote concluída! Verifique os contadores abaixo.";
            return RedirectToAction(nameof(Index), new { culture });
        }

        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string culture = "fr-CA")
        {
            var source = await _context.ExternalJobSources.FindAsync(id);
            if (source != null)
            {
                _context.ExternalJobSources.Remove(source);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Fonte removida.";
            }
            return RedirectToAction(nameof(Index), new { culture });
        }
    }
}