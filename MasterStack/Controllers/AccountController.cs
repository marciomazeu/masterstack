using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using MasterStack.Models;
using MasterStack.Data;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Identity.UI.Services;
using MasterStack.ViewModels;

namespace MasterStack.Controllers
{
    [Route("{culture}/Account")]
    public class AccountController : Controller
    {
       private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IEmailSender _emailSender;

    private readonly IConfiguration _configuration;

    public AccountController(
        SignInManager<ApplicationUser> signInManager, 
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender, 
        IStringLocalizer<SharedResource> localizer,
        IConfiguration configuration)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _localizer = localizer;
        _emailSender = emailSender;
        _configuration = configuration;
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
        ViewBag.SiteKey = _configuration["ReCaptcha:SiteKey"];
        return View();
    }

  [HttpPost("Register")]
public async Task<IActionResult> Register(string email, string password, string confirmPassword, string displayName, string culture)
{
    var currentCulture = culture ?? (string)RouteData.Values["culture"] ?? "pt-BR";

    // 1. Validação do reCAPTCHA
    var captchaToken = Request.Form["g-recaptcha-response"];
    if (string.IsNullOrEmpty(captchaToken) || !await IsReCaptchaValid(captchaToken))
    {
        ViewBag.Error = "Falha na verificação de segurança (reCAPTCHA).";
        return View();
    }

    // 2. Validação Manual de Senha
    if (password != confirmPassword)
    {
        ViewBag.Error = _localizer["PasswordsDoNotMatch"].Value; 
        return View();
    }

    // 3. Criação do Usuário
    var user = new ApplicationUser { UserName = email, Email = email, DisplayName = displayName };
    var result = await _userManager.CreateAsync(user, password);

    if (result.Succeeded)
    {
        await _userManager.AddToRoleAsync(user, "User");

        // 4. Fluxo de E-mail de Confirmação
        try 
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action("ConfirmEmail", "Account", 
                new { userId = user.Id, token = token, culture = currentCulture }, Request.Scheme);

            string subject = _localizer["EmailConfirmationSubject"];
            string body = $@"
                <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2>{_localizer["EmailGreeting"]} {displayName},</h2>
                    <p>{_localizer["EmailInstruction"]}</p>
                    <div style='margin: 30px 0;'>
                        <a href='{confirmationLink}' 
                           style='background-color: #007bff; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                            {_localizer["ConfirmLinkText"]}
                        </a>
                    </div>
                    <hr />
                    <p style='font-size: 12px; color: #666;'>{_localizer["EmailFooter"]}</p>
                </div>";

            await _emailSender.SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO AO ENVIAR E-MAIL]: {ex.Message}");
            // Em produção, você registraria isso em um log real (Serilog/NLog)
        }

        // 5. Login e Redirecionamento
        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home", new { culture = currentCulture });
    }

    // 6. Tratamento de Erros do Identity (Tradução)
    var firstError = result.Errors.FirstOrDefault();
    if (firstError != null)
    {
        var translatedError = _localizer[firstError.Code].Value;
        ViewBag.Error = translatedError != firstError.Code 
                        ? translatedError 
                        : _localizer["RegistrationError"].Value;
    }

    return View();
}

    [HttpGet("ConfirmEmail")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token, string culture)
    {
        var currentCulture = culture ?? "pt-BR";

        if (userId == null || token == null)
        {
            return RedirectToAction("Index", "Home", new { culture = currentCulture });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            ViewBag.Error = $"Usuário ID {userId} não encontrado.";
            return View("Error");
        }

        // Tenta confirmar o e-mail com o token recebido
        var result = await _userManager.ConfirmEmailAsync(user, token);

        if (result.Succeeded)
        {
            // Aqui você pode redirecionar para uma página de sucesso ou direto para o Login
            return View("ConfirmEmailSuccess"); 
        }
        else
        {
            ViewBag.Error = "Erro ao confirmar o e-mail. O token pode ter expirado.";
            return View("Error");
        }
    }

    [HttpGet("AccessDenied")]
    public IActionResult AccessDenied()
    {
        return View();
    }

        // 1. Abre a página para digitar o e-mail
        [HttpGet("ForgotPassword")]
        public IActionResult ForgotPassword(string culture)
        {
            return View();
        }

        // 2. Processa o pedido e envia o e-mail
        [HttpPost("ForgotPassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email, string culture)
        {
            var currentCulture = culture ?? "pt-BR";
            
            // Buscamos o usuário pelo e-mail
            var user = await _userManager.FindByEmailAsync(email);

            // Se o usuário não existe ou o e-mail não está confirmado (opcional), 
            // não revelamos o erro. Apenas fingimos que enviamos.
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                return RedirectToAction("ForgotPasswordConfirmation", new { culture = currentCulture });
            }

            // Gerar o Token de Reset de Senha
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Gerar o Link (Apontando para a futura Action 'ResetPassword')
            var callbackUrl = Url.Action("ResetPassword", "Account", 
                new { token = token, email = user.Email, culture = currentCulture }, Request.Scheme);

            // Disparar o E-mail usando seu serviço e localizer
            // await _emailSender.SendEmailAsync(user.Email!, _localizer["ResetPasswordSubject"],
            //         $"{_localizer["EmailGreeting"]} {user.DisplayName},<br/><br/>" +
            //         $"{_localizer["ResetPasswordInstruction"]} <a href='{callbackUrl}'>{_localizer["ResetPasswordLinkText"]}</a>");

            // 1. Defina o assunto e o corpo com HTML estruturado
string subject = _localizer["ResetPasswordSubject"];
string body = $@"
<!DOCTYPE html>
<html lang='{currentCulture}'>
<head>
    <meta charset='UTF-8'>
    <style>
        .container {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #eee; border-radius: 10px; }}
        .header {{ text-align: center; border-bottom: 2px solid #007bff; padding-bottom: 10px; margin-bottom: 20px; }}
        .button-container {{ text-align: center; margin: 30px 0; }}
        .button {{ background-color: #007bff; color: white !important; padding: 12px 25px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block; }}
        .footer {{ font-size: 12px; color: #777; margin-top: 30px; border-top: 1px solid #eee; padding-top: 10px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>MasterStack</h2>
        </div>
        <p>{_localizer["EmailGreeting"]} {user.DisplayName},</p>
        <p>{_localizer["ResetPasswordInstruction"]}</p>
        
        <div class='button-container'>
            <a href='{callbackUrl}' class='button'>{_localizer["ResetPasswordLinkText"]}</a>
        </div>
        
        <p>Se você não solicitou a redefinição de senha, nenhuma ação adicional é necessária e você pode ignorar este e-mail com segurança.</p>
        
        <div class='footer'>
            <p>Este é um e-mail automático enviado pelo sistema MasterStack.<br>
            Por favor, não responda a este e-mail.</p>
        </div>
    </div>
</body>
</html>";

// 2. Envie o e-mail
await _emailSender.SendEmailAsync(user.Email!, subject, body);

            return RedirectToAction("ForgotPasswordConfirmation", new { culture = currentCulture });
        }

        // 1. GET: Abre o formulário de nova senha
        [HttpGet("ResetPassword")]
        public IActionResult ResetPassword(string token, string email, string culture)
        {
            if (token == null || email == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Criamos um ViewModel simples para carregar o token e o e-mail
            var model = new ResetPasswordViewModel { Token = token, Email = email };
            return View(model);
        }

        // 2. POST: Processa a nova senha
        [HttpPost("ResetPassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var culture = (string)RouteData.Values["culture"] ?? "pt-BR";

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) 
            {
                // Novamente, não revelamos que o usuário não existe
                return RedirectToAction("ResetPasswordConfirmation", new { culture = culture });
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation", new { culture = culture });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        private async Task<bool> IsReCaptchaValid(string token)
{
    if (string.IsNullOrEmpty(token)) return false;

    var secretKey = _configuration["ReCaptcha:SecretKey"];
    using var client = new HttpClient();
    
    var response = await client.PostAsync(
        $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={token}", 
        null);

    if (response.IsSuccessStatusCode)
    {
        var jsonResponse = await response.Content.ReadAsStringAsync();
        // Na v3, precisamos olhar o 'success' E o 'score'
        // Exemplo de resposta: {"success": true, "score": 0.9, "action": "register", ...}
        
        return jsonResponse.Contains("\"success\": true") && 
               !jsonResponse.Contains("\"score\": 0.0") && 
               !jsonResponse.Contains("\"score\": 0.1"); // Rejeita scores muito baixos
    }
    return false;
}

        [HttpGet("ForgotPasswordConfirmation")]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet("ResetPasswordConfirmation")]
        public IActionResult ResetPasswordConfirmation()
        {
            // Esta Action apenas exibe a View informando que a senha foi alterada.
            return View();
        }
    }
}