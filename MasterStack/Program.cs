using MasterStack;
using MasterStack.Data;
using MasterStack.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.Mvc.Razor;
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
app.UseAuthentication();
app.UseAuthorization();

// A ordem das linhas abaixo é CRÍTICA:
app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization(localizationOptions); // DEVE vir antes de UseAuthorization e MapControllerRoute
app.UseAuthorization();

// 1. Rota Localizada (Captura idiomas primeiro)
app.MapControllerRoute(
    name: "localized",
    pattern: "{culture:regex(^[a-z]{{2}}(-[A-Z]{{2}})?$)}/{controller=Home}/{action=Index}/{id?}");

// 2. Rota Padrão (Fallback para quando não houver cultura na URL)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.Run();
