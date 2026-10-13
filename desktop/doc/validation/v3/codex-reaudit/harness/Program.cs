using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using QuranVideo.Tests;

var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var results = new List<object>();
void Record(object result) { results.Add(result); Console.WriteLine(JsonSerializer.Serialize(result)); }
foreach (var scenario in new[] { "valid-control", "missing-saved-take", "changed-revision-wizard", "changed-image-revision", "implicit-multiple-takes", "valid-first-incomplete-second-batch" })
{
    using var fixture = new WizardProjectTests();
    var ws = (WorkspaceService)fixture.GetType().GetField("_workspaceService", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(fixture)!;
    using var vm = new StudioViewModel(ws, surahNumber: 1, ayahNumber: 1);
    // Supply the already selected catalog choice without starting playback of fixture audio.
    var choice = new RecitationChoice("saber_abdelhakam_warsh", "saber_abdelhakam", "Saber");
    typeof(StudioViewModel).GetField("_selectedRecitation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, choice);
    vm.Reciters.Add(choice);
    vm.WizardStartAyah = 1; vm.WizardEndAyah = 2;
    await vm.RefreshWizardSequenceAsync();
    await vm.SaveProjectAsync();
    using var db = new AppDbContext(ws.GetDatabasePath());
    var project = await db.Projects.Include(p => p.SelectedAyahs).SingleAsync();
    var original = db.Passages.Single(p => p.AyahNumber == 1);
    var originalId = original.Id;
    var validator = new SequenceValidationService(ws);
    if (scenario == "valid-first-incomplete-second-batch")
    {
        var source = db.AudioSources.Single();
        using (var writer = new NAudio.Wave.WaveFileWriter(ws.ToAbsolutePath(source.RelativeFilePath), new NAudio.Wave.WaveFormat(48000,16,1)))
            for (int i=0;i<48000*5;i++) writer.WriteSample((float)(Math.Sin(i*2*Math.PI*440/48000)*0.1));
        source.DurationSeconds = 5;
        await db.SaveChangesAsync();
        var firstValid = await validator.ValidateProjectAsync(project, true);
        var second = new VideoProjectEntity { Title="Missing ayah 3", SurahNumber=1, StartAyahNumber=1, EndAyahNumber=3, ReciterId=project.ReciterId, AudioSourceId=project.AudioSourceId };
        var col = new CollectionEntity { Name="Audit batch" };
        col.Items.Add(new CollectionItemEntity { Project=project, OrderIndex=1 });
        col.Items.Add(new CollectionItemEntity { Project=second, OrderIndex=2 });
        db.Collections.Add(col); await db.SaveChangesAsync();
        var collections = new CollectionsViewModel(ws);
        await collections.LoadCollectionsAsync(); await collections.LoadCurrentCollectionItemsAsync();
        await collections.ExportCollectionBatchAsync();
        var outputs = Directory.GetFiles(Path.Combine(ws.WorkspacePath,"media","videos"),"*.mp4").Length;
        Record(new { scenario, firstValid=firstValid.IsValid, firstErrors=firstValid.GetAllErrorMessages(), exportedFiles=outputs, blocked=!string.IsNullOrEmpty(collections.BatchErrorMessage) });
    }
    if (scenario == "valid-control")
    {
        var control = await validator.ValidateProjectAsync(project, false);
        Record(new { scenario, valid = control.IsValid, errors = control.GetAllErrorMessages() });
    }
    if (scenario == "changed-image-revision")
    {
        db.ImageRecipes.First().Revision++;
        await db.SaveChangesAsync();
        Record(new { scenario, expectedValid = false, actualValid = (await validator.ValidateProjectAsync(project, false)).IsValid });
    }
    if (scenario == "changed-revision-wizard")
    {
        original.Revision++; original.StartTimeSec = 0.1;
        await db.SaveChangesAsync();
        var before = await validator.ValidateProjectAsync(project, false);
        await vm.OpenSavedProjectAsync(project);
        // ExportProjectAsync itself calls SaveProjectAsync before validation.
        await vm.SaveProjectAsync();
        using var refreshed = new AppDbContext(ws.GetDatabasePath());
        var saved = await refreshed.Projects.Include(p => p.SelectedAyahs).SingleAsync();
        var after = await validator.ValidateProjectAsync(saved, false);
        Record(new { scenario, beforeValid = before.IsValid, afterOpenAndSaveValid = after.IsValid, expectedAfterValid = false });
    }
    if (scenario is "missing-saved-take" or "implicit-multiple-takes")
    {
        var replacement = new AyahPassageEntity { AudioSourceId = original.AudioSourceId, AyahId = original.AyahId,
            SurahNumber = 1, AyahNumber = 1, StartTimeSec = 0, EndTimeSec = 2,
            Kind = PassageKind.FullRetake, Revision = 2, IsValidatedByUser = true };
        db.Passages.Add(replacement);
        if (scenario == "missing-saved-take") db.Passages.Remove(original);
        await db.SaveChangesAsync();
        if (scenario == "missing-saved-take") await vm.OpenSavedProjectAsync(project);
        else await vm.RefreshWizardSequenceAsync();
        var selected = vm.WizardAyatItems.First().SelectedTake?.Id;
        await vm.SaveProjectAsync();
        using var refreshed = new AppDbContext(ws.GetDatabasePath());
        var saved = await refreshed.Projects.Include(p => p.SelectedAyahs).SingleAsync();
        Record(new { scenario, originalId, selectedWithoutUserChoice = selected, valid = (await validator.ValidateProjectAsync(saved, false)).IsValid });
    }
    ws.Dispose();
}
using (var ws = new WorkspaceService())
{
    ws.InitializeWorkspace(Path.Combine(output, "backup-case-" + Guid.NewGuid().ToString("N")));
    using (var db = new AppDbContext(ws.GetDatabasePath())) db.Database.EnsureCreated();
    var backup = ws.GetDatabasePath() + ".pre_v3.bak";
    File.WriteAllText(backup, "not a SQLite backup");
    try {
        var ok = await new DatabaseMigrationService().MigrateDatabaseAsync(ws.GetDatabasePath());
        Record(new { scenario = "corrupt-existing-backup", migrationAccepted = ok, backupRemainsInvalid = File.ReadAllText(backup) == "not a SQLite backup" });
    } catch (Exception ex) { Record(new { scenario = "corrupt-existing-backup", blocked = true, error = ex.Message }); }
}
await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
