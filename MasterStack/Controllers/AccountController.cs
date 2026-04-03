using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using MasterStack.Models;
using MasterStack.Data;
using Microsoft.Extensions.Localization;

namespace MasterStack.Controllers
{
    [Route("{culture}/Account")]
    public class AccountController : Controller
    {
       private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AccountController(SignInManager<ApplicationUser> signInManager, 
    UserManager<ApplicationUser> userManager, IStringLocalizer<SharedResource> localizer)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _localizer = localizer;
    }

        [HttpGet("Login")]
    public IActionResult Login(string returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

        [HttpPost("Login")]
public async Task<IActionResult> Login(string username, string password, string culture, string returnUrl = null)
{
    // 1. Tenta o login usando o SignInManager do Identity
    // O terceiro parâmetro (false) é para 'RememberMe'
    // O quarto parâmetro (false) é para 'LockoutOnFailure' (bloquear se errar muito)
    var result = await _signInManager.PasswordSignInAsync(username, password, isPersistent: false, lockoutOnFailure: false);

    if (result.Succeeded)
    {
        // Pega a cultura da rota ou parâmetro
        culture ??= (string)RouteData.Values["culture"] ?? "pt-BR";

        // Se houver uma URL de retorno (ex: tentou acessar Admin sem logar), manda pra lá
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Dashboard", "Admin", new { culture = culture });
    }

    // Se falhar, volta pra tela de login com erro
    //ViewBag.Error = "Usuário ou senha inválidos no sistema Identity";
    ViewBag.Error = _localizer["InvalidLoginAttempt"].Value;
    return View();
}

   // Mude para HttpPost por segurança, mas se o seu link for um <a> simples, use HttpGet
       [HttpPost] // O Identity exige POST para evitar deslogamentos acidentais via links
[HttpPost("Logout")] // <--- CORREÇÃO AQUI: Apenas "Logout", pois o prefixo já vem da classe
    public async Task<IActionResult> Logout(string culture)
    {
        await _signInManager.SignOutAsync();
        
        // Pega a cultura da rota se o parâmetro vier nulo
        var currentCulture = culture ?? (string)RouteData.Values["culture"] ?? "pt-BR";

        return RedirectToAction("Index", "Home", new { culture = currentCulture });
    }

    // 1. O método GET (Abre a página quando você digita a URL)
    [HttpGet("Register")]
    public IActionResult Register()
    {
        return View();
    }

  [HttpPost("Register")]
public async Task<IActionResult> Register(string email, string password, string confirmPassword, string displayName, string culture)
{
    var currentCulture = culture ?? (string)RouteData.Values["culture"] ?? "pt-BR";

    // 1. Validação Manual (Já usando sua chave de recurso)
    if (password != confirmPassword)
    {
        ViewBag.Error = _localizer["PasswordsDoNotMatch"].Value; 
        return View();
    }

    var user = new ApplicationUser { UserName = email, Email = email, DisplayName = displayName };
    var result = await _userManager.CreateAsync(user, password);

    if (result.Succeeded)
    {
        await _userManager.AddToRoleAsync(user, "User");
        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home", new { culture = currentCulture });
    }

    // 2. O PULO DO GATO: Tradução dos erros do Identity
    // Em vez de Description (Inglês), usamos o Code como chave para o nosso Localizer
    var firstError = result.Errors.FirstOrDefault();
    if (firstError != null)
    {
        // Tenta traduzir pelo Código do Erro (ex: DuplicateUserName)
        // Se não existir no seu .resx, ele mostra a descrição original ou uma mensagem genérica
        var translatedError = _localizer[firstError.Code].Value;
        
        ViewBag.Error = translatedError != firstError.Code 
                        ? translatedError 
                        : _localizer["RegistrationError"].Value;
    }

    return View();
}

    [HttpGet("AccessDenied")]
    public IActionResult AccessDenied()
    {
        return View();
    }
    }
}