using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using MasterStack.Models;
using MasterStack.Data;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Identity.UI.Services;
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Http;          
using Microsoft.AspNetCore.Hosting;      
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

        // ==========================================
        // Helper para tradução com Fallback seguro em E-mails
        // ==========================================
        private string GetLocalizedString(string key, string cultureStr, string frText, string ptText, string enText)
        {
            var localized = _localizer[key];
            if (!localized.ResourceNotFound && localized.Value != key)
            {
                return localized.Value;
            }

            return cultureStr.StartsWith("fr", StringComparison.OrdinalIgnoreCase) 
                ? frText 
                : (cultureStr.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? enText : ptText);
        }

        [HttpGet]
        [HttpGet("Login")]
        [HttpGet("/Account/Login")]
        public IActionResult Login(string culture, string returnUrl = null)
        {
            ViewBag.CurrentCulture = culture ?? RouteData.Values["culture"] ?? "fr-CA";
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

            if (!user.EmailConfirmed)
            {
                _logger.LogInformation(">>> EMAIL NÃO CONFIRMADO. Atualizando EmailConfirmed=true para {Email}", user.Email);
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

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

        [HttpGet("LoginWith2FA")]
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

        [HttpPost("LoginWith2FA")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginWith2FA(LoginWith2FAViewModel model, [FromRoute] string culture, string returnUrl = null)
        {
            string currentCulture = string.IsNullOrEmpty(culture) ? (string)RouteData.Values["culture"] ?? "pt-BR" : culture;

            var cultureInfo = new CultureInfo(currentCulture);
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;

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

            // 🌐 Tradução dinâmica da mensagem de código inválido
            ModelState.AddModelError(string.Empty, _localizer["Invalid2FACode"].Value);
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["CurrentCulture"] = currentCulture;
            return View(model);
        }

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

            var code = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");

            string subject = _localizer["2FAEmailSubject"];
            string message = $"{_localizer["EmailGreeting"]} {user.DisplayName},<br/><br/>{_localizer["2FAEmailBody"]} <strong>{code}</strong>";

            try
            {
                await _emailSender.SendEmailAsync(user.Email!, subject, message);
                TempData["SuccessMessage"] = _localizer["2FAEmailSentSuccess"].Value;
            }
            catch
            {
                TempData["ErrorMessage"] = _localizer["2FAEmailSentError"].Value;
            }

            return RedirectToAction(nameof(LoginWith2FA), new { culture = currentCulture, returnUrl });
        }

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

            if (string.IsNullOrEmpty(captchaToken) || !await IsReCaptchaValid(captchaToken))
            {
                ViewBag.Error = _localizer["RecaptchaError"].Value;
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

                try 
                {
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var confirmationLink = Url.Action("ConfirmEmail", "Account", 
                        new { userId = user.Id, token = token, culture = currentCulture }, Request.Scheme);

                    var cultureInfo = new CultureInfo(currentCulture);
                    CultureInfo.CurrentCulture = cultureInfo;
                    CultureInfo.CurrentUICulture = cultureInfo;

                    string subject = GetLocalizedString("EmailConfirmationSubject", currentCulture, "Confirmez votre courriel - MasterStack", "Confirme seu e-mail - MasterStack", "Confirm your email - MasterStack");
                    string greeting = GetLocalizedString("EmailGreeting", currentCulture, "Bonjour", "Olá", "Hello");
                    string instruction = GetLocalizedString("EmailInstruction", currentCulture, "Merci de vous être inscrit. Veuillez confirmer votre courriel en cliquant sur le bouton ci-dessous :", "Obrigado por se cadastrar. Por favor, confirme seu e-mail clicando no botão abaixo:", "Thank you for registering. Please confirm your email by clicking the button below:");
                    string confirmText = GetLocalizedString("ConfirmLinkText", currentCulture, "Confirmer mon courriel", "Confirmar Meu E-mail", "Confirm My Email");
                    string footerText = GetLocalizedString("EmailFooter", currentCulture, "Tous droits réservés.", "Todos os direitos reservados.", "All rights reserved.");

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
                            <h2 style='margin: 0 0 16px 0; font-size: 18px; font-weight: 600; color: #1f2937;'>{greeting} {displayName},</h2>
                            <p style='margin: 0 0 24px 0; font-size: 14px; line-height: 1.6; color: #4b5563;'>
                                {instruction}
                            </p>
                            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
                                <tr>
                                    <td align='center' style='padding: 8px 0 24px 0;'>
                                        <a href='{confirmationLink}' target='_blank' style='display: inline-block; background-color: #2563eb; color: #ffffff; font-size: 14px; font-weight: 600; text-decoration: none; padding: 12px 28px; border-radius: 8px;'>
                                            {confirmText}
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
                                © {DateTime.UtcNow.Year} MasterStack Jobs. {footerText}
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

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                return View("ConfirmEmailSuccess"); 
            }

            ViewBag.Error = _localizer["ConfirmEmailTokenExpired"].Value;
            return View("Error");
        }

        [HttpGet("ForgotPassword")]
        public IActionResult ForgotPassword(string culture)
        {
            return View();
        }

        [HttpPost("ForgotPassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email, string culture)
        {
            var currentCulture = culture ?? (string)RouteData.Values["culture"] ?? "pt-BR";
            var cleanEmail = email?.Trim().ToLower();

            var user = await _userManager.FindByEmailAsync(cleanEmail);
            if (user == null && !string.IsNullOrEmpty(cleanEmail))
            {
                user = _context.Users.FirstOrDefault(u => u.NormalizedEmail == cleanEmail.ToUpper());
            }

            if (user == null)
            {
                return RedirectToAction("ForgotPasswordConfirmation", new { culture = currentCulture });
            }

            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.Action("ResetPassword", "Account", 
                    new { token = token, email = user.Email, culture = currentCulture }, Request.Scheme);

                var cultureInfo = new CultureInfo(currentCulture);
                CultureInfo.CurrentCulture = cultureInfo;
                CultureInfo.CurrentUICulture = cultureInfo;

                string subject = GetLocalizedString("ResetPasswordSubject", currentCulture, "Réinitialiser le mot de passe", "Redefinição de senha", "Reset Password");
                string title = GetLocalizedString("ResetPasswordTitle", currentCulture, "Réinitialiser le mot de passe", "Recuperação de Senha", "Reset Password");
                string bodyText = GetLocalizedString("ResetPasswordBodyText", currentCulture, 
                    "Nous avons reçu une demande de réinitialisation du mot de passe de votre compte MasterStack. Cliquez sur le bouton ci-dessous pour continuer :", 
                    "Recebemos uma solicitação para redefinir a senha da sua conta na MasterStack. Clique no botão abaixo para prosseguir:", 
                    "We received a request to reset your password for your MasterStack account. Click the button below to proceed:");

                string buttonText = GetLocalizedString("ResetPasswordButtonText", currentCulture, "Réinitialiser mon mot de passe", "Redefinir Minha Senha", "Reset My Password");
                string warningText = GetLocalizedString("ResetPasswordWarningText", currentCulture, 
                    "Si vous n'avez pas demandé cette modification, aucune action n'est requise et votre mot de passe restera le même.", 
                    "Se você não solicitou essa alteração, nenhuma ação é necessária e sua senha permanecerá a mesma.", 
                    "If you did not request this change, no action is required and your password will remain the same.");

                string linkText = GetLocalizedString("ResetPasswordLinkFallback", currentCulture, 
                    "Si le bouton ci-dessus ne fonctionne pas, copiez et collez le lien suivant dans votre navigateur :", 
                    "Caso o botão acima não funcione, copie e cole o link a seguir no seu navegador:", 
                    "If the button above does not work, copy and paste the following link into your browser:");

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
                    <tr>
                        <td style='padding: 32px 32px 24px 32px; text-align: center; border-bottom: 1px solid #f0f0f0;'>
                            <h1 style='margin: 0; font-size: 24px; font-weight: 700; color: #111827;'>MasterStack</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding: 32px;'>
                            <h2 style='margin: 0 0 16px 0; font-size: 18px; font-weight: 600; color: #1f2937;'>{title}</h2>
                            <p style='margin: 0 0 24px 0; font-size: 14px; line-height: 1.6; color: #4b5563;'>
                                {bodyText}
                            </p>
                            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
                                <tr>
                                    <td align='center' style='padding: 8px 0 24px 0;'>
                                        <a href='{callbackUrl}' target='_blank' style='display: inline-block; background-color: #2563eb; color: #ffffff; font-size: 14px; font-weight: 600; text-decoration: none; padding: 12px 28px; border-radius: 8px;'>
                                            {buttonText}
                                        </a>
                                    </td>
                                </tr>
                            </table>
                            <p style='margin: 0 0 16px 0; font-size: 13px; line-height: 1.5; color: #6b7280;'>
                                {warningText}
                            </p>
                            <hr style='border: none; border-top: 1px solid #f0f0f0; margin: 24px 0 16px 0;' />
                            <p style='margin: 0; font-size: 12px; color: #9ca3af; word-break: break-all;'>
                                {linkText}<br />
                                <a href='{callbackUrl}' style='color: #2563eb; text-decoration: underline;'>{callbackUrl}</a>
                            </p>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding: 24px 32px; background-color: #f9fafb; text-align: center; border-top: 1px solid #f0f0f0;'>
                            <p style='margin: 0; font-size: 12px; color: #9ca3af;'>
                                © {DateTime.UtcNow.Year} MasterStack Jobs.
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

        [HttpGet("ResetPassword")]
        public IActionResult ResetPassword(string token, string email, string culture)
        {
            if (token == null || email == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var model = new ResetPasswordViewModel { Token = token, Email = email };
            return View(model);
        }

        [HttpPost("ResetPassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var culture = (string)RouteData.Values["culture"] ?? "pt-BR";

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) 
            {
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
                return jsonResponse.Contains("\"success\": true") && 
                       !jsonResponse.Contains("\"score\": 0.0") && 
                       !jsonResponse.Contains("\"score\": 0.1");
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

            if (!ModelState.IsValid) 
            {
                var erros = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Content($"Erro de validação do formulário: {erros}");
            }

            var cleanCode = model.VerificationCode.Replace(" ", "").Replace("-", "");

            var isValid = await _userManager.VerifyTwoFactorTokenAsync(
                user, 
                _userManager.Options.Tokens.AuthenticatorTokenProvider, 
                cleanCode
            );

            if (!isValid)
            {
                return Content("O ASP.NET Identity rejeitou o código do seu celular. Motivos: Relógio do computador/celular dessincronizado ou a chave SharedKey mudou entre o GET e o POST.");
            }

            var result = await _userManager.SetTwoFactorEnabledAsync(user, true);
            if (result.Succeeded)
            {
                await _context.SaveChangesAsync();
                await _signInManager.RefreshSignInAsync(user);
                
                TempData["Success"] = _localizer["2FAEnabledSuccess"].Value;

                if (await _userManager.IsInRoleAsync(user, "Admin") || await _userManager.IsInRoleAsync(user, "Author"))
                {
                    return RedirectToAction("Dashboard", "Admin", new { culture = currentCulture });
                }

                return RedirectToAction("Profile", "User", new { culture = currentCulture });
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
                ModelState.AddModelError(string.Empty, _localizer["Invalid2FACode"].Value);
                return View();
            }

            var result = await _userManager.SetTwoFactorEnabledAsync(user, true);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                return RedirectToAction("Dashboard", "Admin");
            }

            ModelState.AddModelError(string.Empty, "Erro ao ativar o 2FA no banco de dados.");
            return View();
        }

        [HttpGet("AccessDenied")]
        [HttpGet("/AccessDenied")]
        [HttpGet("{culture}/Account/AccessDenied")]
        public IActionResult AccessDenied(string culture)
        {
            var currentCulture = culture ?? (string)RouteData.Values["culture"] ?? "pt-BR";
            TempData["WarningMessage"] = _localizer["AccessDeniedMessage"].Value;
            return RedirectToAction("Index", "Home", new { culture = currentCulture });
        }
    }
}