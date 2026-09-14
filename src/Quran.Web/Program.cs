using Microsoft.Extensions.FileProviders;
using Quran.Infrastructure;
using Quran.Import;
using Quran.AudioProcessor;
using Quran.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

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

// Isolated local review sessions do not reuse another Windows user's DPAPI keys.
if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("WarshPilot:ReviewMode"))
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
}

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddPageRoute("/Mushaf/Page", "mushaf/warsh/page/{pageNumber:int?}");
    // /Mushaf/Page/{pageNumber?} already comes from the Razor page's @page directive.
    // Routing is case-insensitive, so a lowercase alias would duplicate that endpoint.
});
builder.Services.AddControllers();
builder.Services.AddSingleton(_ => new WarshPilotCorpus(pilotDirectory));
builder.Services.AddSingleton(_ => new MarwPilotCorpus(Path.Combine(AppContext.BaseDirectory, "App_Data", "marw-newMushaf", MarwPilotCorpus.Revision)));

// Clean Architecture: Infrastructure, AudioProcessor, Import & EF Core 9
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAudioProcessorServices();
builder.Services.AddImportServices();

var app = builder.Build();

if (args.Contains("--replace-legacy-awjuh"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<Quran.Infrastructure.Persistence.QuranDbContext>();
    Console.WriteLine($"Obsolete awjuh removed: {await AwjuhCatalog.ReplaceLegacyAsync(db)}");
    return;
}
if (args.Contains("--initialize-system"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<Quran.Infrastructure.Persistence.QuranDbContext>();
    await db.Database.MigrateAsync();
    await AwjuhCatalog.InitializeAsync(db);
    Console.WriteLine("Database migration and catalog bootstrap completed.");
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();

// Streaming Audio avec support des requêtes HTTP Range (audio scrubbing/seeking)
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

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(baseStoragePath),
    RequestPath = "/audio"
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

app.MapRazorPages().WithStaticAssets();
app.MapControllers();

app.Run();
