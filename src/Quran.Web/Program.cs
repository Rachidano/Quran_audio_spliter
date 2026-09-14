using Microsoft.Extensions.FileProviders;
using Quran.Infrastructure;
using Quran.Import;
using Quran.AudioProcessor;
using Quran.Infrastructure.Services;
using Quran.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

if (args.Length == 3 && args[0] == "--import-warsh-pilot")
{
    WarshPilotCorpus.Import(args[1], args[2]);
    Console.WriteLine($"Paquet Warsh importé et vérifié : {WarshPilotCorpus.Revision}");
    return;
}

var pilotDirectory = Path.Combine(AppContext.BaseDirectory, "App_Data", "warsh-kfqc-quranws", WarshPilotCorpus.Revision);
if (args.Length == 1 && args[0] == "--check-warsh-pilot")
{
    var corpus = new WarshPilotCorpus(pilotDirectory);
    Console.WriteLine($"Warsh : {corpus.PageNumbers.Count} pages, {corpus.PageNumbers.Sum(n => corpus.GetPage(n)!.Regions.Count)} régions vérifiées.");
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

// Isolated local review sessions do not reuse another Windows user's DPAPI keys.
if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("WarshPilot:ReviewMode"))
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
}

builder.Services.AddSingleton(System.Text.Encodings.Web.HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All));

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddPageRoute("/Mushaf/Page", "mushaf/warsh/page/{pageNumber:int?}");
    options.Conventions.AuthorizeFolder("/Admin", "RequireReviewer");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole(IdentitySeeder.RoleAdmin));
    options.AddPolicy("RequireReviewer", policy => policy.RequireRole(IdentitySeeder.RoleReviewer, IdentitySeeder.RoleAdmin));
});

builder.Services.AddControllers();
builder.Services.AddSingleton(_ => new WarshPilotCorpus(pilotDirectory));
builder.Services.AddSingleton(_ => new MarwPilotCorpus(Path.Combine(AppContext.BaseDirectory, "App_Data", "marw-newMushaf", MarwPilotCorpus.Revision)));

// Clean Architecture: Infrastructure, AudioProcessor, Import & EF Core 9
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<Quran.Infrastructure.Persistence.QuranDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAudioProcessorServices();
builder.Services.AddImportServices();

var app = builder.Build();

if (args.Length >= 3 && args[0] == "--create-admin")
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await IdentitySeeder.SeedRolesAsync(roleManager);
    var email = args[1];
    var password = args[2];
    var res = await IdentitySeeder.CreateUserAsync(userManager, email, password, IdentitySeeder.RoleAdmin);
    Console.WriteLine(res.Succeeded ? $"Admin {email} created successfully." : $"Failed to create admin: {string.Join(", ", res.Errors.Select(e => e.Description))}");
    return;
}

if (args.Contains("--replace-legacy-awjuh"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<Quran.Infrastructure.Persistence.QuranDbContext>();
    Console.WriteLine($"Obsolete awjuh removed: {await AwjuhCatalog.ReplaceLegacyAsync(db)}");
    return;
}
if (args.Contains("--migrate-passages"))
{
    using var scope = app.Services.CreateScope();
    var annotationService = scope.ServiceProvider.GetRequiredService<Quran.Application.Interfaces.IAnnotationService>();
    var report = await annotationService.MigrateMushafPassagesToAudioSegmentsAsync();
    Console.WriteLine($"Passages migration: Migrated={report.MigratedCount}, ExistingPreserved={report.ExistingPreservedCount}, Errors={report.Errors.Count}");
    foreach (var d in report.Details) Console.WriteLine($" - {d}");
    foreach (var e in report.Errors) Console.Error.WriteLine($" ! {e}");
    return;
}

if (args.Contains("--initialize-system"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<Quran.Infrastructure.Persistence.QuranDbContext>();
    await db.Database.MigrateAsync();
    await AwjuhCatalog.InitializeAsync(db);
    var corpusService = scope.ServiceProvider.GetRequiredService<Quran.Application.Interfaces.ICorpusResolutionService>();
    await corpusService.EnsureCorporaInitializedAsync();
    var annotationService = scope.ServiceProvider.GetRequiredService<Quran.Application.Interfaces.IAnnotationService>();
    await annotationService.MigrateMushafPassagesToAudioSegmentsAsync();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    await IdentitySeeder.SeedRolesAsync(roleManager);
    Console.WriteLine("Database migration, catalog bootstrap, corpus and passages initialization completed.");
    return;
}

// Seed default roles if not present
using (var scope = app.Services.CreateScope())
{
    try
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        await IdentitySeeder.SeedRolesAsync(roleManager);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not seed initial roles during startup.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Storage setup
var baseStoragePath = builder.Configuration["Storage:BasePath"];
if (string.IsNullOrWhiteSpace(baseStoragePath))
{
    var appDir = builder.Environment.ContentRootPath;
    var candidate = Path.Combine(appDir, "..", "..", "data", "audio");
    baseStoragePath = Directory.Exists(candidate)
        ? Path.GetFullPath(candidate)
        : Path.GetFullPath(Path.Combine(appDir, "data", "audio"));
}

Directory.CreateDirectory(Path.Combine(baseStoragePath, "sources"));
Directory.CreateDirectory(Path.Combine(baseStoragePath, "working"));
Directory.CreateDirectory(Path.Combine(baseStoragePath, "published"));

// Seuls les dérivés audio validés et publiés sont accessibles en direct statique
var publishedPath = Path.Combine(baseStoragePath, "published");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(publishedPath),
    RequestPath = "/audio/published",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000, immutable");
    }
});

var mushafPath = Path.Combine(Path.GetDirectoryName(baseStoragePath)!, "mushaf");
Directory.CreateDirectory(Path.Combine(mushafPath, "masters", "warsh"));
Directory.CreateDirectory(Path.Combine(mushafPath, "web", "warsh"));

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mushafPath),
    RequestPath = "/mushaf",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000, immutable");
    }
});

app.MapRazorPages();
app.MapControllers();

app.Run();

public partial class Program { }
