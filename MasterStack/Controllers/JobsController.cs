using System.Globalization;
using System.Threading.Tasks;
using MasterStack.Data;
using MasterStack.DTOs;
using MasterStack.Models;
using MasterStack.Services;
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace MasterStack.Controllers
{
    [Authorize] // 🔒 Exige login para acessar o módulo de vagas
    [Route("{culture}/[controller]")]
    public class JobsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILocationService _locationService;
        private readonly IGeocodingService _geocodingService;
        private readonly IConfiguration _configuration;
        private readonly IStringLocalizer<JobsController> _localizer;
        private readonly JobAggregatorService _jobAggregatorService;
        private readonly IHttpClientFactory _httpClientFactory;

        public JobsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILocationService locationService,
            IGeocodingService geocodingService,
            IConfiguration configuration,
            IStringLocalizer<JobsController> localizer,
            JobAggregatorService jobAggregatorService,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _userManager = userManager;
            _locationService = locationService;
            _geocodingService = geocodingService;
            _configuration = configuration;
            _localizer = localizer;
            _jobAggregatorService = jobAggregatorService;
            _httpClientFactory = httpClientFactory;
        }

        // GET: /{culture}/Jobs
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var jobs = await _context.JobPostings
                .Where(j => j.UserId == user.Id)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return View(jobs);
        }

        // GET: /{culture}/Jobs/Details/5
        [HttpGet("Details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var job = await _context.JobPostings.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null) return NotFound();

            return View(job);
        }

        // GET: /{culture}/Jobs/Enterprises
        [HttpGet("Enterprises")]
        public async Task<IActionResult> Enterprises()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!user.Latitude.HasValue || !user.Longitude.HasValue)
            {
                TempData["Warning"] = _localizer["Enterprises_ProfileLocationRequired"].Value;
            }

            double userLat = user.Latitude ?? 0;
            double userLng = user.Longitude ?? 0;
            int radiusKm = user.SearchRadiusKm > 0 ? user.SearchRadiusKm : 50;

            // Bounding box simples para filtrar empresas no SQL antes de carregar na memória
            double latDelta = radiusKm / 111.0;
            double lonDelta = radiusKm / (111.0 * Math.Cos(userLat * Math.PI / 180.0));

            double minLat = userLat - latDelta;
            double maxLat = userLat + latDelta;
            double minLon = userLng - lonDelta;
            double maxLon = userLng + lonDelta;

            var companiesFromDb = await _context.Companies
                .Where(c => c.Latitude.HasValue && c.Longitude.HasValue &&
                            c.Latitude >= minLat && c.Latitude <= maxLat &&
                            c.Longitude >= minLon && c.Longitude <= maxLon)
                .ToListAsync();

            var companiesList = companiesFromDb
                .Select(c => new CompanyDistanceViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    City = c.City,
                    Latitude = c.Latitude!.Value,
                    Longitude = c.Longitude!.Value,
                    DistanceInKm = _geocodingService.CalculateDistanceKm(userLat, userLng, c.Latitude.Value, c.Longitude.Value)
                })
                .Where(c => c.DistanceInKm <= radiusKm)
                .OrderBy(c => c.DistanceInKm)
                .ToList();

            var jobsList = await _context.JobPostings
                .Where(j => j.UserId == user.Id)
                .OrderByDescending(j => j.CreatedAt)
                .Take(50)
                .ToListAsync();

            var viewModel = new EnterprisesPageViewModel
            {
                User = user,
                Companies = companiesList,
                JobPostings = jobsList
            };

            return View(viewModel);
        }

        // GET: /{culture}/Jobs/CompaniesNearby
        [HttpGet("CompaniesNearby")]
        public async Task<IActionResult> GetCompaniesNearby([FromQuery] double radiusKm = 50)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user?.Latitude == null || user?.Longitude == null)
            {
                return BadRequest(_localizer["CompaniesNearby_ProfileLocationRequired"].Value);
            }

            double userLat = user.Latitude.Value;
            double userLng = user.Longitude.Value;

            double latDelta = radiusKm / 111.0;
            double lonDelta = radiusKm / (111.0 * Math.Cos(userLat * Math.PI / 180.0));

            var companies = await _context.Companies
                .Where(c => c.Latitude.HasValue && c.Longitude.HasValue &&
                            c.Latitude >= userLat - latDelta && c.Latitude <= userLat + latDelta &&
                            c.Longitude >= userLng - lonDelta && c.Longitude <= userLng + lonDelta)
                .ToListAsync();

            var nearbyCompanies = companies
                .Select(c => new
                {
                    Company = c,
                    DistanceKm = _geocodingService.CalculateDistanceKm(userLat, userLng, c.Latitude!.Value, c.Longitude!.Value)
                })
                .Where(x => x.DistanceKm <= radiusKm)
                .OrderBy(x => x.DistanceKm)
                .ToList();

            return Ok(nearbyCompanies);
        }

        // GET: /{culture}/Jobs/FetchNearbyCompaniesFromOSM
        [HttpGet("FetchNearbyCompaniesFromOSM")]
        public async Task<IActionResult> FetchNearbyCompaniesFromOSM()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized(new { success = false, message = _localizer["Osm_SessionExpired"].Value });
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null || !user.Latitude.HasValue || !user.Longitude.HasValue)
            {
                return BadRequest(new { success = false, message = _localizer["Osm_InvalidCoordinatesOrUser"].Value });
            }

            double lat = user.Latitude.Value;
            double lon = user.Longitude.Value;

            int radiusKm = user.SearchRadiusKm > 0 ? Math.Min(user.SearchRadiusKm, 10) : 10;
            int radiusMeters = radiusKm * 1000;

            string latStr = lat.ToString(CultureInfo.InvariantCulture);
            string lonStr = lon.ToString(CultureInfo.InvariantCulture);

            string overpassQuery = $"[out:json][timeout:10];node(around:{radiusMeters},{latStr},{lonStr})[\"office\"];out 30;";

            var endpoints = new[]
            {
                "https://overpass.kumi.systems/api/interpreter",
                "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
                "https://overpass-api.de/api/interpreter"
            };

            var httpClient = _httpClientFactory.CreateClient("OsmClient");
            httpClient.Timeout = TimeSpan.FromSeconds(12);

            string? jsonResponse = null;

            foreach (var endpoint in endpoints)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}?data={Uri.EscapeDataString(overpassQuery)}");
                    request.Headers.Add("User-Agent", "MasterStackApp/1.0 (contato@masterstack.com)");
                    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        jsonResponse = await response.Content.ReadAsStringAsync();
                        break;
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (string.IsNullOrEmpty(jsonResponse))
            {
                return StatusCode(503, new { success = false, message = _localizer["Osm_ServerUnstable"].Value });
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;
                var companies = new List<CompanyDto>();

                if (root.TryGetProperty("elements", out var elements))
                {
                    foreach (var element in elements.EnumerateArray())
                    {
                        double companyLat = 0;
                        double companyLon = 0;

                        if (element.TryGetProperty("lat", out var latProp))
                        {
                            companyLat = latProp.GetDouble();
                            companyLon = element.GetProperty("lon").GetDouble();
                        }
                        else if (element.TryGetProperty("center", out var centerProp))
                        {
                            companyLat = centerProp.GetProperty("lat").GetDouble();
                            companyLon = centerProp.GetProperty("lon").GetDouble();
                        }

                        string defaultName = _localizer["Osm_UnnamedCompany"].Value;
                        string defaultOffice = _localizer["Osm_Office"].Value;

                        string name = defaultName;
                        string officeType = defaultOffice;

                        if (element.TryGetProperty("tags", out var tags))
                        {
                            if (tags.TryGetProperty("name", out var nameProp))
                                name = nameProp.GetString() ?? name;

                            if (tags.TryGetProperty("office", out var officeProp))
                                officeType = officeProp.GetString() ?? officeType;
                        }

                        companies.Add(new CompanyDto
                        {
                            Id = element.GetProperty("id").GetInt64(),
                            Name = name,
                            OfficeType = officeType,
                            Latitude = companyLat,
                            Longitude = companyLon
                        });
                    }
                }

                return Ok(companies);
            }
            catch (System.Text.Json.JsonException)
            {
                return StatusCode(500, new { success = false, message = _localizer["Osm_ErrorProcessingMapData"].Value });
            }
        }

        // GET: /{culture}/Jobs/FetchTechJobsNearby
        [HttpGet("FetchTechJobsNearby")]
        public async Task<IActionResult> FetchTechJobsNearby([FromRoute] string culture)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized(_localizer["UserNotAuthenticated"].Value);

            string country = !string.IsNullOrWhiteSpace(user.CountryCode) ? user.CountryCode.Trim().ToLower() : "";
            string city = !string.IsNullOrWhiteSpace(user.City) ? user.City.Trim() : "";
            string what = !string.IsNullOrWhiteSpace(user.PreferredJobTitle) ? user.PreferredJobTitle.Trim() : "developer";
            int radiusKm = user.SearchRadiusKm > 0 ? user.SearchRadiusKm : 50;

            if (string.IsNullOrEmpty(country))
            {
                TempData["Warning"] = _localizer["FetchJobs_CountryCityRequired"].Value;
                return RedirectToAction("Enterprises", new { culture });
            }

            var httpClient = _httpClientFactory.CreateClient();

            // 1. Instanciar tasks
            var adzunaTask = FetchAdzunaJobsAsync(httpClient, user, country, what, city, radiusKm);
            var joobleTask = FetchJoobleJobsAsync(httpClient, user, country, what, city, radiusKm);

            var jsearchFilter = new JobSearchFilter { Query = what, Location = city, Page = 1 };
            var jsearchTask = _jobAggregatorService.AggregateJobsAsync(jsearchFilter);

            // 2. Aguardar a resolução em paralelo
            await Task.WhenAll(adzunaTask, joobleTask, jsearchTask);

            var adzunaJobs = adzunaTask.Result ?? new List<JobPosting>();
            var joobleJobs = joobleTask.Result ?? new List<JobPosting>();
            
            // Tratamento seguro contra NULOS para o JSearch
            var jsearchRaw = jsearchTask.Result ?? new List<JobDto>();
            var jsearchJobs = jsearchRaw.Select(j => new JobPosting
            {
                UserId = user.Id,
                Title = j.Title,
                CompanyName = j.Company,
                Location = $"[JSearch] {j.Location}",
                RedirectUrl = j.Url,
                Latitude = user.Latitude ?? 0,
                Longitude = user.Longitude ?? 0,
                IsExactLocation = false,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            // 3. Unificar listas
            var allJobs = new List<JobPosting>();
            allJobs.AddRange(adzunaJobs);
            allJobs.AddRange(joobleJobs);
            allJobs.AddRange(jsearchJobs);

            // 4. Salvar no Banco
            if (allJobs.Any())
            {
                var distinctJobs = allJobs
                    .GroupBy(j => string.IsNullOrEmpty(j.RedirectUrl) ? $"{j.Title}-{j.CompanyName}" : j.RedirectUrl)
                    .Select(g => g.First())
                    .Take(50)
                    .ToList();

                var oldJobs = _context.JobPostings.Where(j => j.UserId == user.Id);
                _context.JobPostings.RemoveRange(oldJobs);

                await _context.JobPostings.AddRangeAsync(distinctJobs);
                await _context.SaveChangesAsync();
            }

            // 5. Feedback atualizado considerando os 3 provedores
            int totalProvidersFound = 0;
            if (adzunaJobs.Any()) totalProvidersFound++;
            if (joobleJobs.Any()) totalProvidersFound++;
            if (jsearchJobs.Any()) totalProvidersFound++;

            if (totalProvidersFound == 0)
            {
                TempData["Warning"] = _localizer["FetchJobs_NoJobsFound"].Value;
            }
            else if (totalProvidersFound < 3)
            {
                TempData["Warning"] = _localizer["FetchJobs_PartialJobsFound"].Value;
            }

            return RedirectToAction("Enterprises", new { culture });
        }

        // POST: /{culture}/Jobs/UpdatePreferences 
        // POST: /{culture}/Jobs/UpdateSearchPreferences (Suporta ambos os nomes para evitar 404)
        [HttpPost("UpdatePreferences")]
        [HttpPost("UpdateSearchPreferences")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePreferences([FromRoute] string culture, int searchRadiusKm, string preferredJobTitle, bool includeRemoteCanada = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                user.SearchRadiusKm = searchRadiusKm > 0 ? searchRadiusKm : 25;
                user.PreferredJobTitle = string.IsNullOrWhiteSpace(preferredJobTitle) ? "developer" : preferredJobTitle.Trim();

                await _userManager.UpdateAsync(user);
                TempData["Success"] = _localizer["Preferences_SaveSuccess"].Value;
            }

            return RedirectToAction("Enterprises", new { culture });
        }

        [HttpGet("GetCities/{countryCode}")]
        public async Task<IActionResult> GetCities(string countryCode)
        {
            var cities = await _locationService.GetCitiesByCountryAsync(countryCode);
            return Json(cities);
        }

        // ==========================================
        // MÉTODOS AUXILIARES PRIVADOS
        // ==========================================
        private async Task<List<JobPosting>> FetchAdzunaJobsAsync(HttpClient httpClient, ApplicationUser user, string country, string what, string city, int radiusKm)
        {
            var jobs = new List<JobPosting>();
            string appId = _configuration["Adzuna:AppId"] ?? "";
            string appKey = _configuration["Adzuna:AppKey"] ?? "";

            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appKey)) return jobs;

            string remoteQuery = country switch
            {
                "br" => $"{what} remoto",
                "es" => $"{what} remoto",
                _ => $"{what} remote"
            };

            string safeWhat = Uri.EscapeDataString(what);
            string safeCity = Uri.EscapeDataString(city);
            string safeRemote = Uri.EscapeDataString(remoteQuery);

            string localUrl = $"https://api.adzuna.com/v1/api/jobs/{country}/search/1?app_id={appId}&app_key={appKey}&results_per_page=15&what={safeWhat}&where={safeCity}&distance={radiusKm}";
            string remoteUrl = $"https://api.adzuna.com/v1/api/jobs/{country}/search/1?app_id={appId}&app_key={appKey}&results_per_page=10&what={safeRemote}";

            try
            {
                var localResponse = await httpClient.GetAsync(localUrl);
                if (localResponse.IsSuccessStatusCode)
                {
                    var bytes = await localResponse.Content.ReadAsByteArrayAsync();
                    var json = System.Text.Encoding.UTF8.GetString(bytes);
                    jobs.AddRange(ParseAdzunaJobs(json, user));
                }

                var remoteResponse = await httpClient.GetAsync(remoteUrl);
                if (remoteResponse.IsSuccessStatusCode)
                {
                    var bytes = await remoteResponse.Content.ReadAsByteArrayAsync();
                    var json = System.Text.Encoding.UTF8.GetString(bytes);
                    jobs.AddRange(ParseAdzunaJobs(json, user));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API ERROR] Exceção na Adzuna: {ex.Message}");
            }

            return jobs;
        }

        private async Task<List<JobPosting>> FetchJoobleJobsAsync(HttpClient httpClient, ApplicationUser user, string country, string what, string city, int radiusKm)
        {
            var jobs = new List<JobPosting>();
            string apiKey = _configuration["Jooble:ApiKey"] ?? "";

            if (string.IsNullOrEmpty(apiKey)) return jobs;

            string url = $"https://jooble.org/api/{apiKey}";
            string locationQuery = !string.IsNullOrEmpty(city) ? $"{city}, {country.ToUpper()}" : country.ToUpper();

            var requestBody = new JoobleRequestDto
            {
                keywords = what,
                location = locationQuery,
                radius = radiusKm
            };

            try
            {
                var jsonContent = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var response = await httpClient.PostAsync(url, jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonString);

                    if (doc.RootElement.TryGetProperty("jobs", out var jobsArray))
                    {
                        double userLat = user.Latitude ?? 0;
                        double userLng = user.Longitude ?? 0;

                        string fallbackTitle = _localizer["Job_Untitled"].Value;
                        string fallbackCompany = _localizer["Job_CompanyNotSpecified"].Value;

                        foreach (var item in jobsArray.EnumerateArray())
                        {
                            string title = item.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? fallbackTitle : fallbackTitle;
                            string company = item.TryGetProperty("company", out var cProp) ? cProp.GetString() ?? fallbackCompany : fallbackCompany;
                            string location = item.TryGetProperty("location", out var lProp) ? lProp.GetString() ?? city : city;
                            string link = item.TryGetProperty("link", out var lkProp) ? lkProp.GetString() ?? "" : "";

                            jobs.Add(new JobPosting
                            {
                                UserId = user.Id,
                                Title = title,
                                CompanyName = company,
                                Location = $"[Jooble] {location}",
                                RedirectUrl = link,
                                Latitude = userLat,
                                Longitude = userLng,
                                IsExactLocation = false,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro Jooble: {ex.Message}");
            }

            return jobs;
        }

        private List<JobPosting> ParseAdzunaJobs(string jsonString, ApplicationUser user)
        {
            var parsedJobs = new List<JobPosting>();
            using var doc = System.Text.Json.JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("results", out var results))
            {
                double userLat = user.Latitude ?? 46.8138;
                double userLng = user.Longitude ?? -71.2080;

                string fallbackTitle = _localizer["Job_Untitled"].Value;
                string fallbackCompany = _localizer["Job_CompanyNotSpecified"].Value;
                string fallbackLocation = user.City ?? _localizer["Job_UndefinedRegion"].Value;

                foreach (var item in results.EnumerateArray())
                {
                    string jobTitle = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? fallbackTitle : fallbackTitle;
                    string companyName = fallbackCompany;

                    if (item.TryGetProperty("company", out var companyObj) && companyObj.TryGetProperty("display_name", out var compName))
                    {
                        companyName = compName.GetString() ?? companyName;
                    }

                    string locationName = fallbackLocation;
                    if (item.TryGetProperty("location", out var locObj) && locObj.TryGetProperty("display_name", out var locName))
                    {
                        locationName = locName.GetString() ?? locationName;
                    }

                    string link = item.TryGetProperty("redirect_url", out var redProp) ? redProp.GetString() ?? "" : "";

                    parsedJobs.Add(new JobPosting
                    {
                        UserId = user.Id,
                        Title = jobTitle,
                        CompanyName = companyName,
                        Location = $"[Adzuna] {locationName}",
                        RedirectUrl = link,
                        Latitude = userLat,
                        Longitude = userLng,
                        IsExactLocation = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            return parsedJobs;
        }

        [HttpGet("Search")]
        public async Task<IActionResult> Search(string query, string location)
        {
            var filter = new JobSearchFilter
            {
                Query = query ?? "",
                Location = location ?? "",
                Page = 1
            };

            List<JobDto> jobs = await _jobAggregatorService.AggregateJobsAsync(filter);

            return View(jobs);
        }

    }
}