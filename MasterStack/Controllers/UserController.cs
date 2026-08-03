using System.Globalization;
using MasterStack.Data;
using MasterStack.Models;
using MasterStack.Services;
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using MasterStack.DTOs;

namespace MasterStack.Controllers
{
    [Authorize] // 🔒 Exige login para qualquer ação de usuário
    [Route("{culture}/User")]
    public class UserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILocationService _locationService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IGeocodingService _geocodingService;
        private readonly IConfiguration _configuration;

        public UserController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILocationService locationService,
            IWebHostEnvironment webHostEnvironment,
            IGeocodingService geocodingService,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _context = context;
            _locationService = locationService;
            _webHostEnvironment = webHostEnvironment;
            _geocodingService = geocodingService;
            _configuration = configuration;
        }

        // ==========================================
        // Método Auxiliar para Carregar Países
        // ==========================================
        private async Task PopulateCountriesViewBagAsync(string culture)
        {
            var countries = await _locationService.GetCountriesForCulture(culture);

            if (countries != null && countries.Any())
            {
                ViewBag.Countries = countries.Select(c => new SelectListItem
                {
                    Value = c.Iso2, 
                    Text = c.Name
                }).ToList();
            }
            else
            {
                ViewBag.Countries = new List<SelectListItem>();
            }
        }

        // ==========================================
        // 1. GET: Exibir Perfil
        // ==========================================
        [HttpGet("Profile")]
        [Authorize(Roles = "Admin,User,Author")]
        public async Task<IActionResult> Profile([FromRoute] string culture)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account", new { culture });

            var user = await _context.Users
                .Include(u => u.Translations)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();

            var isAuthorOrAdmin = await _userManager.IsInRoleAsync(user, "Admin") || 
                                  await _userManager.IsInRoleAsync(user, "Author");

            var selectedBio = user.Translations.FirstOrDefault(t => t.Culture == culture)?.Biography 
                        ?? user.Translations.FirstOrDefault(t => t.Culture == "pt-BR")?.Biography 
                        ?? "";

            await PopulateCountriesViewBagAsync(culture);
            ViewData["CurrentCulture"] = culture;

            double? lat = user.Latitude;
            double? lng = user.Longitude;

            string countryCode = user.CountryCode ?? "CA";

            var westernCountries = new[] { "CA", "US", "BR", "MX", "AR", "CL", "CO", "PE" };

            if (lng.HasValue && westernCountries.Contains(countryCode.ToUpper()))
            {
                if (lng.Value > 0)
                {
                    lng = -lng.Value;
                }

                if (lat.HasValue && lat.Value < 0)
                {
                    double temp = lat.Value;
                    lat = lng;
                    lng = temp;
                }
            }

            return View(new ProfileViewModel 
            { 
                DisplayName = user.DisplayName, 
                Bio = selectedBio,
                CurrentImageUrl = user.ProfileImageUrl,
                IsAuthorOrAdmin = isAuthorOrAdmin,
                IsTwoFactorEnabled = user.TwoFactorEnabled,

                FacebookUrl = user.FacebookUrl,
                InstagramUrl = user.InstagramUrl,
                TwitterUrl = user.TwitterUrl, 
                LinkedInUrl = user.LinkedInUrl, 
                GitHubUrl = user.GitHubUrl, 

                StreetAddress = user.Address,
                City = user.City,
                StateOrRegion = user.StateOrRegion,
                PostalCode = user.PostalCode,
                CountryCode = countryCode,
                Latitude = lat,
                Longitude = lng
            });
        }

        [HttpGet("GetCitiesByCountry")] 
        public async Task<IActionResult> GetCitiesByCountry(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode))
            {
                return Json(new List<object>());
            }

            var cities = await _locationService.GetCitiesByCountryAsync(countryCode);
            var result = cities.Select(c => new { id = c, name = c }).ToList();
            result.Add(new { id = "OTHER", name = "➕ Outra / Não listada" });

            return Json(result);
        }

        // ==========================================
        // 2. POST: Atualizar Perfil & Localização
        // ==========================================
        [HttpPost("UpdateProfile")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,User,Author")]
        public async Task<IActionResult> UpdateProfile([FromRoute] string culture, ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account", new { culture });

            if (!ModelState.IsValid) 
            {
                model.CurrentImageUrl = user.ProfileImageUrl;
                model.IsTwoFactorEnabled = user.TwoFactorEnabled;
                
                await PopulateCountriesViewBagAsync(culture);
                ViewData["CurrentCulture"] = culture;
                
                return View("Profile", model);
            }

            string? oldImagePath = null;

            if (model.NewImage != null && model.NewImage.Length > 0)
            {
                var uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "profiles");
                if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(model.NewImage.FileName)}";
                var filePath = Path.Combine(uploadFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.NewImage.CopyToAsync(stream);
                }

                if (!string.IsNullOrEmpty(user.ProfileImageUrl) && !user.ProfileImageUrl.Contains("default"))
                {
                    oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, user.ProfileImageUrl.TrimStart('/'));
                }

                user.ProfileImageUrl = "/uploads/profiles/" + fileName;
            }

            user.FacebookUrl = model.FacebookUrl;
            user.InstagramUrl = model.InstagramUrl;
            user.TwitterUrl = model.TwitterUrl;
            user.LinkedInUrl = model.LinkedInUrl;
            user.GitHubUrl = model.GitHubUrl;

            user.Address = model.StreetAddress;
            user.City = model.City;
            user.StateOrRegion = model.StateOrRegion;
            user.PostalCode = model.PostalCode;
            user.CountryCode = model.CountryCode;

            if (!string.IsNullOrEmpty(model.CountryCode) && (!string.IsNullOrEmpty(model.PostalCode) || !string.IsNullOrEmpty(model.City)))
            {
                var (val1, val2) = await _geocodingService.GetCoordinatesAsync(
                    model.StreetAddress ?? "", 
                    model.City ?? "", 
                    model.CountryCode, 
                    model.PostalCode ?? ""
                );

                if (val1.HasValue && val2.HasValue)
                {
                    double lat = val1.Value;
                    double lon = val2.Value;

                    if (Math.Abs(lat) > 90)
                    {
                        double temp = lat;
                        lat = lon;
                        lon = temp;
                    }

                    string country = model.CountryCode.ToUpper();

                    if (country == "CA" || country == "US")
                    {
                        lat = Math.Abs(lat);     
                        lon = -Math.Abs(lon);    
                    }
                    else if (country == "BR")
                    {
                        lat = -Math.Abs(lat);    
                        lon = -Math.Abs(lon);    
                    }

                    user.Latitude = lat;
                    user.Longitude = lon;
                }
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded) 
            {
                if (oldImagePath != null && System.IO.File.Exists(oldImagePath))
                {
                    try { System.IO.File.Delete(oldImagePath); } catch { }
                }

                TempData["Success"] = GetLocalizedSuccessMessage(culture);
                return RedirectToAction(nameof(Profile), new { culture });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await PopulateCountriesViewBagAsync(culture);
            ViewData["CurrentCulture"] = culture;
            return View("Profile", model);
        }

        private static string GetLocalizedSuccessMessage(string culture)
        {
            return culture.ToLower() switch
            {
                "en-us" => "Profile and location updated successfully!",
                "es-es" => "¡Perfil y ubicación actualizados con éxito!",
                _ => "Perfil e localização atualizados com sucesso!"
            };
        }

        [HttpGet("GetCities/{countryCode}")]
        public async Task<IActionResult> GetCities(string countryCode)
        {
            var cities = await _locationService.GetCitiesByCountryAsync(countryCode);
            return Json(cities);
        }

        [HttpGet("Enterprises")]
public async Task<IActionResult> Enterprises()
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return NotFound();

    // Requer coordenadas reais do cadastro do usuário
    if (!user.Latitude.HasValue || !user.Longitude.HasValue)
    {
        TempData["Warning"] = "Cadastre sua cidade e país no perfil para visualizar o mapa e empresas da sua região.";
    }

    double userLat = user.Latitude ?? 0;
    double userLng = user.Longitude ?? 0;

    int radiusKm = user.SearchRadiusKm > 0 ? user.SearchRadiusKm : 50;

    // Busca apenas empresas válidas do banco
    var companiesFromDb = await _context.Companies
        .Where(c => c.Latitude.HasValue && c.Longitude.HasValue)
        .ToListAsync();

    // Filtra e calcula a distância em relação ao usuário
    var companiesList = companiesFromDb
        .Select(c => new CompanyDistanceViewModel
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            City = c.City,
            Latitude = c.Latitude!.Value,
            Longitude = c.Longitude!.Value,
            DistanceInKm = CalculateDistance(userLat, userLng, c.Latitude.Value, c.Longitude.Value)
        })
        .Where(c => c.DistanceInKm <= radiusKm) // Filtra pelo raio real do usuário
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

        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Math.Round(R * c, 1);
        }

        private static double ToRadians(double angle) => (Math.PI / 180) * angle;

        [HttpGet("CompaniesNearby")]
        public async Task<IActionResult> GetCompaniesNearby([FromQuery] double radiusKm = 50)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user?.Latitude == null || user?.Longitude == null)
            {
                return BadRequest("Cadastre seu CEP ou Cidade no perfil para buscar empresas próximas.");
            }

            var companies = await _context.Companies
                .Where(c => c.Latitude.HasValue && c.Longitude.HasValue)
                .ToListAsync();

            var nearbyCompanies = companies
                .Select(c => new
                {
                    Company = c,
                    DistanceKm = _geocodingService.CalculateDistanceKm(user.Latitude.Value, user.Longitude.Value, c.Latitude!.Value, c.Longitude!.Value)
                })
                .Where(x => x.DistanceKm <= radiusKm)
                .OrderBy(x => x.DistanceKm)
                .ToList();

            return Ok(nearbyCompanies);
        }

        [HttpGet("FetchNearbyCompaniesFromOSM")]
        public async Task<IActionResult> FetchNearbyCompaniesFromOSM()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null || !user.Latitude.HasValue || !user.Longitude.HasValue)
            {
                return BadRequest("Coordenadas inválidas ou usuário não autenticado.");
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

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(12);
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "MasterStackApp/1.0 (contato@masterstack.com)");
            httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
            );

            string? jsonResponse = null;

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var url = $"{endpoint}?data={Uri.EscapeDataString(overpassQuery)}";
                    var response = await httpClient.GetAsync(url);

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
                return StatusCode(503, "O servidor de mapas do OpenStreetMap está instável no momento. Tente novamente em alguns segundos.");
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

                        string name = "Empresa sem nome registrado";
                        string officeType = "Escritório";

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
                return StatusCode(500, "Erro ao processar a resposta dos servidores do mapa.");
            }
        }

        // ==========================================
// BUSCA HÍBRIDA DE VAGAS (ADZUNA + JOOBLE)
// ==========================================
[HttpGet("FetchTechJobsNearby")]
public async Task<IActionResult> FetchTechJobsNearby()
{
    
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Unauthorized("Usuário não autenticado.");

        Console.WriteLine($"=== DADOS DO USUÁRIO NO BANCO ===");
Console.WriteLine($"City: '{user.City}'");
Console.WriteLine($"CountryCode: '{user.CountryCode}'");
Console.WriteLine($"PreferredJobTitle: '{user.PreferredJobTitle}'");
Console.WriteLine($"Latitude: {user.Latitude}");
Console.WriteLine($"Longitude: {user.Longitude}");
Console.WriteLine($"================================");

    // 📍 1. PAÍS DO CADASTRO (Se nulo/vazio, notifica o usuário)
    string country = !string.IsNullOrWhiteSpace(user.CountryCode) 
        ? user.CountryCode.Trim().ToLower() 
        : "";

    // 🏙️ 2. CIDADE DO CADASTRO
    string city = !string.IsNullOrWhiteSpace(user.City) 
        ? user.City.Trim() 
        : "";

    // 💼 3. PREFERÊNCIA DE CARGO DO CADASTRO (ou "developer" como termo neutro global)
    string what = !string.IsNullOrWhiteSpace(user.PreferredJobTitle) 
        ? user.PreferredJobTitle.Trim() 
        : "developer";

    // 📏 4. RAIO DO CADASTRO
    int radiusKm = user.SearchRadiusKm > 0 ? user.SearchRadiusKm : 50;

    // Se o usuário não tiver país definido no cadastro, avisa e cancela
    if (string.IsNullOrEmpty(country))
    {
        TempData["Warning"] = "Por favor, cadastre seu país e cidade no perfil para buscar vagas da sua região.";
        return RedirectToAction("Enterprises");
    }

    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("User-Agent", "MasterStack/1.0");

    // 🚀 Chamadas dinâmicas orientadas 100% ao cadastro do usuário
    var adzunaTask = FetchAdzunaJobsAsync(httpClient, user, country, what, city, radiusKm);
    var joobleTask = FetchJoobleJobsAsync(httpClient, user, country, what, city, radiusKm);

    // Aguarda o término de ambas as tarefas
    await Task.WhenAll(adzunaTask, joobleTask);

    var adzunaJobs = adzunaTask.Result ?? new List<JobPosting>();
    var joobleJobs = joobleTask.Result ?? new List<JobPosting>();

    Console.WriteLine($"[API TEST] Adzuna retornou: {adzunaJobs.Count} vagas.");
    Console.WriteLine($"[API TEST] Jooble retornou: {joobleJobs.Count} vagas.");

    // Unifica os resultados
    var allJobs = new List<JobPosting>();
    allJobs.AddRange(adzunaJobs);
    allJobs.AddRange(joobleJobs);

    // Se houve resultados, atualiza o banco de dados
    if (allJobs.Any())
    {
        var distinctJobs = allJobs
            .GroupBy(j => string.IsNullOrEmpty(j.RedirectUrl) ? $"{j.Title}-{j.CompanyName}" : j.RedirectUrl)
            .Select(g => g.First())
            .Take(40)
            .ToList();

        var oldJobs = _context.JobPostings.Where(j => j.UserId == user.Id);
        _context.JobPostings.RemoveRange(oldJobs);

        await _context.JobPostings.AddRangeAsync(distinctJobs);
        await _context.SaveChangesAsync();
    }

    // Mensagens de Alerta reais baseadas na contagem exata da requisição atual
    if (!adzunaJobs.Any() && !joobleJobs.Any())
    {
        TempData["Warning"] = "Nenhuma vaga foi encontrada no momento para sua região.";
    }
    else if (!adzunaJobs.Any())
    {
        TempData["Warning"] = "A Jooble retornou resultados, mas a Adzuna não encontrou vagas para esta busca.";
    }
    else if (!joobleJobs.Any())
    {
        TempData["Warning"] = "A Adzuna retornou resultados, mas a Jooble não encontrou vagas para esta busca.";
    }

    return RedirectToAction("Enterprises");
}

// ==========================================
// MÉTODO AUXILIAR: ADZUNA (BYPASS DE CHARSET)
// ==========================================
private async Task<List<JobPosting>> FetchAdzunaJobsAsync(HttpClient httpClient, ApplicationUser user, string country, string what, string city, int radiusKm)
{
    var jobs = new List<JobPosting>();
    string appId = _configuration["Adzuna:AppId"];
    string appKey = _configuration["Adzuna:AppKey"];

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
        // 1. Busca Local
        var localResponse = await httpClient.GetAsync(localUrl);
        if (localResponse.IsSuccessStatusCode)
        {
            // 💡 Solução: Lê os bytes diretamente e converte via UTF8 (evita a exceção de Charset inválido)
            var bytes = await localResponse.Content.ReadAsByteArrayAsync();
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            
            var localJobs = ParseAdzunaJobs(json, user, false);
            jobs.AddRange(localJobs);
        }

        // 2. Busca Remota
        var remoteResponse = await httpClient.GetAsync(remoteUrl);
        if (remoteResponse.IsSuccessStatusCode)
        {
            var bytes = await remoteResponse.Content.ReadAsByteArrayAsync();
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            
            var remoteJobs = ParseAdzunaJobs(json, user, true);
            jobs.AddRange(remoteJobs);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[API ERROR] Exceção na Adzuna: {ex.Message}");
    }

    return jobs;
}

// ==========================================
// MÉTODO AUXILIAR: JOOBLE
// ==========================================
private async Task<List<JobPosting>> FetchJoobleJobsAsync(HttpClient httpClient, ApplicationUser user, string country, string what, string city, int radiusKm)
{
    var jobs = new List<JobPosting>();
    string apiKey = _configuration["Jooble:ApiKey"];

    if (string.IsNullOrEmpty(apiKey)) return jobs;

    // A Jooble aceita requisição por POST enviando JSON
    string url = $"https://jooble.org/api/{apiKey}";

    // Se a cidade estiver cadastrada, busca "Cidade, PAÍS". Se não tiver, busca apenas pelo "PAÍS"
        string locationQuery = !string.IsNullOrEmpty(city) 
            ? $"{city}, {country.ToUpper()}" 
            : country.ToUpper();

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

                foreach (var item in jobsArray.EnumerateArray())
                {
                    string title = item.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "Sem título" : "Sem título";
                    string company = item.TryGetProperty("company", out var cProp) ? cProp.GetString() ?? "Empresa não informada" : "Empresa não informada";
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

        // ==========================================
// PARSER DA ADZUNA (RETORNA LISTA PRÓPRIA)
// ==========================================
private List<JobPosting> ParseAdzunaJobs(string jsonString, ApplicationUser user, bool isForceRemote)
{
    var parsedJobs = new List<JobPosting>();
    using var doc = System.Text.Json.JsonDocument.Parse(jsonString);
    var root = doc.RootElement;

    if (root.TryGetProperty("results", out var results))
    {
        double userLat = user.Latitude ?? 46.8138;
        double userLng = user.Longitude ?? -71.2080;

        foreach (var item in results.EnumerateArray())
        {
            string jobTitle = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "Sem título" : "Sem título";
            
            string companyName = "Empresa não informada";
            if (item.TryGetProperty("company", out var companyObj) && companyObj.TryGetProperty("display_name", out var compName))
            {
                companyName = compName.GetString() ?? companyName;
            }

            string locationName = user.City ?? "Região Indefinida";
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

        [HttpPost("UpdateSearchPreferences")]
        public async Task<IActionResult> UpdateSearchPreferences(int searchRadiusKm, string preferredJobTitle, bool includeRemoteCanada = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                user.SearchRadiusKm = searchRadiusKm > 0 ? searchRadiusKm : 25;
                user.PreferredJobTitle = string.IsNullOrWhiteSpace(preferredJobTitle) ? "developer" : preferredJobTitle.Trim();

                await _userManager.UpdateAsync(user);
                TempData["Success"] = "Preferências de busca salvas com sucesso!";
            }

            return RedirectToAction(nameof(Enterprises));
        }
    }
}