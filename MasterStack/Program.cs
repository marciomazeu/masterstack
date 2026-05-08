using MasterStack;
using MasterStack.Data;
using MasterStack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity.UI.Services;
using MasterStack.Services; // Seu namespace
using Npgsql.EntityFrameworkCore.PostgreSQL;

var builder = WebApplication.CreateBuilder(args);

// 1. Configura a pasta de recursos
//builder.Services.AddLocalization();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// 2. Configura o MVC para usar a classe SharedResource
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

    builder.Services.AddRazorPages();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] {
        new CultureInfo("en-US"),
        new CultureInfo("pt-BR"),
        new CultureInfo("fr-CA")
    };

    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // LIMPE os provedores padr�o e coloque a ROTA em primeiro lugar
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Insert(0, new RouteDataRequestCultureProvider());
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(typeof(CultureFilter));
});

//conexao com o banco de dados
// Substitua o UseSqlServer por este:
// builder.Services.AddDbContext<ApplicationDbContext>(options =>
// options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Substitua o UseSqlServer por UseNpgsql
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 1. Adiciona o servi�o de compress�o
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] {
            "text/html",
            "text/css",
            "application/javascript",
            "image/svg+xml",
            "application/json"
        });
});

// --- [CONFIGURAÇÃO DE COOKIE FLEXÍVEL] ---
builder.Services.ConfigureApplicationCookie(options =>
{
    // Remova o pt-BR fixo. O ASP.NET tentará achar a rota correta.
    options.LoginPath = "/Account/Login"; 
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52428800; // 50MB em bytes
});
// Configura o limite para o IIS (servidores Windows)
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 50 * 1024 * 1024;
});

// --- [REGISTRO DO EMAIL SENDER] ---
builder.Services.AddTransient<IEmailSender, EmailSender>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = true;
    options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultProvider;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

var app = builder.Build();
// 1. Em produção, usamos o Handler. Em desenvolvimento, a página técnica.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    // COMENTE esta linha abaixo para forçar a página AZUL no seu Mac
    // app.UseDeveloperExceptionPage(); 
    
    // E DESCOMENTE esta para o teste:
    app.UseExceptionHandler("/Home/Error"); 
    
}

// 2. Configura os idiomas (PT-BR, EN-CA, FR-CA)
var supportedCultures = new[] { "pt-BR", "pt", "en-US", "en", "fr-CA", "fr" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("pt-BR")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        // 1. Defina a lista de idiomas desejados
        var seedLanguages = new List<Language>
        {
            new Language { Culture = "pt-BR", Name = "Portugues", FlagClass = "fi-br", IsActive = true },
            new Language { Culture = "en-US", Name = "English", FlagClass = "fi-us", IsActive = true },
            new Language { Culture = "fr-CA", Name = "Français", FlagClass = "fi-ca", IsActive = true }
        };

        // 2. S� insere se o idioma ainda n�o existir no banco
        foreach (var lang in seedLanguages)
        {
            if (!context.Languages.Any(l => l.Culture == lang.Culture))
            {
                context.Languages.Add(lang);
            }
        }

        context.SaveChanges();
        Console.WriteLine("Sucesso: Sincroniza��o de idiomas conclu�da (sem apagar dados).");
    }
    catch (Exception ex)
    {
        Console.WriteLine("ERRO AO ACESSAR TABELA: " + ex.Message);
    }
}

// Isso for�a o sistema a olhar para a URL primeiro
localizationOptions.RequestCultureProviders.Insert(0, new RouteDataRequestCultureProvider());

// Configure the HTTP request pipeline.
// if (!app.Environment.IsDevelopment())
// {
//     app.UseExceptionHandler("/Home/Error"); // Erros de servidor (500)
//     app.UseHsts();
// }

// Captura erros de status code como o 404
// Captura o status code e redireciona mantendo a cultura ou usando padr�o
app.UseStatusCodePagesWithReExecute("/Home/NotFound/{0}");



app.UseHttpsRedirection();
//cache de imagens
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache de 30 dias para imagens, css e js
        const int durationInSeconds = 60 * 60 * 24 * 30;
        ctx.Context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] =
            "public,max-age=" + durationInSeconds;
    }
});

app.UseRequestLocalization(localizationOptions);

app.UseRouting();


app.UseAuthentication();
app.UseAuthorization();

// 1. Rota Localizada (Sempre primeiro)
// Ordem importa: a rota com cultura deve vir PRIMEIRO
app.MapControllerRoute(
    name: "culture-route",
    pattern: "{culture}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 3. Razor Pages (Identity) - Se houver conflito, os Controllers acima ganham prioridade
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // 1. Criar as Roles se não existirem
    string[] roles = { "Admin", "Author", "User" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Função auxiliar para criar utilizadores de teste
    async Task CreateTestUser(string email, string name, string role, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            var newUser = new ApplicationUser 
            { 
                UserName = email, 
                Email = email, 
                DisplayName = name,
                EmailConfirmed = true // Evita problemas de validação de email no teste
            };
            
            var result = await userManager.CreateAsync(newUser, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(newUser, role);
            }
        }
    }

    // 2. Criar os três perfis de teste
    // ATENÇÃO: Usa senhas que cumpram os requisitos do Identity (Letra grande, número e símbolo)
    await CreateTestUser("admin@masterstack.com", "Admin Geral", "Admin", "Master@123");
    await CreateTestUser("autor@masterstack.com", "Autor de Conteúdo", "Author", "Master@123");
    await CreateTestUser("leitor@masterstack.com", "Leitor Comum", "User", "Master@123");
}


app.Run();