using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MasterStack.Data;
using MasterStack.Models;
using MasterStack.Models.Enums;
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace MasterStack.Controllers
{
    [Authorize(Roles = "Recruiter,Admin")]
    [Route("{controller}")]
    public class RecruiterController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public RecruiterController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _userManager = userManager;
            _localizer = localizer;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var query = _context.JobPostings
                .Include(j => j.Applications)
                .Where(j => string.IsNullOrEmpty(j.RedirectUrl) && j.RecruiterId != null)
                .AsQueryable();

            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin") || User.IsInRole("Admin");

            if (!isAdmin)
            {
                query = query.Where(j => j.RecruiterId == user.Id);
            }

            var jobs = await query
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return View(jobs);
        }

        [HttpGet("CreateJob")]
        public IActionResult CreateJob()
        {
            // ✅ Passa uma instância limpa com string vazia na Description para não dar NullReferenceException
            var model = new JobPosting
            {
                Description = string.Empty
            };

            return View(model);
        }

        [HttpPost("CreateJob")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateJob(CreateJobViewModel model, string culture)
        {
            var currentCulture = culture ?? (string)RouteData.Values["culture"] ?? "pt-BR";

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var job = new JobPosting
            {
                Title = model.Title,
                Description = model.Description,
                CompanyName = string.IsNullOrWhiteSpace(model.CompanyName) ? "Empresa" : model.CompanyName,
                Location = model.Location,
                IsRemote = model.IsRemote,
                MinSalary = model.MinSalary,
                MaxSalary = model.MaxSalary,
                Currency = model.Currency,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                IsInternal = true,
                SourceProvider = "Internal",
                RecruiterId = user.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.JobPostings.Add(job);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { culture = currentCulture });
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id, string culture = "pt-BR")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var job = await _context.JobPostings.FindAsync(id);
            if (job == null) return NotFound();

            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin") || User.IsInRole("Admin");
            if (!isAdmin && job.RecruiterId != user.Id)
            {
                return Forbid();
            }

            return View(job);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, JobPosting model, string culture = "pt-BR")
        {
            if (id != model.Id) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var jobToUpdate = await _context.JobPostings.FindAsync(id);
            if (jobToUpdate == null) return NotFound();

            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin") || User.IsInRole("Admin");
            if (!isAdmin && jobToUpdate.RecruiterId != user.Id)
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                jobToUpdate.Title = model.Title;
                jobToUpdate.CompanyName = model.CompanyName;
                jobToUpdate.Location = model.Location;
                jobToUpdate.IsRemote = model.IsRemote;
                jobToUpdate.Description = model.Description;
                jobToUpdate.IsActive = model.IsActive;

                _context.Update(jobToUpdate);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index), new { culture });
            }

            return View(model);
        }

        // ✅ ACEITA TANTO 'jobId' QUANTO 'id' PARA EVITAR 404 E DESCOMPASSO
        [HttpGet("Applications")]
        public async Task<IActionResult> Applications([FromRoute] string culture, [FromQuery] int? jobId, [FromQuery] int? id)
        {
            string currentCulture = string.IsNullOrEmpty(culture) ? "pt-BR" : culture;
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { culture = currentCulture });
            }

            int? targetJobId = jobId ?? id;

            var query = _context.JobApplications
                .Include(a => a.JobPosting)
                .Include(a => a.Candidate)
                .AsQueryable();

            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin") || User.IsInRole("Admin");
            if (!isAdmin)
            {
                query = query.Where(a => a.JobPosting.RecruiterId == user.Id);
            }

            if (targetJobId.HasValue && targetJobId > 0)
            {
                query = query.Where(a => a.JobPostingId == targetJobId.Value);
            }

            var applications = await query.OrderByDescending(a => a.AppliedAt).ToListAsync();

            ViewData["CurrentCulture"] = currentCulture;
            return View("JobApplications", applications);
        }

        [HttpPost("ToggleStatus/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id, string culture = "pt-BR")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var job = await _context.JobPostings.FindAsync(id);
            if (job == null) return NotFound();

            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin") || User.IsInRole("Admin");
            if (!isAdmin && job.RecruiterId != user.Id)
            {
                return Forbid();
            }

            job.IsActive = !job.IsActive;

            _context.Update(job);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { culture });
        }

        [HttpPost("UpdateApplicationStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateApplicationStatus(int applicationId, ApplicationStatus status, [FromRoute] string culture)
        {
            string currentCulture = string.IsNullOrEmpty(culture) ? "pt-BR" : culture;
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var application = await _context.JobApplications
                .Include(a => a.JobPosting)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null || (application.JobPosting.RecruiterId != user.Id && !User.IsInRole("Admin")))
            {
                return NotFound();
            }

            application.Status = status;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Status do candidato atualizado com sucesso!";

            // ✅ CORRIGIDO: enviando 'jobId' explicitamente para casar com a query string da Action Applications
            return RedirectToAction("Applications", "Recruiter", new { jobId = application.JobPostingId, culture = currentCulture });
        }

        [HttpPost("CloseJob/{id}")] // ✅ Corrigido: sem as chaves extras
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CloseJob(int id, string culture = "pt-BR")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // ✅ Corrigido: usando RecruiterId em vez de UserId
            var job = await _context.JobPostings.FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
            {
                return NotFound();
            }

            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin") || User.IsInRole("Admin");
            if (!isAdmin && job.RecruiterId != user.Id)
            {
                return Forbid();
            }

            if (!job.IsClosed)
            {
                job.IsClosed = true;
                job.ClosedAt = DateTime.UtcNow;

                _context.JobPostings.Update(job);
                await _context.SaveChangesAsync();

                TempData["Success"] = _localizer["Job_ClosedSuccess"].Value;
            }

            return RedirectToAction(nameof(Index), new { culture });
        }

        [HttpGet("DownloadResume/{applicationId}")]
        public async Task<IActionResult> DownloadResume(int applicationId, [FromServices] IWebHostEnvironment env)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var application = await _context.JobApplications
                .Include(a => a.JobPosting)
                .Include(a => a.Candidate)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null || string.IsNullOrEmpty(application.ResumePath))
            {
                return NotFound("Currículo não cadastrado nesta aplicação.");
            }

            if (application.JobPosting.RecruiterId != user.Id && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var cleanRelativePath = application.ResumePath.TrimStart('/', '\\');
            var physicalPath = Path.Combine(env.WebRootPath, cleanRelativePath);

            if (!System.IO.File.Exists(physicalPath))
            {
                return NotFound($"Arquivo físico não localizado no servidor em: {cleanRelativePath}");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(physicalPath);

            Response.Headers.Append("Content-Disposition", $"inline; filename=Curriculo_{application.CandidateId}.pdf");

            return File(fileBytes, "application/pdf");
        }
    }
}