using MasterStack;
using MasterStack.Data;
using MasterStack.Models;
using MasterStack.Services;
using MasterStack.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// --- 1. BANCO DE DADOS ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 2. IDENTITY CONFIG ---
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/Account/Login"; 
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// --- 3. LOCALIZAÇÃO (CONFIGURAÇÃO) ---
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] {
    new CultureInfo("pt-BR"),
    new CultureInfo("en-US"),
    new CultureInfo("fr-CA")
};

builder.Services.Configure<RequestLocalizationOptions>(options => {
    options.DefaultRequestCulture = new RequestCulture("pt-BR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new RouteDataRequestCultureProvider()); // 1º Rota
    options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider()); // 2º QueryString (Fallback)
    options.RequestCultureProviders.Add(new CookieRequestCultureProvider());      // 3º Cookie (Fallback)
});

// --- 4. MVC E RAZOR ---
builder.Services.AddControllersWithViews(options => {
    options.Filters.Add(typeof(CultureFilter));
})
.AddViewLocalization()
.AddDataAnnotationsLocalization();

builder.Services.AddRazorPages();

// --- 5. SERVIÇOS EXTRAS E INJEÇÃO DE DEPENDÊNCIA ---
builder.Services.AddScoped<GeminiAiService>();

// 📌 REGISTRO DO SERVIÇO DE LOCALIZAÇÃO (Resolve o erro do ILocationService na UserController)
builder.Services.AddScoped<ILocationService, LocationService>();

builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddResponseCompression(options => {
    options.EnableForHttps = true;
});
builder.Services.AddHttpClient<IGeocodingService, GeocodingService>();
builder.Services.AddHostedService<AffiliateExpirationService>();
var app = builder.Build();

// --- 6. PIPELINE DE EXECUÇÃO ---

if (app.Environment.IsDevelopment()) 
{
    app.UseDeveloperExceptionPage();
} 
else 
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();

    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | 
                           Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
    });
}

app.UseStatusCodePagesWithReExecute("/Home/NotFound/{0}");
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// LOCALIZAÇÃO (após UseRouting e antes de UseAuthorization)
var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

app.UseAuthentication();
app.UseAuthorization();

// --- 7. ROTAS ---
app.MapControllerRoute(
    name: "culture-route",
    pattern: "{culture}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// --- 8. SEED DATA E MIGRATIONS ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        
        await db.Database.MigrateAsync(); 
        await SeedLanguagesAndRoles(services);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro crítico na inicialização do banco/seed: {ex.Message}");
    }
}

app.Run();

async Task SeedLanguagesAndRoles(IServiceProvider services)
{
    var context = services.GetRequiredService<ApplicationDbContext>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    // 1. Seed de Idiomas
    var seedLanguages = new List<Language>
    {
        new Language { Culture = "pt-BR", Name = "Português", FlagClass = "fi-br", IsActive = true },
        new Language { Culture = "en-US", Name = "English", FlagClass = "fi-us", IsActive = true },
        new Language { Culture = "fr-CA", Name = "Français", FlagClass = "fi-ca", IsActive = true }
    };

    foreach (var lang in seedLanguages)
    {
        if (!context.Languages.Any(l => l.Culture == lang.Culture))
        {
            context.Languages.Add(lang);
        }
    }
    await context.SaveChangesAsync();

    // 2. Seed de Roles
    string[] roles = { "Admin", "Author", "User" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // 3. Seed de Usuários
    async Task CreateUser(string email, string name, string role)
    {
        if (await userManager.FindByEmailAsync(email) == null)
        {
            var user = new ApplicationUser { UserName = email, Email = email, DisplayName = name, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, "Master@123");
            if (result.Succeeded) await userManager.AddToRoleAsync(user, role);
        }
    }

    await CreateUser("admin@masterstack.com", "Admin Geral", "Admin");
    await CreateUser("autor@masterstack.com", "Autor", "Author");
    await CreateUser("leitor@masterstack.com", "Leitor", "User");
}