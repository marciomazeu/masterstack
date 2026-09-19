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

    builder.Host.UseSerilog();

    // --- 1. BANCO DE DADOS (CONFIGURAÇÃO ÚNICA E TRATADA) ---
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    if (!string.IsNullOrEmpty(connectionString) && (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://")))
    {
        var databaseUri = new Uri(connectionString);
        var userInfo = databaseUri.UserInfo.Split(':');

        var builderConn = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = databaseUri.Host,
            Port = databaseUri.Port > 0 ? databaseUri.Port : 5432,
            Username = userInfo[0],
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            Database = databaseUri.LocalPath.TrimStart('/'),
            SslMode = Npgsql.SslMode.Require,
            TrustServerCertificate = true
        };

        connectionString = builderConn.ToString();
    }

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));

    // --- 2. IDENTITY & COOKIES SECURITY CONFIG (COM PRESERVAÇÃO DE CULTURA NO REDIRECT) ---
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
        options.SignIn.RequireConfirmedAccount = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(options => {
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
        options.SlidingExpiration = true;

        // 🔥 Preserva o prefixo da cultura na URL ao redirecionar para a página de Login
        options.Events.OnRedirectToLogin = context =>
        {
            var culture = context.Request.RouteValues["culture"]?.ToString() ?? "fr-CA";
            var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
            context.Response.Redirect($"/{culture}/Account/Login?returnUrl={returnUrl}");
            return Task.CompletedTask;
        };
    });

    // --- 3. LOCALIZAÇÃO ---
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
    builder.Services.AddMemoryCache();

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

    // --- REGISTRO DOS PROVEDORES DE VAGAS ---
    builder.Services.AddHttpClient<JSearchJobProvider>(client =>
    {
        client.BaseAddress = new Uri("https://jsearch.p.rapidapi.com/");
        client.Timeout = TimeSpan.FromSeconds(10);
        
        var apiKey = builder.Configuration["RapidAPI:Key"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-rapidapi-key", apiKey);
        }
        client.DefaultRequestHeaders.TryAddWithoutValidation("x-rapidapi-host", "jsearch.p.rapidapi.com");
    });
    builder.Services.AddScoped<IJobProvider, JSearchJobProvider>(sp => sp.GetRequiredService<JSearchJobProvider>());

    builder.Services.AddHttpClient<RemotiveJobProvider>();
    builder.Services.AddScoped<IJobProvider, RemotiveJobProvider>(sp => sp.GetRequiredService<RemotiveJobProvider>());

    builder.Services.AddHttpClient<IGeocodingService, GeocodingService>();

    builder.Services.AddHostedService<AffiliateExpirationService>();
    builder.Services.AddHostedService<JobCleanupService>();
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

    // Ignora chamadas automáticas de DevTools
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

    app.UseHttpsRedirection();
    app.UseResponseCompression();

    // 💡 PASSO CRÍTICO 1: CRIAÇÃO FÍSICA DOS DIRETÓRIOS ANTES DE REGISTAR OS FICHEIROS ESTÁTICOS
    var uploadsFolder = Path.Combine(app.Environment.WebRootPath, "uploads");
    var blogUploadsFolder = Path.Combine(uploadsFolder, "blog");
    var profileUploadsFolder = Path.Combine(uploadsFolder, "profiles");

    try
    {
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
        if (!Directory.Exists(blogUploadsFolder)) Directory.CreateDirectory(blogUploadsFolder);
        if (!Directory.Exists(profileUploadsFolder)) Directory.CreateDirectory(profileUploadsFolder);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Aviso ao verificar/criar diretórios de uploads.");
    }

    // 💡 PASSO CRÍTICO 2: FICHEIROS ESTÁTICOS (REGISTO ÚNICO COM CACHE)
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
        }
    });

    app.UseCookiePolicy();

    // 💡 PASSO CRÍTICO 3: ROTEAMENTO
    app.UseRouting();

    // Redirecionamento da raiz sem idioma
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

    // Aplicação da Localização
    var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
    app.UseRequestLocalization(localizationOptions);

    // Tratamento de páginas não encontradas (Apenas após ficheiros estáticos e rotas)
    app.UseStatusCodePagesWithReExecute("/Home/NotFound/{0}");

    app.UseAuthentication();
    app.UseAuthorization();

    // Endpoints rápidos para Logout mantendo/restaurando a cultura
    app.MapGet("/{culture}/Account/Logout", async (string culture, SignInManager<ApplicationUser> signInManager) =>
    {
        await signInManager.SignOutAsync();
        return Results.Redirect($"/{culture}/Account/Login");
    });

    app.MapGet("/Account/Logout", async (SignInManager<ApplicationUser> signInManager) =>
    {
        await signInManager.SignOutAsync();
        return Results.Redirect("/fr-CA/Account/Login");
    });

    // --- 7. ROTAS MAPPING ---
    app.MapControllerRoute(
        name: "culture-route",
        pattern: "{culture}/{controller=Home}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.MapRazorPages();

    // --- 8. SEED DATA & MIGRATIONS ---
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync(); 
            
            await SeedData.SeedLanguagesAndRolesAsync(services);

            // Garantia de Roles Admin e Author
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            if (!await roleManager.RoleExistsAsync("Author"))
            {
                await roleManager.CreateAsync(new IdentityRole("Author"));
            }

            var adminEmail = "marciomazeu@hotmail.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser != null && !await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro no Seed/Migração durante a inicialização.");
        }
    }

    Log.Information("Iniciando escuta de requisições via app.Run()...");
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