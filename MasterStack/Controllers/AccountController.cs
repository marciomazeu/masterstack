using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace MasterStack.Controllers
{
    [Route("{culture}/Account")]
    public class AccountController : Controller
    {
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

        [HttpGet("Logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("MyCookieAuth");

            // Pega a cultura atual para redirecionar para a Home no idioma certo
            var culture = RouteData.Values["culture"] ?? "pt-BR";
            return RedirectToAction("Index", "Home", new { culture = culture });
        }
    }
}