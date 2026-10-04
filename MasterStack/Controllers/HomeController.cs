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
using System.Text;

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

      [HttpGet("/sitemap.xml")]
        [AllowAnonymous]
        public async Task<IActionResult> Sitemap()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var sb = new StringBuilder();

            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            // 1. URLs Institucionais e Raiz por Idioma
            var culturas = new[] { "pt-BR", "en-US", "fr-CA" };
            foreach (var culture in culturas)
            {
                // Home Page
                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{baseUrl}/{culture}</loc>");
                sb.AppendLine("    <changefreq>daily</changefreq>");
                sb.AppendLine("    <priority>1.0</priority>");
                sb.AppendLine("  </url>");

                // Vagas
                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{baseUrl}/{culture}/Jobs</loc>");
                sb.AppendLine("    <changefreq>daily</changefreq>");
                sb.AppendLine("    <priority>0.9</priority>");
                sb.AppendLine("  </url>");

                // Index do Blog
                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{baseUrl}/{culture}/BlogPosts</loc>");
                sb.AppendLine("    <changefreq>daily</changefreq>");
                sb.AppendLine("    <priority>0.8</priority>");
                sb.AppendLine("  </url>");
            }

            // 2. Artigos do Blog Publicados
            var posts = await _context.BlogPosts
                .AsNoTracking()
                .Include(p => p.Translations)
                .Where(p => p.Translations.Any(t => t.IsPublished && !t.IsDeleted))
                .ToListAsync();

            foreach (var post in posts)
            {
                foreach (var trans in post.Translations.Where(t => t.IsPublished && !t.IsDeleted))
                {
                    // 💡 Correção do CS0019: Usa a data de criação se a de atualização não for aplicável
                    var lastMod = post.UpdatedAt != default ? post.UpdatedAt : post.CreatedAt;

                    sb.AppendLine("  <url>");
                    sb.AppendLine($"    <loc>{baseUrl}/{trans.Culture}/blog/{trans.Slug}</loc>");
                    sb.AppendLine($"    <lastmod>{lastMod:yyyy-MM-dd}</lastmod>");
                    sb.AppendLine("    <changefreq>monthly</changefreq>");
                    sb.AppendLine("    <priority>0.7</priority>");
                    sb.AppendLine("  </url>");
                }
            }

            sb.AppendLine("</urlset>");

            return Content(sb.ToString(), "application/xml", Encoding.UTF8);
        }
    }
}