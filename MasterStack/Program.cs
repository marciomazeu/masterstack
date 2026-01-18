using MasterStack;
using MasterStack.Data;
using MasterStack.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// 1. Configura a pasta de recursos
builder.Services.AddLocalization();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// 2. Configura o MVC para usar a classe SharedResource
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

builder.Services.AddAuthentication("MyCookieAuth")
    .AddCookie("MyCookieAuth", options =>
    {
        options.Cookie.Name = "MasterStackAdmin";
        options.Events.OnRedirectToLogin = context =>
        {
            // Tenta pegar a cultura da rota ou usa pt-BR como padrão
            var culture = context.HttpContext.Request.RouteValues["culture"] ?? "pt-BR";
            var loginUrl = $"/{culture}/Account/Login?ReturnUrl={context.Request.Path}";
            context.Response.Redirect(loginUrl);
            return Task.CompletedTask;
        };
    });

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] {
        new CultureInfo("en-US"),
        new CultureInfo("en-CA"),
        new CultureInfo("pt-BR"),
        new CultureInfo("fr-CA")
    };

    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // LIMPE os provedores padrão e coloque a ROTA em primeiro lugar
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Insert(0, new RouteDataRequestCultureProvider());
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(typeof(CultureFilter));
});

//conexao com o banco de dados
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 1. Adiciona o serviço de compressão
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

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52428800; // 50MB em bytes
});
var app = builder.Build();

// 2. Configura os idiomas (PT-BR, EN-CA, FR-CA)
var supportedCultures = new[] { "pt-BR", "pt", "en-US", "en", "fr-FR", "fr-CA", "fr" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
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
            new Language { Culture = "pt-BR", Name = "Português", FlagClass = "fi-br", IsActive = true },
            new Language { Culture = "en-US", Name = "English", FlagClass = "fi-us", IsActive = true },
            new Language { Culture = "fr-CA", Name = "Français", FlagClass = "fi-ca", IsActive = true }
        };

        // 2. Só insere se o idioma ainda não existir no banco
        foreach (var lang in seedLanguages)
        {
            if (!context.Languages.Any(l => l.Culture == lang.Culture))
            {
                context.Languages.Add(lang);
            }
        }

        context.SaveChanges();
        Console.WriteLine("Sucesso: Sincronização de idiomas concluída (sem apagar dados).");
    }
    catch (Exception ex)
    {
        Console.WriteLine("ERRO AO ACESSAR TABELA: " + ex.Message);
    }
}

// Isso força o sistema a olhar para a URL primeiro
localizationOptions.RequestCultureProviders.Insert(0, new RouteDataRequestCultureProvider());

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();
//app.UseResponseCompression();
app.UseStaticFiles();

// 1. PRIMEIRO: Localiza para onde a requisição vai
app.UseRouting();

// 2. SEGUNDO: Define o idioma baseado na rota localizada
app.UseRequestLocalization(localizationOptions);

// 3. TERCEIRO: Identifica quem é o usuário (Cookie)
app.UseAuthentication();

// 4. QUARTO: Verifica se o usuário tem permissão ([Authorize])
app.UseAuthorization();

// 5. POR ÚLTIMO: Executa o Controller encontrado
app.MapControllerRoute(
    name: "sitemap",
    pattern: "sitemap.xml",
    defaults: new { controller = "BlogPosts", action = "Sitemap" });

app.MapControllerRoute(
    name: "localized",
    pattern: "{culture:regex(^[a-z]{{2}}(-[A-Z]{{2}})?$)}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
