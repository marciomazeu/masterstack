using MasterStack.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using MasterStack.Models.ViewModels;
using MasterStack.Models;

namespace MasterStack.Controllers
{
    [Authorize(Roles = "Recruiter,Admin")]
    [Microsoft.AspNetCore.Mvc.Route("{culture}/[controller]")]
    public class CandidateController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CandidateController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /{culture}/Candidate/Profile/{id}
        // Em Controllers/CandidateController.cs

// Em Controllers/CandidateController.cs

[HttpGet("Profile/{id}")]
public async Task<IActionResult> Profile(string id, string culture = "pt-BR")
{
    var candidate = await _context.Users
        .FirstOrDefaultAsync(u => u.Id == id);

    if (candidate == null) return NotFound("Candidato não localizado.");

    var resume = await _context.Set<Resume>()
        .FirstOrDefaultAsync(r => r.UserId == id);

    string? summary = null;
    var experiences = new List<ResumeExperience>();
    var educations = new List<ResumeEducation>();
    var skills = new List<ResumeSkill>();
    bool hasTranslation = false;

    if (resume != null)
    {
        // Busca a tradução do resumo especificamente para o idioma selecionado
        var translation = await _context.Set<ResumeTranslation>()
            .FirstOrDefaultAsync(t => t.ResumeId == resume.Id && t.Culture == culture);

        // Busca as experiências especificamente para o idioma selecionado
        experiences = await _context.Set<ResumeExperience>()
            .Where(e => e.ResumeId == resume.Id && e.Culture == culture)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();

        // Busca formações e habilidades para o idioma selecionado
        educations = await _context.Set<ResumeEducation>()
            .Where(e => e.ResumeId == resume.Id && e.Culture == culture)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();

        skills = await _context.Set<ResumeSkill>()
            .Where(s => s.ResumeId == resume.Id && s.Culture == culture)
            .ToListAsync();

        summary = translation?.Summary;

        // Se encontrou qualquer registro no idioma selecionado, marcamos como existente
        hasTranslation = translation != null || experiences.Any() || educations.Any() || skills.Any();
    }

    var viewModel = new CandidateProfileViewModel
    {
        Candidate = candidate,
        Resume = resume,
        Summary = summary,
        Experiences = experiences,
        Educations = educations,
        Skills = skills,
        HasTranslation = hasTranslation // 👈 Indica se há conteúdo no idioma ativo
    };

    return View(viewModel);
}
    }
}