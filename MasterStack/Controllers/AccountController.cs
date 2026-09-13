using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using MasterStack.Models;
using MasterStack.Data;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Identity.UI.Services;
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Http;          // Para IFormFile (imagens)
using Microsoft.AspNetCore.Hosting;      // Para IWebHostEnvironment
using System.IO;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Encodings.Web;
using Serilog;

namespace MasterStack.Controllers
{
    [Route("{culture}/[controller]")]
    public class AccountController : Controller
    {
       private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IEmailSender _emailSender;

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager, 
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender, 
        IStringLocalizer<SharedResource> localizer,
        IConfiguration configuration,
        IWebHostEnvironment webHostEnvironment,
        ApplicationDbContext context,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _localizer = localizer;
        _emailSender = emailSender;
        _configuration = configuration;
        _webHostEnvironment = webHostEnvironment;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [HttpGet("Login")] // 👈 ADICIONE ESTA LINHA
    [HttpGet("/Account/Login")]
    public IActionResult Login(string culture, string returnUrl = null)
    {
        // 1. Prioriza a cultura da URL, se não houver, usa a do sistema
        ViewBag.CurrentCulture = culture ?? RouteData.Values["culture"] ?? "fr-CA";
        
        // 2. Armazena a URL de retorno para o formulário saber para onde ir após o sucesso
        ViewBag.ReturnUrl = returnUrl;
        
        return View();
    }

   [HttpPost]
[HttpPost("Login")]
[HttpPost("/Account/Login")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Login(string username, string password, string culture, string returnUrl = null)
{
    string currentCulture = string.IsNullOrEmpty(culture) ? "pt-BR" : culture;

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
    {
        ViewBag.Error = _localizer["InvalidLoginAttempt"].Value;
        return View();
    }

    var cleanInput = username.Trim().ToLower();

    var user = await _userManager.FindByEmailAsync(cleanInput) 
            ?? await _userManager.FindByNameAsync(cleanInput)
            ?? _context.Users.FirstOrDefault(u => u.NormalizedEmail == cleanInput.ToUpper() || u.NormalizedUserName == cleanInput.ToUpper());

    if (user == null)
    {
        ViewBag.Error = _localizer["InvalidLoginAttempt"].Value;
        return View();
    }

    // 1. Garante a confirmação de e-mail e persiste no banco caso esteja pendente
    if (!user.EmailConfirmed)
    {
        _logger.LogInformation(">>> EMAIL NÃO CONFIRMADO. Atualizando EmailConfirmed=true no PostgreSQL para {Email}", user.Email);
        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);
    }

    // 2. Tenta autenticar usando o UserName oficial do Identity
    var result = await _signInManager.PasswordSignInAsync(user.UserName!, password, isPersistent: false, lockoutOnFailure: true);

    _logger.LogInformation(">>> RESULTADO DO LOGIN PARA {Email}: Succeeded={Succeeded}, LockedOut={IsLockedOut}, Requires2FA={Requires2FA}", 
        user.Email, result.Succeeded, result.IsLockedOut, result.RequiresTwoFactor);

    if (result.RequiresTwoFactor)
    {
        return RedirectToAction("LoginWith2FA", "Account", new { culture = currentCulture, returnUrl = returnUrl });
    }

    if (result.Succeeded)
    {
        _logger.LogInformation(">>> LOGIN REALIZADO COM SUCESSO PARA {Email}", user.Email);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        if (await _userManager.IsInRoleAsync(user, "Admin") || await _userManager.IsInRoleAsync(user, "Author"))
        {
            return RedirectToAction("Dashboard", "Admin", new { culture = currentCulture });
        }

        if (await _userManager.IsInRoleAsync(user, "Recruiter"))
        {
            return RedirectToAction("Index", "Recruiter", new { culture = currentCulture });
        }

        return RedirectToAction("Index", "Home", new { culture = currentCulture });
    }

    if (result.IsLockedOut)
    {
        ViewBag.Error = _localizer["AccountLocked"].Value; 
        return View();
    }

    ViewBag.Error = _localizer["InvalidLoginAttempt"].Value;
    return View();
}

    // 1. GET do Login com 2FA
[   HttpGet("LoginWith2FA")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginWith2FA([FromRoute] string culture, string returnUrl = null)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { culture });
        }

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["CurrentCulture"] = string.IsNullOrEmpty(culture) ? "pt-BR" : culture;

        return View(new LoginWith2FAViewModel());
    }

    // 2. POST do Login com 2FA
    [HttpPost("LoginWith2FA")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginWith2FA(LoginWith2FAViewModel model, [FromRoute] string culture, string returnUrl = null)
    {
        string currentCulture = string.IsNullOrEmpty(culture) ? "pt-BR" : culture;

        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null) 
        {
            return RedirectToAction("Login", "Account", new { culture = currentCulture });
        }

        if (!ModelState.IsValid)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["CurrentCulture"] = currentCulture;
            return View(model);
        }

        var cleanCode = model.TwoFactorCode?.Replace(" ", "").Replace("-", "");

        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(cleanCode, isPersistent: model.RememberMe, rememberClient: false);

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (await _userManager.IsInRoleAsync(user, "Admin") || await _userManager.IsInRoleAsync(user, "Author"))
            {
                return RedirectToAction("Dashboard", "Admin", new { culture = currentCulture });
            }

            return RedirectToAction("Profile", "User", new { culture = currentCulture });
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, _localizer["AccountLocked"].Value);
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Código inválido. Verifique o seu aplicativo autenticador.");
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["CurrentCulture"] = currentCulture;
        return View(model);
    }

// 3. POST para Envio de Código por E-mail
[HttpPost("SendEmailCode")]
[AllowAnonymous]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SendEmailCode([FromRoute] string culture, string returnUrl = null)
{
    string currentCulture = string.IsNullOrEmpty(culture) ? "pt-BR" : culture;

    var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
    if (user == null)
    {
        return RedirectToAction("Login", "Account", new { culture = currentCulture });
    }

    // Gera o token do tipo Email do Identity
    var code = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");

    // Envia o e-mail via IEmailSender
    string subject = "Seu Código de Acesso 2FA";
    string message = $"Olá, seu código de verificação em duas etapas é: <strong>{code}</strong>";

    try
    {
        await _emailSender.SendEmailAsync(user.Email, subject, message);
        TempData["SuccessMessage"] = "Código enviado para o seu e-mail com sucesso!";
    }
    catch
    {
        TempData["ErrorMessage"] = "Não foi possível enviar o e-mail no momento. Verifique as configurações de SMTP.";
    }

    return RedirectToAction(nameof(LoginWith2FA), new { culture = currentCulture, returnUrl });
}

   

    // 1. O método GET (Abre a página quando você digita a URL)
    [HttpGet("Register")]
    public IActionResult Register()
    {
        ViewBag.SiteKey = _configuration["ReCaptcha:SiteKey"];
        return View();
    }

[HttpPost("Register")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Register(
    string email, 
    string password, 
    string confirmPassword, 
    string displayName, 
    string userType,
    string culture)
{
    var currentCulture = culture ?? (string)RouteData.Values["culture"] ?? "pt-BR";

    var captchaToken = Request.Form["g-recaptcha-response"].ToString();

    _logger.LogInformation(">>> REGISTRO: Recebido token reCAPTCHA com tamanho: {Size}", captchaToken?.Length ?? 0);

    if (string.IsNullOrEmpty(captchaToken) || !await IsReCaptchaValid(captchaToken))
    {
        _logger.LogWarning(">>> REGISTRO: Falha na verificação do reCAPTCHA para o e-mail: {Email}", email);
        ViewBag.Error = "Falha na verificação de segurança (reCAPTCHA).";
        return View();
    }

    if (password != confirmPassword)
    {
        ViewBag.Error = _localizer["PasswordsDoNotMatch"].Value; 
        return View();
    }

    var user = new ApplicationUser { UserName = email.Trim(), Email = email.Trim(), DisplayName = displayName };
    var result = await _userManager.CreateAsync(user, password);

    if (result.Succeeded)
    {
        string roleToAssign = string.Equals(userType, "Recruiter", StringComparison.OrdinalIgnoreCase) 
            ? "Recruiter" 
            : "Candidate";

        await _userManager.AddToRoleAsync(user, roleToAssign);

        // Disparo do E-mail de Confirmação com Template Responsivo
        try 
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action("ConfirmEmail", "Account", 
                new { userId = user.Id, token = token, culture = currentCulture }, Request.Scheme);

            string subject = _localizer["EmailConfirmationSubject"];
            string body = $@"
            <!DOCTYPE html>
            <html lang='{currentCulture}'>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            </head>
            <body style='margin: 0; padding: 0; background-color: #f4f6f9; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;'>
                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='background-color: #f4f6f9; padding: 40px 10px;'>
                    <tr>
                        <td align='center'>
                            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='max-width: 520px; background-color: #ffffff; border-radius: 12px; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05); overflow: hidden;'>
                                <tr>
                                    <td style='padding: 32px 32px 24px 32px; text-align: center; border-bottom: 1px solid #f0f0f0;'>
                                        <h1 style='margin: 0; font-size: 24px; font-weight: 700; color: #111827;'>MasterStack</h1>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding: 32px;'>
                                        <h2 style='margin: 0 0 16px 0; font-size: 18px; font-weight: 600; color: #1f2937;'>{_localizer["EmailGreeting"]} {displayName},</h2>
                                        <p style='margin: 0 0 24px 0; font-size: 14px; line-height: 1.6; color: #4b5563;'>
                                            {_localizer["EmailInstruction"]}
                                        </p>
                                        <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
                                            <tr>
                                                <td align='center' style='padding: 8px 0 24px 0;'>
                                                    <a href='{confirmationLink}' target='_blank' style='display: inline-block; background-color: #2563eb; color: #ffffff; font-size: 14px; font-weight: 600; text-decoration: none; padding: 12px 28px; border-radius: 8px;'>
                                                        {_localizer["ConfirmLinkText"]}
                                                    </a>
                                                </td>
                                            </tr>
                                        </table>
                                        <hr style='border: none; border-top: 1px solid #f0f0f0; margin: 24px 0 16px 0;' />
                                        <p style='margin: 0; font-size: 12px; color: #9ca3af; word-break: break-all;'>
                                            <a href='{confirmationLink}' style='color: #2563eb; text-decoration: underline;'>{confirmationLink}</a>
                                        </p>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding: 24px 32px; background-color: #f9fafb; text-align: center; border-top: 1px solid #f0f0f0;'>
                                        <p style='margin: 0; font-size: 12px; color: #9ca3af;'>
                                            © {DateTime.UtcNow.Year} MasterStack Jobs. {_localizer["EmailFooter"]}
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>";

            await _emailSender.SendEmailAsync(user.Email!, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERRO AO ENVIAR E-MAIL DE REGISTRO]");
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        if (roleToAssign == "Recruiter")
        {
            return RedirectToAction("Index", "Recruiter", new { culture = currentCulture });
        }

        return RedirectToAction("Index", "Home", new { culture = currentCulture });
    }

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

 // ✅ Sem atributo [Route] ou [HttpGet("Logout")]. 
    // Usamos apenas [HttpGet, HttpPost] para aceitar os dois verbos na rota padrão "/Account/Logout".
    [HttpGet]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout(string culture)
    {
        await _signInManager.SignOutAsync();
        
        var currentCulture = !string.IsNullOrEmpty(culture) 
            ? culture 
            : RouteData.Values["culture"]?.ToString() ?? "pt-BR";

        return RedirectToAction("Login", "Account", new { culture = currentCulture });
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

        // 1. Abre a página para digitar o e-mail
        [HttpGet("ForgotPassword")]
        public IActionResult ForgotPassword(string culture)
        {
            return View();
        }

        [HttpPost("ForgotPassword")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ForgotPassword(string email, string culture)
{
    var currentCulture = culture ?? "pt-BR";
    var cleanEmail = email?.Trim().ToLower();

    // 🔍 1. DIAGNÓSTICO: Listar os últimos usuários cadastrados no banco
    var totalUsers = _context.Users.Count();
    var registeredEmails = _context.Users.Select(u => u.Email).Take(10).ToList();
    
    _logger.LogInformation("==================================================");
    _logger.LogInformation(">>> DIAGNÓSTICO DE BANCO:");
    _logger.LogInformation(">>> Total de Usuários no Banco: {Total}", totalUsers);
    _logger.LogInformation(">>> E-mails cadastrados: {Emails}", string.Join(", ", registeredEmails));
    _logger.LogInformation(">>> E-mail recebido no formulário: '{Email}'", cleanEmail);
    _logger.LogInformation("==================================================");

    // 2. Busca o usuário
    var user = await _userManager.FindByEmailAsync(cleanEmail);
    if (user == null && !string.IsNullOrEmpty(cleanEmail))
    {
        user = _context.Users.FirstOrDefault(u => u.NormalizedEmail == cleanEmail.ToUpper());
    }

    if (user == null)
    {
        _logger.LogWarning(">>> BUSCA: Usuário '{Email}' NÃO encontrado!", cleanEmail);
        return RedirectToAction("ForgotPasswordConfirmation", new { culture = currentCulture });
    }

    _logger.LogInformation(">>> USUÁRIO ENCONTRADO (ID: {Id}). Disparando AWS SES...", user.Id);

    try
    {
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var callbackUrl = Url.Action("ResetPassword", "Account", 
            new { token = token, email = user.Email, culture = currentCulture }, Request.Scheme);

        string subject = _localizer["ResetPasswordSubject"];
        // 🎨 Template HTML Moderno e Responsivo
        string body = $@"
        <!DOCTYPE html>
        <html lang='{currentCulture}'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <title>{subject}</title>
        </head>
        <body style='margin: 0; padding: 0; background-color: #f4f6f9; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;'>
            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='background-color: #f4f6f9; padding: 40px 10px;'>
                <tr>
                    <td align='center'>
                        <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='max-width: 520px; background-color: #ffffff; border-radius: 12px; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05); overflow: hidden;'>
                            
                            <!-- Header -->
                            <tr>
                                <td style='padding: 32px 32px 24px 32px; text-align: center; border-bottom: 1px solid #f0f0f0;'>
                                    <h1 style='margin: 0; font-size: 24px; font-weight: 700; color: #111827; letter-spacing: -0.5px;'>MasterStack</h1>
                                </td>
                            </tr>

                            <!-- Body Content -->
                            <tr>
                                <td style='padding: 32px;'>
                                    <h2 style='margin: 0 0 16px 0; font-size: 18px; font-weight: 600; color: #1f2937;'>Recuperação de Senha</h2>
                                    <p style='margin: 0 0 24px 0; font-size: 14px; line-height: 1.6; color: #4b5563;'>
                                        Recebemos uma solicitação para redefinir a senha da sua conta na <strong>MasterStack</strong>. Clique no botão abaixo para prosseguir:
                                    </p>
                                    
                                    <!-- Botão CTA -->
                                    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
                                        <tr>
                                            <td align='center' style='padding: 8px 0 24px 0;'>
                                                <a href='{callbackUrl}' target='_blank' style='display: inline-block; background-color: #2563eb; color: #ffffff; font-size: 14px; font-weight: 600; text-decoration: none; padding: 12px 28px; border-radius: 8px; box-shadow: 0 2px 4px rgba(37, 99, 235, 0.2);'>
                                                    Redefinir Minha Senha
                                                </a>
                                            </td>
                                        </tr>
                                    </table>

                                    <p style='margin: 0 0 16px 0; font-size: 13px; line-height: 1.5; color: #6b7280;'>
                                        Se você não solicitou essa alteração, nenhuma ação é necessária e sua senha permanecerá a mesma.
                                    </p>

                                    <!-- Link fallback caso o botão não funcione -->
                                    <hr style='border: none; border-top: 1px solid #f0f0f0; margin: 24px 0 16px 0;' />
                                    <p style='margin: 0; font-size: 12px; line-height: 1.4; color: #9ca3af; word-break: break-all;'>
                                        Caso o botão acima não funcione, copie e cole o link a seguir no seu navegador:<br />
                                        <a href='{callbackUrl}' style='color: #2563eb; text-decoration: underline;'>{callbackUrl}</a>
                                    </p>
                                </td>
                            </tr>

                            <!-- Footer -->
                            <tr>
                                <td style='padding: 24px 32px; background-color: #f9fafb; text-align: center; border-top: 1px solid #f0f0f0;'>
                                    <p style='margin: 0; font-size: 12px; color: #9ca3af;'>
                                        © {DateTime.UtcNow.Year} MasterStack Jobs. Todos os direitos reservados.
                                    </p>
                                </td>
                            </tr>

                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>";

        await _emailSender.SendEmailAsync(user.Email!, subject, body);

        _logger.LogInformation(">>> E-MAIL ENVIADO COM SUCESSO PARA: {Email}", user.Email);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, ">>> ERRO NO SMTP DA AWS: {Message}", ex.Message);
    }

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

       [HttpGet("/{culture}/Account/EnableTwoFactor")]
public async Task<IActionResult> EnableTwoFactor([FromRoute] string culture)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Challenge();

    var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
    if (string.IsNullOrEmpty(unformattedKey))
    {
        await _userManager.ResetAuthenticatorKeyAsync(user);
        unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
    }

    string appName = "MasterStack";
    string encodedEmail = UrlEncoder.Default.Encode(user.Email ?? "");
    string authenticatorUri = $"otpauth://totp/{UrlEncoder.Default.Encode(appName)}:{encodedEmail}?secret={unformattedKey}&issuer={appName}&digits=6";

    var model = new EnableTwoFactorViewModel
    {
        SharedKey = unformattedKey!,
        AuthenticatorUri = authenticatorUri
    };

    ViewData["CurrentCulture"] = string.IsNullOrEmpty(culture) ? "pt-BR" : culture;
    return View(model);
}

[HttpPost("/{culture}/Account/EnableTwoFactor")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EnableTwoFactor(EnableTwoFactorViewModel model, [FromRoute] string culture)
{
    string currentCulture = string.IsNullOrEmpty(culture) ? "pt-BR" : culture;
    
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Challenge();

    // Se o formulário veio inválido do HTML, vamos descobrir o motivo aqui
    if (!ModelState.IsValid) 
    {
        var erros = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        return Content($"Erro de validação do formulário: {erros}");
    }

    var cleanCode = model.VerificationCode.Replace(" ", "").Replace("-", "");

    // Valida o token contra o banco
    var isValid = await _userManager.VerifyTwoFactorTokenAsync(
        user, 
        _userManager.Options.Tokens.AuthenticatorTokenProvider, 
        cleanCode
    );

    if (!isValid)
    {
        // SE O CÓDIGO FOR REJEITADO, VAI PARAR AQUI:
        return Content("O ASP.NET Identity rejeitou o código do seu celular. Motivos: Relógio do computador/celular dessincronizado ou a chave SharedKey mudou entre o GET e o POST.");
    }

    var result = await _userManager.SetTwoFactorEnabledAsync(user, true);
    
    if (result.Succeeded)
{
    await _context.SaveChangesAsync();
    await _signInManager.RefreshSignInAsync(user);
    
    TempData["Success"] = "Autenticação em dois fatores ativada com sucesso!";

    // 💡 Redirecionamento Inteligente baseado na Role do Usuário:
    if (await _userManager.IsInRoleAsync(user, "Admin") || await _userManager.IsInRoleAsync(user, "Author"))
    {
        // Se for Admin ou Autor, manda para o Dashboard do Admin
        return RedirectToAction("Dashboard", "Admin", new { culture = currentCulture });
    }

    // Se for um usuário comum, manda de volta para o Perfil dele
    return RedirectToAction("Profile", "Account", new { culture = currentCulture });
}

    return Content($"Erro do Identity ao salvar: {result.Errors.FirstOrDefault()?.Description}");
}

[HttpPost]
[Route("[action]")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> VerifyTwoFactor(string verificationCode)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Challenge();

    var cleanCode = verificationCode.Replace(" ", "").Replace("-", "");

    var isValid = await _userManager.VerifyTwoFactorTokenAsync(
        user, 
        _userManager.Options.Tokens.AuthenticatorTokenProvider, 
        cleanCode
    );

    if (!isValid)
    {
        ModelState.AddModelError(string.Empty, "Código inválido.");
        return View(); // Retorna a view mostrando o erro
    }

    // 🔥 ISSO AQUI PRECISA RODAR E SUBIR PRO BANCO:
    var result = await _userManager.SetTwoFactorEnabledAsync(user, true);
    
    if (result.Succeeded)
    {
        // Força a atualização dos cookies do usuário logado para reconhecer o 2FA ativo
        await _signInManager.RefreshSignInAsync(user);
        return RedirectToAction("Dashboard", "Admin");
    }

    ModelState.AddModelError(string.Empty, "Erro ao ativar o 2FA no banco de dados.");
    return View();
}

    [HttpGet("AccessDenied")]
    [HttpGet("/AccessDenied")]
    public IActionResult AccessDenied()
    {
        TempData["WarningMessage"] = "Seu perfil de usuário não tem permissão para realizar candidaturas.";
        return RedirectToAction("Index", "Home");
    }
    }
}