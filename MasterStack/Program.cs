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
using Microsoft.AspNetCore.Mvc;
using Serilog;
using MasterStack.Services.JobProviders;
using MasterStack.Services.Providers;

// --- CONFIGURAÇÃO INICIAL DO LOGGING (SERILOG) ---
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/masterstack-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Iniciando a aplicação MasterStack...");

    var builder = WebApplication.CreateBuilder(args);

    // Substitui o provedor padrão de log pelo Serilog
    builder.Host.UseSerilog();

    // --- 1. BANCO DE DADOS ---
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // --- 2. IDENTITY & COOKIES SECURITY CONFIG ---
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
        options.SignIn.RequireConfirmedAccount = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(options => {
        options.LoginPath = "/Account"; 
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
        options.SlidingExpiration = true;
    });

    // --- 3. LOCALIZAÇÃO (CONFIGURAÇÃO) ---
    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

    var supportedCultures = new[] {
        new CultureInfo("pt-BR"),
        new CultureInfo("en-US"),
        new CultureInfo("fr-CA")
    };

    builder.Services.Configure<RequestLocalizationOptions>(options => {
        options.DefaultRequestCulture = new RequestCulture("fr-CA");
        options.SupportedCultures = supportedCultures;
        options.SupportedUICultures = supportedCultures;

        options.RequestCultureProviders.Clear();
        options.RequestCultureProviders.Add(new RouteDataRequestCultureProvider());
        options.RequestCultureProviders.Add(new CookieRequestCultureProvider());
        options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
        options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
    });

    // --- 4. MVC E RAZOR ---
    builder.Services.AddControllersWithViews(options => {
        options.Filters.Add(typeof(CultureFilter));
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

    builder.Services.AddRazorPages();

    // --- 5. SERVIÇOS EXTRAS E INJEÇÃO DE DEPENDÊNCIA ---
    builder.Services.AddMemoryCache(); // CORREÇÃO: Registrado no container DI ANTES do builder.Build()

    builder.Services.AddScoped<GeminiAiService>();
    builder.Services.AddScoped<ILocationService, LocationService>();
    builder.Services.AddTransient<IEmailSender, EmailSender>();
    builder.Services.AddScoped<ResumeParserService>();

    builder.Services.AddResponseCompression(options => {
        options.EnableForHttps = true;
    });

    builder.Services.Configure<CookiePolicyOptions>(options =>
    {
        options.CheckConsentNeeded = context => context != null;
        options.MinimumSameSitePolicy = SameSiteMode.Lax;
    });

    // --- REGISTRO CORRETO DO JSEARCHJOBPROVIDER ---
   builder.Services.AddHttpClient<JSearchJobProvider>(client =>
{
    client.BaseAddress = new Uri("https://jsearch.p.rapidapi.com/");

    var apiKey = builder.Configuration["RapidAPI:Key"];

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Log.Warning("A chave RapidAPI:Key não foi encontrada nas configurações!");
    }
    else
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("x-rapidapi-key", apiKey);
    }

    client.DefaultRequestHeaders.TryAddWithoutValidation("x-rapidapi-host", "jsearch.p.rapidapi.com");
});
    // Injeta o HttpClient tipado para o JSearchJobProvider
    builder.Services.AddHttpClient<IJobProvider, JSearchJobProvider>(client =>
    {
        client.BaseAddress = new Uri("https://jsearch.p.rapidapi.com/");
        client.Timeout = TimeSpan.FromSeconds(10);
    });
    // Registra o HttpClient para o Remotive
    builder.Services.AddHttpClient<RemotiveJobProvider>();
    // Registra como IJobProvider
    builder.Services.AddScoped<IJobProvider, RemotiveJobProvider>();
    // Registra a interface resolvendo através da classe concreta do HttpClient
    builder.Services.AddTransient<IJobProvider>(sp => sp.GetRequiredService<JSearchJobProvider>());

    builder.Services.AddHttpClient<IGeocodingService, GeocodingService>();

    builder.Services.AddHostedService<AffiliateExpirationService>();
    builder.Services.AddHostedService<JobCleanupBackgroundService>();
    builder.Services.AddScoped<IAffiliateRenderService, AffiliateRenderService>();
    builder.Services.AddScoped<JobAggregatorService>();

    // --- CONSTRUÇÃO DO APP ---
    var app = builder.Build();

    // --- 6. PIPELINE DE EXECUÇÃO ---
    app.UseSerilogRequestLogging();

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

    // Ignora chamadas automáticas de DevTools do Chrome para não poluir os logs com 404
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/.well-known"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });

    // Security Headers para Produção
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        await next();
    });

    var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
    app.UseRequestLocalization(localizationOptions);

    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value;

        if (string.IsNullOrEmpty(path) || path == "/")
        {
            context.Response.Redirect("/fr-CA");
            return;
        }

        await next();
    });

    app.UseHttpsRedirection();
    app.UseResponseCompression();
    app.UseStaticFiles();
    app.UseCookiePolicy();

    app.UseRouting();

    var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
    app.UseRequestLocalization(locOptions.Value);

    app.UseStatusCodePagesWithReExecute("/Home/NotFound/{0}");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGet("/{culture}/Account/Logout", async (string culture, SignInManager<ApplicationUser> signInManager) =>
    {
        await signInManager.SignOutAsync();
        return Results.Redirect($"/{culture}/Account/Login");
    });

    app.MapGet("/Account/Logout", async (SignInManager<ApplicationUser> signInManager) =>
    {
        await signInManager.SignOutAsync();
        return Results.Redirect("/pt-BR/Account/Login");
    });

    // --- 7. ROTAS ---
    app.MapControllerRoute(
        name: "culture-route",
        pattern: "{culture}/{controller=Home}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.MapRazorPages();

    // --- 8. SEED DATA E MIGRATIONS ---
    if (!EF.IsDesignTime)
    {
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
                Log.Error(ex, "Erro na execução do seed.");
            }
        }
    }

    app.Run();
}
catch (Microsoft.Extensions.Hosting.HostAbortedException)
{
    throw;
}
catch (Exception ex)
{
    Log.Fatal(ex, "A aplicação encerrou inesperadamente.");
}
finally
{
    Log.CloseAndFlush();
}

async Task SeedLanguagesAndRoles(IServiceProvider services)
{
    var context = services.GetRequiredService<ApplicationDbContext>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

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

    string[] roles = { "Admin", "Author", "User" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    string adminEmail = "seu-email-real@dominio.com";
    
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var adminUser = new ApplicationUser 
        { 
            UserName = adminEmail, 
            Email = adminEmail, 
            DisplayName = "Admin MasterStack", 
            EmailConfirmed = true 
        };
        
        var result = await userManager.CreateAsync(adminUser, "SenhaProvisoria#2026!Secured");
        if (result.Succeeded) 
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}