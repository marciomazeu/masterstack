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
    [Route("{culture}/User")]
    public class UserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILocationService _locationService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IGeocodingService _geocodingService;

        public UserController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILocationService locationService,
            IWebHostEnvironment webHostEnvironment,
            IGeocodingService geocodingService)
        {
            _userManager = userManager;
            _context = context;
            _locationService = locationService;
            _webHostEnvironment = webHostEnvironment;
            _geocodingService = geocodingService;
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
            user.PreferredJobTitle = model.PreferredJobTitle;
            user.SearchRadiusKm = model.SearchRadiusKm;

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
    }
}