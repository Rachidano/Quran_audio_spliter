using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using QuranVideo.Tests;
using QuranVideo.Desktop.ViewModels;

var findings = new List<object>();
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
foreach (var scenario in new[] { "missing-ayah-control", "unreviewed-audio", "unreviewed-image", "wrong-reciter", "unreviewed-gap", "changed-revision", "multiple-takes", "past-end", "invalid-media" })
{
    using var fixture = new SequenceValidationTests();
    var type = fixture.GetType();
    object Field(string n) => type.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!;
    void Seed(string name, params object[] values) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(fixture, values);
    var ws = (WorkspaceService)Field("_workspaceService");
    Seed("SeedMockSurahAndAudio", 1, "saber", "source", 20d);
    for (int i = 1; i <= 2; i++)
    {
        Seed("SeedMockImageRecipe", 1, i, true);
        Seed("SeedMockPassage", "source", 1, i, (i - 1) * 2d, i * 2d, PassageKind.Full);
    }
    using var db = new AppDbContext(ws.GetDatabasePath());
    var passages = db.Passages.OrderBy(p => p.AyahNumber).ToList();
    var project = new VideoProjectEntity {
        SurahNumber = 1, StartAyahNumber = 1, EndAyahNumber = 2,
        ReciterId = "saber", AudioSourceId = "source",
        SelectedAyahs = passages.Select(p => new ProjectAyahEntity {
            AyahNumber = p.AyahNumber, PassageId = p.Id, PassageRevision = 1,
            StartTimeSec = p.StartTimeSec, EndTimeSec = p.EndTimeSec
        }).ToList()
    };
    switch (scenario)
    {
        case "missing-ayah-control": db.Passages.Remove(passages[1]); break;
        case "unreviewed-audio": passages[0].IsValidatedByUser = false; break;
        case "unreviewed-image": db.ImageRecipes.First().Status = AyahImageStatus.NeedsReview; break;
        case "wrong-reciter": project.ReciterId = "another-reciter"; break;
        case "unreviewed-gap": passages[1].StartTimeSec = 6; passages[1].EndTimeSec = 8; break;
        case "changed-revision": passages[0].Revision = 2; passages[0].EndTimeSec = 1.9; break;
        case "multiple-takes":
            project.SelectedAyahs.Clear();
            Seed("SeedMockPassage", "source", 1, 1, 0d, 2d, PassageKind.FullRetake);
            break;
        case "past-end": passages[1].EndTimeSec = 20.5; break;
    }
    db.SaveChanges();
    // Fixture media are intentionally just headers. For business-rule cases isolate
    // the rules by disabling physical checks; invalid-media explicitly enables them.
    var result = await new SequenceValidationService(ws).ValidateProjectAsync(project, scenario == "invalid-media");
    findings.Add(new { scenario, expectedValid = false, actualValid = result.IsValid,
        result.HasJunctionWarnings, errors = result.GetAllErrorMessages() });
    Console.WriteLine($"{scenario}: expected=False actual={result.IsValid} junctionWarning={result.HasJunctionWarnings}");
    ws.Dispose();
}
using (var ws = new WorkspaceService())
{
    ws.InitializeWorkspace(Path.Combine(output, "fresh-workspace-" + Guid.NewGuid().ToString("N")));
    try
    {
        await new DatabaseMigrationService().MigrateDatabaseAsync(ws.GetDatabasePath());
        await new RecitationLibraryService(ws).InitializeDefaultCollectionAsync();
        findings.Add(new { scenario = "fresh-start-migration-then-catalog", success = true });
    }
    catch (Exception ex)
    {
        Console.WriteLine("fresh-start: " + ex.GetBaseException().Message);
        findings.Add(new { scenario = "fresh-start-migration-then-catalog", success = false, error = ex.GetBaseException().Message });
    }
}
// Real decodable media: prove the renderer itself accepts unreviewed material,
// and a collection can start before finding an incomplete later project.
using (var fixture = new SequenceValidationTests())
{
    var type = fixture.GetType();
    void Seed(string name, params object[] values) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(fixture, values);
    var ws = (WorkspaceService)type.GetField("_workspaceService", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!;
    Seed("SeedMockSurahAndAudio", 1, "saber", "source", 20d);
    for (int i = 1; i <= 2; i++)
    {
        Seed("SeedMockImageRecipe", 1, i, true);
        Seed("SeedMockPassage", "source", 1, i, (i - 1) * 2d, i * 2d, PassageKind.Full);
    }
    using var db = new AppDbContext(ws.GetDatabasePath());
    var source = db.AudioSources.Single();
    var audioPath = ws.ToAbsolutePath(source.RelativeFilePath);
    using (var writer = new NAudio.Wave.WaveFileWriter(audioPath, new NAudio.Wave.WaveFormat(48000, 16, 1)))
    {
        for (int i = 0; i < 48000 * 5; i++) writer.WriteSample((float)(Math.Sin(i * 2 * Math.PI * 440 / 48000) * .1));
    }
    source.DurationSeconds = 5;
    foreach (var img in db.ImageRecipes)
    {
        using var bitmap = new System.Drawing.Bitmap(300, 100);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        g.Clear(System.Drawing.Color.White);
        g.FillRectangle(System.Drawing.Brushes.Black, 40, 30, 100, 20);
        bitmap.Save(ws.ToAbsolutePath(img.RelativeImagePath), System.Drawing.Imaging.ImageFormat.Png);
        img.Status = AyahImageStatus.NeedsReview;
    }
    var passages = db.Passages.OrderBy(p => p.AyahNumber).ToList();
    foreach (var p in passages) p.IsValidatedByUser = false;
    var first = new VideoProjectEntity { Title = "audit-unreviewed", SurahNumber = 1, StartAyahNumber = 1, EndAyahNumber = 2, ReciterId = "saber", AudioSourceId = "source" };
    foreach (var p in passages) first.SelectedAyahs.Add(new ProjectAyahEntity { OrderIndex = p.AyahNumber, AyahNumber = p.AyahNumber, PassageId = p.Id, RelativeImagePath = db.ImageRecipes.Single(r => r.AyahNumber == p.AyahNumber).RelativeImagePath });
    db.Projects.Add(first);
    db.SaveChanges();
    var service = new CompositionService(ws);
    var rendered = await service.RenderProjectAsync(first.Id);
    Console.WriteLine($"actual-render-unreviewed: success={rendered.Success} error={rendered.ErrorMessage}");
    findings.Add(new { scenario = "actual-render-unreviewed", rendered.Success, rendered.ErrorMessage });
    if (rendered.Success) File.Copy(ws.ToAbsolutePath(rendered.Project.OutputVideoPath!), Path.Combine(output, "unreviewed-render.mp4"), true);
    // Restore validation status to isolate the collection's incomplete second entry.
    foreach (var p in passages) p.IsValidatedByUser = true;
    foreach (var img in db.ImageRecipes) img.Status = AyahImageStatus.Approved;
    first.Status = "Draft";
    var second = new VideoProjectEntity { Title = "audit-missing-ayah-3", SurahNumber = 1, StartAyahNumber = 1, EndAyahNumber = 3, ReciterId = "saber", AudioSourceId = "source" };
    second.SelectedAyahs.Add(new ProjectAyahEntity { OrderIndex = 1, AyahNumber = 1, PassageId = passages[0].Id, RelativeImagePath = db.ImageRecipes.First().RelativeImagePath });
    var collection = new CollectionEntity { Name = "Audit" };
    collection.Items.Add(new CollectionItemEntity { Project = first, OrderIndex = 1 });
    collection.Items.Add(new CollectionItemEntity { Project = second, OrderIndex = 2 });
    db.Collections.Add(collection);
    db.SaveChanges();
    var vm = new CollectionsViewModel(ws);
    await vm.LoadCollectionsAsync();
    await vm.LoadCurrentCollectionItemsAsync();
    var ready = vm.CurrentCollectionItems.Select(x => x.IsReady).ToArray();
    await vm.ExportCollectionBatchAsync();
    using var check = new AppDbContext(ws.GetDatabasePath());
    var firstStatus = check.Projects.Single(p => p.Id == first.Id).Status;
    findings.Add(new { scenario = "batch-with-incomplete-second-project", readiness = ready, firstStatus, vm.BatchErrorMessage });
    Console.WriteLine($"batch incomplete: readiness={string.Join(',',ready)} firstStatus={firstStatus} error={vm.BatchErrorMessage}");
    ws.Dispose();
}
await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(findings, new JsonSerializerOptions { WriteIndented = true }));
