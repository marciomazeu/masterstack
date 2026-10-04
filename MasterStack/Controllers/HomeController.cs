using MasterStack.Data;
using MasterStack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MasterStack.ViewModels;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;

namespace MasterStack.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [HttpGet("/")]
        [HttpGet("/{culture}")]
        [HttpGet("/{culture}/Home/Index")]
        public async Task<IActionResult> Index([FromRoute] string? culture)
        {
            var currentCulture = !string.IsNullOrEmpty(culture) ? culture : "pt-BR";

            var latestPosts = await _context.BlogPosts
                .Include(p => p.Translations)
                .OrderByDescending(p => p.CreatedAt)
                .Take(3)
                .ToListAsync();

            var featuredJobs = await _context.JobPostings
                .OrderByDescending(j => j.CreatedAt)
                .Take(3)
                .Select(j => new JobItemViewModel
                {
                    Id = j.Id,
                    Title = j.Title,
                    CompanyName = !string.IsNullOrWhiteSpace(j.CompanyName) 
                        ? j.CompanyName 
                        : "Empresa Confidencial",
                    Location = !string.IsNullOrWhiteSpace(j.Location) 
                        ? j.Location.Replace("[Adzuna]", "").Trim() 
                        : null,
                    JobType = "Home_Job_Type_FullTime_Remote", 
                    PostedDate = j.CreatedAt,
                    Skills = new List<string>(),
                    RedirectUrl = j.RedirectUrl
                })
                .ToListAsync();

            var viewModel = new HomeViewModel
            {
                LatestPosts = latestPosts,
                FeaturedJobs = featuredJobs
            };

            return View(viewModel);
        }

        [HttpGet("/Privacy")]
        [HttpGet("/{culture}/Home/Privacy")]
        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), Path = "/" }
            );

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                var segments = returnUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0 && new[] { "pt-BR", "en-US", "fr-CA" }.Contains(segments[0]))
                {
                    segments[0] = culture;
                    returnUrl = "/" + string.Join('/', segments);
                }

                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Home", new { culture = culture });
        }

        [AllowAnonymous]
        [Route("Home/Error/{statusCode?}")]
        [Route("/{culture}/Home/Error/{statusCode?}")]
        public async Task<IActionResult> Error(int? statusCode)
        {
            string currentCulture = CultureInfo.CurrentCulture.Name;

            var reExecuteFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            if (reExecuteFeature != null)
            {
                var originalPath = reExecuteFeature.OriginalPath;
                var segments = originalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (segments.Length > 0 && new[] { "pt-BR", "en-US", "fr-CA" }.Contains(segments[0]))
                {
                    currentCulture = segments[0];

                    var cultureInfo = new CultureInfo(currentCulture);
                    CultureInfo.CurrentCulture = cultureInfo;
                    CultureInfo.CurrentUICulture = cultureInfo;

                    HttpContext.Features.Set<IRequestCultureFeature>(
                        new RequestCultureFeature(new RequestCulture(cultureInfo), null)
                    );
                }
            }

            List<BlogPostTranslation> sugestoes = new List<BlogPostTranslation>();
            try
            {
                sugestoes = await _context.BlogPostTranslations
                    .AsNoTracking()
                    .Include(t => t.BlogPost)
                    .Where(t => t.Culture.ToLower() == currentCulture.ToLower() && t.BlogPost != null && !t.IsDeleted && t.IsPublished)
                    .OrderByDescending(t => t.BlogPost.CreatedAt)
                    .Take(3)
                    .ToListAsync();
            }
            catch
            {
                sugestoes = new List<BlogPostTranslation>();
            }

            if (statusCode == 404)
            {
                return View("~/Views/Home/NotFound.cshtml", sugestoes);
            }

            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [AllowAnonymous]
        [Route("Home/NotFound/{statusCode}")]
        public async Task<IActionResult> NotFoundPage(int statusCode)
        {
            string currentCulture = CultureInfo.CurrentCulture.Name;

            var suggestedPosts = await _context.BlogPostTranslations
                .Include(t => t.BlogPost)
                .Where(t => t.Culture == currentCulture && t.BlogPost != null && !t.IsDeleted && t.IsPublished)
                .OrderByDescending(t => t.BlogPost.CreatedAt)
                .Take(3)
                .ToListAsync();

            return View("NotFound", suggestedPosts);
        }

        [Route("/{culture}/p/{slug}")]
        public async Task<IActionResult> Page(string culture, string slug)
        {
            var page = await _context.StaticPages
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Slug == slug);

            if (page == null) return NotFound();

            var translation = page.Translations.FirstOrDefault(t => t.Culture == culture) 
                              ?? page.Translations.FirstOrDefault();

            if (translation == null) return RedirectToAction("Index", "Home", new { culture = culture });

            return View(translation);
        }
    }
}