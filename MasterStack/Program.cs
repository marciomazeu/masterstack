using MasterStack;
using MasterStack.Data;
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

// 3. Rota Globalizada para SEO: ex: masterstack.com/pt-BR/Home/Index
app.MapControllerRoute(
    name: "default",
    pattern: "{culture=en-US}/{controller=Home}/{action=Index}/{id?}");


app.Run();
