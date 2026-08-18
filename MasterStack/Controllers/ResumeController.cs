using MasterStack.Data;
using MasterStack.Models;
using MasterStack.Services;
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MasterStack.Controllers
{
    [Authorize]
    [Route("{culture}/[controller]/[action]")]
    public class ResumeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ResumeParserService _parserService;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public ResumeController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager,
            ResumeParserService parserService,
            IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _userManager = userManager;
            _parserService = parserService;
            _localizer = localizer;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string culture = "pt-BR")
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var resume = await _context.Resumes
                .Include(r => r.Translations)
                .Include(r => r.Experiences)
                .Include(r => r.Educations)
                .Include(r => r.Skills)
                .FirstOrDefaultAsync(r => r.UserId == userId);

            if (resume == null)
            {
                return View(new ResumeFormViewModel { Culture = culture });
            }

            var translation = resume.Translations.FirstOrDefault(t => t.Culture == culture);

            var viewModel = new ResumeFormViewModel
            {
                ResumeId = resume.Id,
                Culture = culture,
                JobTitle = translation?.JobTitle,
                Summary = translation?.Summary,
                Phone = resume.Phone,
                LinkedInUrl = resume.LinkedInUrl,
                GithubUrl = resume.GitHubUrl,
                SkillsCsv = resume.Skills != null ? string.Join(", ", resume.Skills.Select(s => s.Name)) : "",
                Educations = resume.Educations
                    .Where(e => e.Culture == culture)
                    .Select(e => new ResumeEducationViewModel
                    {
                        Institution = e.Institution,
                        Degree = e.Degree,
                        StartDate = e.StartDate,
                        EndDate = e.EndDate
                    })
                    .ToList(),
                Experiences = resume.Experiences
                    .Where(e => e.Culture == culture)
                    .Select(e => new ResumeExperienceViewModel 
                    { 
                        Company = e.Company,
                        Role = e.Position,
                        StartDate = e.StartDate,
                        EndDate = e.EndDate,
                        Description = e.Description
                    })
                    .ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(ResumeFormViewModel model)
        {

            if (string.IsNullOrWhiteSpace(model.Culture))
            {
                model.Culture = System.Globalization.CultureInfo.CurrentCulture.Name; 
                // Pega o idioma ativo na requisição/URL (ex: se a URL for /en/Resume, será 'en')
            }

            // Se mesmo assim continuar nulo ou vazio, bloqueie o salvamento para não corromper os dados
            if (string.IsNullOrWhiteSpace(model.Culture))
            {
                ModelState.AddModelError("Culture", "O idioma não foi identificado.");
                return View("Index", model);
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // CORREÇÃO 1: Incluído Include(r => r.Educations) e Include(r => r.Skills)
            var resume = await _context.Resumes
                .Include(r => r.Translations)
                .Include(r => r.Experiences)
                .Include(r => r.Educations)
                .Include(r => r.Skills)
                .FirstOrDefaultAsync(r => r.UserId == userId);

            if (resume == null)
            {
                resume = new Resume { UserId = userId };
                _context.Resumes.Add(resume);
                await _context.SaveChangesAsync();
            }

            // 1. Atualiza ou insere a tradução no idioma selecionado
            var translation = resume.Translations.FirstOrDefault(t => t.Culture == model.Culture);
            if (translation == null)
            {
                translation = new ResumeTranslation { Culture = model.Culture, ResumeId = resume.Id };
                resume.Translations.Add(translation);
            }
            translation.JobTitle = model.JobTitle ?? string.Empty;
            translation.Summary = model.Summary ?? string.Empty;

            // 2. Atualiza dados globais de contato
            resume.Phone = model.Phone ?? string.Empty;
            resume.LinkedInUrl = model.LinkedInUrl ?? string.Empty;
            resume.GitHubUrl = model.GithubUrl ?? string.Empty;
            resume.UpdatedAt = DateTime.UtcNow;

            // 3. Atualiza as Experiências do Idioma
            var oldExperiences = resume.Experiences.Where(e => e.Culture == model.Culture).ToList();
            _context.ResumeExperiences.RemoveRange(oldExperiences);

            if (model.Experiences != null)
            {
                foreach (var exp in model.Experiences)
                {
                    resume.Experiences.Add(new ResumeExperience
                    {
                        Culture = model.Culture,
                        Company = exp.Company ?? string.Empty,
                        Position = exp.Role ?? string.Empty,
                        Location = string.Empty,
                        StartDate = exp.StartDate,
                        EndDate = exp.EndDate,
                        Description = exp.Description ?? string.Empty
                    });
                }
            }

            // 4. Atualiza a Formação Acadêmica do Idioma
            // 1. Busque e remova os registros existentes para a cultura selecionada
var oldEducations = await _context.ResumeEducations
    .Where(e => e.ResumeId == resume.Id && e.Culture == model.Culture)
    .ToListAsync();

_context.ResumeEducations.RemoveRange(oldEducations);

// 2. Mapeie os itens da ViewModel para a Entidade, injetando a Culture do model principal
if (model.Educations != null)
{
    foreach (var edu in model.Educations)
    {
        if (string.IsNullOrWhiteSpace(edu.Institution))
            continue;

        // Prioriza a cultura do item da lista; se vazia, usa a cultura do formulário pai
        var targetCulture = !string.IsNullOrWhiteSpace(edu.Culture) 
            ? edu.Culture 
            : model.Culture;

        resume.Educations.Add(new ResumeEducation
        {
            ResumeId = resume.Id,
            Culture = targetCulture,
            Institution = edu.Institution,
            Degree = edu.Degree ?? string.Empty,
            StartDate = edu.StartDate,
            EndDate = edu.EndDate
        });
    }
}


            // 5. Atualiza Habilidades (Skills)
            if (resume.Skills != null)
            {
                _context.ResumeSkills.RemoveRange(resume.Skills);
            }

            if (!string.IsNullOrWhiteSpace(model.SkillsCsv))
            {
                var skillNames = model.SkillsCsv
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct();

                foreach (var skillName in skillNames)
                {
                    resume.Skills.Add(new ResumeSkill
                    {
                        Name = skillName
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Currículo salvo com sucesso para o idioma selecionado!";

            return RedirectToAction("Index", new { culture = model.Culture });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ImportPdf(IFormFile pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0 || !pdfFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = _localizer["Resume_JsSelectPdfFirst"].Value });
            }

            try
            {
                var parsedData = _parserService.ParsePdf(pdfFile);
                return Json(new { success = true, data = parsedData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"{_localizer["Resume_JsImportError"].Value} {ex.Message}" });
            }
        }

        [HttpGet]
public async Task<IActionResult> ExportPdf(string culture = "pt-BR")
{
    // 1. Configura a cultura da Thread para i18n
    var cultureInfo = new System.Globalization.CultureInfo(culture);
    System.Globalization.CultureInfo.CurrentCulture = cultureInfo;
    System.Globalization.CultureInfo.CurrentUICulture = cultureInfo;

    QuestPDF.Settings.License = LicenseType.Community;

    //var user = await _userManager.GetUserAsync(User);
    var user = await _userManager.GetUserAsync(User) as ApplicationUser;
    if (user == null) return RedirectToAction("Login", "Account");

    var resume = await _context.Resumes
        .Include(r => r.Translations)
        .Include(r => r.Experiences)
        .Include(r => r.Educations)
        .Include(r => r.Skills)
        .FirstOrDefaultAsync(r => r.UserId == user.Id);

    if (resume == null) return NotFound(_localizer["Resume_NotFound"].Value);

    var translation = resume.Translations.FirstOrDefault(t => t.Culture == culture)
                   ?? resume.Translations.FirstOrDefault(t => t.Culture == "pt-BR");

    // 2. Método Auxiliar para obter rótulos traduzidos com Fallback seguro
    bool isEn = culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
    string GetText(string key, string fallbackPt, string fallbackEn)
    {
        var localized = _localizer[key];
        if (!localized.ResourceNotFound && !string.IsNullOrWhiteSpace(localized.Value))
            return localized.Value;
            
        return isEn ? fallbackEn : fallbackPt;
    }

    // Rótulos estáticos padronizados
    var txtSummaryHeader = GetText("Resume_PdfSectionSummary", "RESUMO PROFISSIONAL", "PROFESSIONAL SUMMARY");
    var txtSkillsHeader = GetText("Resume_PdfSectionSkills", "HABILIDADES TÉCNICAS", "TECHNICAL SKILLS");
    var txtExpHeader = GetText("Resume_PdfSectionExperience", "EXPERIÊNCIA PROFISSIONAL", "PROFESSIONAL EXPERIENCE");
    var txtEduHeader = GetText("Resume_PdfSectionEducation", "FORMAÇÃO ACADÊMICA", "EDUCATION");
    var txtPresent = GetText("Resume_PdfPresent", "Presente", "Present");
    var txtPage = GetText("Resume_PdfPage", "Página", "Page");
    var txtOf = GetText("Resume_PdfOf", "de", "of");
    var fileNamePrefix = GetText("Resume_PdfDefaultFileName", "Curriculo", "Resume");

    string defaultName = _localizer["Resume_DefaultYourName"].Value;
    string fullName = user.DisplayName ?? defaultName;
    string email = user.Email ?? "";

    // Paleta de cores sóbria para o padrão norte-americano
    var primaryColor = "#1A365D";
    var secondaryColor = "#4A5568";
    var lineDividerColor = "#CBD5E0";

    var experiences = resume.Experiences.Where(e => e.Culture == culture).ToList();
    var educations = resume.Educations.Where(e => e.Culture == culture).ToList();

    var document = Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Margin(32);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Arial").FontColor("#2D3748"));

            // CABEÇALHO (Layout Canadense)
            // CABEÇALHO (Estilo Québécois / Canadense)
page.Header().Column(col =>
{
    // 1. Nome Completo
    col.Item().Text(fullName.ToUpper())
        .FontSize(18)
        .Bold()
        .FontColor(primaryColor);

    // 2. Cargo Pretendido (JobTitle)
    // Se a tradução do idioma atual não tiver cargo, usa um fallback do banco de dados
    var jobTitle = !string.IsNullOrWhiteSpace(translation?.JobTitle) 
        ? translation.JobTitle 
        : resume.Translations.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.JobTitle))?.JobTitle;

    if (!string.IsNullOrWhiteSpace(jobTitle))
    {
        col.Item().PaddingTop(1).Text(jobTitle)
            .FontSize(12)
            .SemiBold()
            .FontColor(secondaryColor);
    }

    // 3. Linha Única de Contatos (Endereço, Telefone, Email, LinkedIn e GitHub)
    // 3. Linha Única de Contatos (Endereço do Usuário, Telefone, Email, LinkedIn e GitHub)
col.Item().PaddingTop(4).Text(text =>
{
    var contactItems = new List<string>();

    // MONTAGEM DO ENDEREÇO A PARTIR DOS DADOS DO USUÁRIO
    // Ajuste os nomes das propriedades (ex: user.Address, user.City) conforme sua classe ApplicationUser
    var locationParts = new List<string>();

    if (!string.IsNullOrWhiteSpace(user?.Address)) 
        locationParts.Add(user.Address);

    if (!string.IsNullOrWhiteSpace(user?.City)) 
        locationParts.Add(user.City);

    if (!string.IsNullOrWhiteSpace(user?.StateOrRegion)) 
        locationParts.Add(user.StateOrRegion);

    if (locationParts.Any())
    {
        contactItems.Add(string.Join(", ", locationParts));
    }

    // TELEFONE (Prioriza o do Resume, usa o do User como Fallback)
    var phone = !string.IsNullOrWhiteSpace(resume.Phone) ? resume.Phone : user?.PhoneNumber;
    if (!string.IsNullOrWhiteSpace(phone)) 
        contactItems.Add(phone);

    // E-MAIL
    if (!string.IsNullOrWhiteSpace(email)) 
        contactItems.Add(email);

    // LINKEDIN (Trata a string para exibição limpa)
    if (!string.IsNullOrWhiteSpace(resume.LinkedInUrl)) 
    {
        var cleanLinkedIn = resume.LinkedInUrl
            .Replace("https://www.", "")
            .Replace("http://www.", "")
            .Replace("https://", "")
            .Replace("http://", "");
        contactItems.Add(cleanLinkedIn);
    }

    // GITHUB
    if (!string.IsNullOrWhiteSpace(resume.GitHubUrl)) 
    {
        var cleanGitHub = resume.GitHubUrl
            .Replace("https://www.", "")
            .Replace("http://www.", "")
            .Replace("https://", "")
            .Replace("http://", "");
        contactItems.Add(cleanGitHub);
    }

    // RENDERIZAÇÃO DOS ITENS SEPARADOS POR PIPE (|)
    for (int i = 0; i < contactItems.Count; i++)
    {
        text.Span(contactItems[i]).FontSize(9f).FontColor(secondaryColor);
        if (i < contactItems.Count - 1)
        {
            text.Span("   |   ").FontSize(9f).FontColor(lineDividerColor);
        }
    }
});

    col.Item().PaddingTop(8).LineHorizontal(1f).LineColor(primaryColor);
});

            // CONTEÚDO PRINCIPAL
            page.Content().PaddingVertical(12).Column(col =>
            {
                col.Spacing(12);

                // 1. SUMMARY
                if (!string.IsNullOrWhiteSpace(translation?.Summary))
                {
                    col.Item().Column(sec =>
                    {
                        sec.Item().Text(txtSummaryHeader).FontSize(10.5f).Bold().FontColor(primaryColor);
                        sec.Item().PaddingTop(2).LineHorizontal(0.8f).LineColor(lineDividerColor);
                        sec.Item().PaddingTop(4).Text(translation.Summary).Justify().LineHeight(1.2f);
                    });
                }

                // 2. TECHNICAL SKILLS
                if (resume.Skills != null && resume.Skills.Any())
                {
                    col.Item().Column(sec =>
                    {
                        sec.Item().Text(txtSkillsHeader).FontSize(10.5f).Bold().FontColor(primaryColor);
                        sec.Item().PaddingTop(2).LineHorizontal(0.8f).LineColor(lineDividerColor);

                        sec.Item().PaddingTop(4).Text(text =>
                        {
                            var skillsList = resume.Skills.ToList();
                            for (int i = 0; i < skillsList.Count; i++)
                            {
                                text.Span(skillsList[i].Name).Bold().FontColor("#2B6CB0");
                                if (i < skillsList.Count - 1)
                                {
                                    text.Span("   •   ").FontColor(secondaryColor);
                                }
                            }
                        });
                    });
                }

                // 3. WORK EXPERIENCE (Formatação em Bullet Points / ATS-Friendly)
                if (experiences.Any())
                {
                    col.Item().Column(sec =>
                    {
                        sec.Item().Text(txtExpHeader).FontSize(14).Bold().FontColor(primaryColor);
                        sec.Item().PaddingTop(2).LineHorizontal(0.8f).LineColor(lineDividerColor);

                        foreach (var exp in experiences)
                        {
                            sec.Item().PaddingTop(6).Column(item =>
                            {
                                item.Item().Row(r =>
                                {
                                    r.RelativeItem().Text(t =>
                                    {
                                        t.Span(exp.Position).Bold().FontSize(12).FontColor("#2D3748");
                                        t.Span($"  —  {exp.Company}").Italic().FontColor(secondaryColor);
                                    });

                                    // Formatação de datas em padrão MMM yyyy (ex: Mar 2022 – Present)
                                    var startDate = exp.StartDate.ToString("MMM yyyy", cultureInfo);
                                    var endDate = exp.EndDate.HasValue ? exp.EndDate.Value.ToString("MMM yyyy", cultureInfo) : txtPresent;
                                    var dateRange = $"{startDate} – {endDate}";

                                    r.AutoItem().Text(dateRange).FontSize(12).FontColor(secondaryColor);
                                });

                                // Converte o texto da descrição em tópicos (bullet points) por quebra de linha
                                if (!string.IsNullOrWhiteSpace(exp.Description))
                                {
                                    var bulletPoints = exp.Description.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                                    foreach (var point in bulletPoints)
                                    {
                                        item.Item().PaddingTop(2).Row(bullet =>
                                        {
                                            bullet.AutoItem().Text("•  ").FontSize(12).FontColor(secondaryColor);
                                            bullet.RelativeItem().Text(point.Trim()).FontSize(12).LineHeight(1.5f);
                                        });
                                    }
                                }
                            });
                        }
                    });
                }

                // 4. EDUCATION (Padrão Canadense: Degree em destaque)
                if (educations.Any())
                {
                    col.Item().Column(sec =>
                    {
                        sec.Item().Text(txtEduHeader).FontSize(14).Bold().FontColor(primaryColor);
                        sec.Item().PaddingTop(2).LineHorizontal(0.8f).LineColor(lineDividerColor);

                        foreach (var edu in educations)
                        {
                            sec.Item().PaddingTop(6).Row(r =>
                            {
                                r.RelativeItem().Text(t =>
                                {
                                    t.Span(edu.Degree).Bold().FontSize(12).FontColor("#2D3748");
                                    t.Span($"  —  {edu.Institution}").Italic().FontColor(secondaryColor);
                                });

                                var startDate = edu.StartDate.ToString("yyyy");
                                var endDate = edu.EndDate.HasValue ? edu.EndDate.Value.ToString("yyyy") : txtPresent;
                                var eduDates = $"{startDate} – {endDate}";

                                r.AutoItem().Text(eduDates).FontSize(8.5f).FontColor(secondaryColor);
                            });
                        }
                    });
                }
            });

            // RODAPÉ
            page.Footer().AlignRight().Text(x =>
            {
                x.Span($"{txtPage} ").FontSize(8).FontColor(secondaryColor);
                x.CurrentPageNumber().FontSize(8).FontColor(secondaryColor);
                x.Span($" {txtOf} ").FontSize(8).FontColor(secondaryColor);
                x.TotalPages().FontSize(8).FontColor(secondaryColor);
            });
        });
    });

    byte[] pdfBytes = document.GeneratePdf();
    var fileName = $"{fileNamePrefix}_{fullName.Replace(" ", "_")}.pdf";
    return File(pdfBytes, "application/pdf", fileName);
}
    }
}