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

namespace MasterStack.Controllers
{
    [Authorize] // 🔒 Exige login para qualquer ação de usuário
    [Route("[controller]")]
    public class UserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILocationService _locationService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IGeocodingService _geocodingService;
        private readonly ILogger<UserController> _logger;
        private readonly ICloudStorageService _cloudStorageService;

        public UserController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILocationService locationService,
            IWebHostEnvironment webHostEnvironment,
            IGeocodingService geocodingService,
            ILogger<UserController> logger,
            ICloudStorageService cloudStorageService)
        {
            _userManager = userManager;
            _context = context;
            _locationService = locationService;
            _webHostEnvironment = webHostEnvironment;
            _geocodingService = geocodingService;
            _logger = logger;
            _cloudStorageService = cloudStorageService;
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

        private string GetLocalizedSuccessMessage(string culture)
        {
            return culture switch
            {
                "fr-CA" => "Profil mis à jour avec succès!",
                "en-US" => "Profile updated successfully!",
                _ => "Perfil atualizado com sucesso!"
            };
        }

        // ==========================================
        // 1. GET: Exibir Perfil
        // ==========================================
        [HttpGet("{culture}/Profile")]
        [Authorize(Roles = "Admin,User,Author,Candidate,Recruiter")]
        public async Task<IActionResult> Profile([FromRoute] string culture)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) 
                return RedirectToAction("Login", "Account", new { culture });

            var user = await _context.Users
                .Include(u => u.Translations)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();

            // 1. Verifica permissões de Autor ou Admin
            bool isAuthorOrAdmin = await _userManager.IsInRoleAsync(user, "Admin") || 
                                await _userManager.IsInRoleAsync(user, "Author");

            // 2. Prepara ViewBag e Culture
            await PopulateCountriesViewBagAsync(culture);
            ViewData["CurrentCulture"] = culture;

            // 3. Monta e retorna a ViewModel
            var model = new ProfileViewModel 
            { 
                DisplayName = user.DisplayName, 
                JobTitle = user.JobTitle,
                Bio = user.Bio,
                Bio_EN = user.Bio_EN,
                Bio_FR = user.Bio_FR,
                AvatarUrl = user.ProfileImageUrl,
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
                CountryCode = user.CountryCode ?? "CA",
                Latitude = user.Latitude,
                Longitude = user.Longitude
            };

            return View(model);
        }
        // ==========================================
        // 2. GET: Helper de Cidades via AJAX
        // ==========================================
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
        // 3. POST: Atualizar Perfil & Localização
        // ==========================================
        [HttpPost("{culture}/User/UpdateProfile")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,User,Author,Candidate,Recruiter")]
        public async Task<IActionResult> UpdateProfile([FromRoute] string culture, ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account", new { culture });

            if (!ModelState.IsValid) 
            {
                model.AvatarUrl = user.ProfileImageUrl;
                model.IsTwoFactorEnabled = user.TwoFactorEnabled;
                
                await PopulateCountriesViewBagAsync(culture);
                ViewData["CurrentCulture"] = culture;
                
                return View("Index", model);
            }

            string? oldImageUrl = user.ProfileImageUrl;

            // 1. Processamento da Foto de Perfil via DigitalOcean Spaces (S3)
            var fileToUpload = model.AvatarFile ?? model.NewImage;

            if (fileToUpload != null && fileToUpload.Length > 0)
            {
                try
                {
                    // Upload direto para a pasta "profiles" no DigitalOcean Spaces
                    var uploadedUrl = await _cloudStorageService.UploadFileAsync(fileToUpload, "profiles");

                    if (!string.IsNullOrEmpty(uploadedUrl))
                    {
                        user.ProfileImageUrl = uploadedUrl;

                        // Elimina a imagem antiga do Spaces se for uma imagem armazenada na nuvem
                        if (!string.IsNullOrEmpty(oldImageUrl) && oldImageUrl.Contains("digitaloceanspaces.com"))
                        {
                            _ = _cloudStorageService.DeleteFileAsync(oldImageUrl);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao enviar a foto de perfil para o DigitalOcean Spaces do utilizador {UserId}", user.Id);
                    ModelState.AddModelError(string.Empty, "Erro ao processar o upload da imagem na nuvem.");
                }
            }

            // 2. Atualização dos Dados Principais e do Autor do Blog
            user.DisplayName = model.DisplayName;
            user.JobTitle = model.JobTitle;
            user.Bio = model.Bio;
            user.Bio_EN = model.Bio_EN;
            user.Bio_FR = model.Bio_FR;

            // 3. Redes Sociais
            user.FacebookUrl = model.FacebookUrl;
            user.InstagramUrl = model.InstagramUrl;
            user.TwitterUrl = model.TwitterUrl;
            user.LinkedInUrl = model.LinkedInUrl;
            user.GitHubUrl = model.GitHubUrl;

            // 4. Localização e Preferências
            user.Address = model.StreetAddress;
            user.City = model.City;
            user.StateOrRegion = model.StateOrRegion;
            user.PostalCode = model.PostalCode;
            user.CountryCode = model.CountryCode;
            user.PreferredJobTitle = model.PreferredJobTitle;
            user.SearchRadiusKm = model.SearchRadiusKm;

            // 5. Geocoding
            if (!string.IsNullOrEmpty(model.CountryCode) && (!string.IsNullOrEmpty(model.PostalCode) || !string.IsNullOrEmpty(model.City)))
            {
                try
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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao obter coordenadas do Geocoding service.");
                }
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded) 
            {
                TempData["Success"] = GetLocalizedSuccessMessage(culture);
                return RedirectToAction(nameof(Profile), new { culture });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await PopulateCountriesViewBagAsync(culture);
            ViewData["CurrentCulture"] = culture;
            return View("Index", model);
        }
    }
}