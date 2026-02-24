using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace MasterStack.Controllers
{
    [Route("{culture}/Account")]
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;

public AccountController(SignInManager<IdentityUser> signInManager)
{
    _signInManager = signInManager;
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
            // 1. Verificação básica (Realismo: use senhas fortes depois!)
            if (username == "admin" && password == "admin")
            {
                // 2. Criar as Claims (O "RG" do usuário)
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, "Admin")
        };

                // 3. Criar a Identidade (O "Cartão" que contém o RG)
                // É aqui que faltava o código que você tentou usar!
                var claimsIdentity = new ClaimsIdentity(claims, "MyCookieAuth");

                // 4. Efetuar o Login e criar o Cookie
                await HttpContext.SignInAsync("MyCookieAuth", new ClaimsPrincipal(claimsIdentity));

                // 5. Redirecionamento Inteligente
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                // Se a cultura vier nula do form, pegamos da rota ou padrão
                culture ??= (string)RouteData.Values["culture"] ?? "pt-BR";

                return RedirectToAction("Dashboard", "Admin", new { culture = culture });
            }

            ViewBag.Error = "Usuário ou senha inválidos";
            return View();
        }

   // Mude para HttpPost por segurança, mas se o seu link for um <a> simples, use HttpGet
        [HttpGet] 
        public async Task<IActionResult> Logout()
        {
           await _signInManager.SignOutAsync();

    // Tente redirecionar para a Home passando explicitamente a cultura
    // Isso evita que o roteador se perca
    return RedirectToAction("Index", "Home", new { culture = "pt-BR" });
        }
    }
}