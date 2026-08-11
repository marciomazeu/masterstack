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
        
        // Ajustes de Segurança de Cookie
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

    // Limpa os provedores padrão para garantir a prioridade correta
    options.RequestCultureProviders.Clear();

    // 1º Rota (A maior prioridade: deve estar na posição 0)
    options.RequestCultureProviders.Add(new RouteDataRequestCultureProvider());

    // 2º Cookie
    options.RequestCultureProviders.Add(new CookieRequestCultureProvider());

    // 3º QueryString (?culture=fr-CA)
    options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());

    // 4º Header Accept-Language do navegador
    options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
});

    // --- 4. MVC E RAZOR ---
    builder.Services.AddControllersWithViews(options => {
        options.Filters.Add(typeof(CultureFilter));
        // Exige o Anti-Forgery Token automaticamente em requisições de alteração (POST, PUT, DELETE)
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

    builder.Services.AddRazorPages();

    // --- 5. SERVIÇOS EXTRAS E INJEÇÃO DE DEPENDÊNCIA ---
    builder.Services.AddScoped<GeminiAiService>();
    builder.Services.AddScoped<ILocationService, LocationService>();
    builder.Services.AddTransient<IEmailSender, EmailSender>();
    builder.Services.AddScoped<ResumeParserService>();

    builder.Services.AddResponseCompression(options => {
        options.EnableForHttps = true;
    });
    builder.Services.Configure<CookiePolicyOptions>(options =>
    {
        // Exige consentimento explícito para cookies não essenciais
        options.CheckConsentNeeded = context => context != null;
        options.MinimumSameSitePolicy = SameSiteMode.Lax;
    });

    builder.Services.AddHttpClient<IGeocodingService, GeocodingService>();
    builder.Services.AddHttpClient<IJobProvider, JSearchJobProvider>();
    builder.Services.AddHostedService<AffiliateExpirationService>();
    builder.Services.AddScoped<IAffiliateRenderService, AffiliateRenderService>();
    // Registra o serviço agregador
    builder.Services.AddScoped<JobAggregatorService>();

    var app = builder.Build();

    // --- 6. PIPELINE DE EXECUÇÃO ---

    // Registra métricas de requisicões HTTP nos logs
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

    // Security Headers para Produção
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        await next();
    });

    // Aplica a localização configurada
    var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
    app.UseRequestLocalization(localizationOptions);

    // Middleware para garantir que acessos na raiz "/" recebam a cultura padrão
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value;

        // Se acessar a raiz sem nada, redireciona para a cultura padrão (fr-CA)
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

    // 1. O Roteamento DEVE vir primeiro para reconhecer os segmentos da URL ({culture})
    app.UseRouting();

    // 2. A Localização DEVE vir logo após o UseRouting
    var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
    app.UseRequestLocalization(locOptions.Value);

    // 3. O Tratador de Status 404 DEVE vir após o Roteamento e Localização estarem ativos
    app.UseStatusCodePagesWithReExecute("/Home/NotFound/{0}");
// app.UseStatusCodePages(async statusCodeContext =>
// {
//     var response = statusCodeContext.HttpContext.Response;
//     if (response.StatusCode == 404)
//     {
//         var request = statusCodeContext.HttpContext.Request;
//         var path = request.Path.Value ?? "";
//         var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

//         string culture = "fr-CA"; // Cultura padrão de fallback
//         var supported = new[] { "pt-BR", "en-US", "fr-CA" };

//         if (segments.Length > 0 && supported.Contains(segments[0]))
//         {
//             culture = segments[0];
//         }

//         // Modifica o path e a query diretamente no request context da reexecução
//         request.Path = $"/Home/NotFound/{response.StatusCode}";
//         request.QueryString = new QueryString($"?culture={culture}");

//         // Reexecuta o pipeline chamando o middleware seguinte do tratador
//         await statusCodeContext.Next(statusCodeContext.HttpContext);
//     }
// });

    app.UseAuthentication();
    app.UseAuthorization();

    // Mapeia o logout via GET para deslogar o usuário e redirecionar para a Login
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
    // Ignora a exceção disparada pelas ferramentas do Entity Framework / dotnet-ef
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

    // 3. Seed Apenas do Admin Principal (Com senha inicial forte)
    string adminEmail = "seu-email-real@dominio.com"; // <-- COLOQUE SEU EMAIL REAL AQUI
    
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var adminUser = new ApplicationUser 
        { 
            UserName = adminEmail, 
            Email = adminEmail, 
            DisplayName = "Admin MasterStack", 
            EmailConfirmed = true 
        };
        
        // Use uma senha temporária forte antes de subir o código
        var result = await userManager.CreateAsync(adminUser, "SenhaProvisoria#2026!Secured");
        if (result.Succeeded) 
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}