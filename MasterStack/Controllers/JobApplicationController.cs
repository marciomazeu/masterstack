using System;
using System.Threading.Tasks;
using MasterStack.Data;
using MasterStack.Models;
using MasterStack.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MasterStack.Controllers
{
   //[Authorize(Roles = "Candidate,Recruiter,Admin")]
   [Authorize]
    [Route("[controller]")]
    public class ApplicationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApplicationController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

  
       [HttpPost("Apply/{jobId}")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Apply(int jobId, IFormFile? resume, string culture, [FromServices] IWebHostEnvironment env)
{
    var currentCulture = culture ?? (string)RouteData.Values["culture"] ?? "pt-BR";
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Challenge();

    var job = await _context.JobPostings.FirstOrDefaultAsync(j => j.Id == jobId && j.IsActive);
    if (job == null) return NotFound();

    var alreadyApplied = await _context.JobApplications
        .AnyAsync(a => a.JobPostingId == jobId && a.CandidateId == user.Id);

    if (alreadyApplied)
    {
        TempData["WarningMessage"] = "Você já se candidatou a esta vaga.";
        return RedirectToAction("Details", "Jobs", new { id = jobId, culture = currentCulture });
    }

    string? savedFilePath = null;

    // Processa PDF apenas se o candidato tiver feito upload
    if (resume != null && resume.Length > 0)
    {
        var extension = Path.GetExtension(resume.FileName).ToLowerInvariant();
        if (extension != ".pdf")
        {
            TempData["ErrorMessage"] = "Apenas arquivos PDF são permitidos.";
            return RedirectToAction("Details", "Jobs", new { id = jobId, culture = currentCulture });
        }

        var uploadsFolder = Path.Combine(env.WebRootPath, "uploads", "resumes");
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{user.Id}_{jobId}_{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await resume.CopyToAsync(stream);
        }

        savedFilePath = $"/uploads/resumes/{uniqueFileName}";
    }

    var application = new JobApplication
    {
        JobPostingId = jobId,
        CandidateId = user.Id,
        AppliedAt = DateTime.UtcNow,
        Status = ApplicationStatus.Submitted,
        ResumePath = savedFilePath // Pode ser null caso não tenha enviado arquivo
    };

    _context.JobApplications.Add(application);
    await _context.SaveChangesAsync();

    TempData["SuccessMessage"] = "Candidatura realizada com sucesso!";
    return RedirectToAction("Details", "Jobs", new { id = jobId, culture = currentCulture });
}
    }
}