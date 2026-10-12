using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Models;
using QuranVideo.Desktop.Services;

// Audit isolé : aucun fichier utilisateur, aucune correction applicative.
var root = Path.GetFullPath(args[0]);
Directory.CreateDirectory(root);
var results = new List<object>();
using var ws = new WorkspaceService();
var init = ws.InitializeWorkspace(Path.Combine(root, "workspace"));
if (!init.Success) throw new Exception(init.ErrorMessage);
var dbPath = Path.Combine(ws.WorkspacePath, "data", "quran.db");
using (var db = new AppDbContext(dbPath))
{
    db.Database.EnsureCreated();
    db.Reciters.Add(new ReciterEntity { Id = "husary", NameArabic = "Test" });
    db.RecitationCollections.Add(new RecitationCollectionEntity { Id = "husary_murattal_warsh", ReciterId = "husary", BaseUrlTemplate = "https://example.invalid/{0:D3}.mp3" });
    db.AudioSources.Add(new AudioSourceEntity { Id = "husary_murattal_warsh:001", CollectionId = "husary_murattal_warsh", SurahNumber = 1, DurationSeconds = 40, SampleRate = 44100 });
    db.Surahs.Add(new SurahEntity { Number = 1, AyahCount = 7, StartPage = 1, EndPage = 2 });
    db.Ayahs.Add(new AyahEntity { Id = "1:1", SurahNumber = 1, AyahNumber = 1, PageNumber = 1 });
    db.SaveChanges();
}
var pages = Path.Combine(ws.WorkspacePath, "media", "mushaf", "pages");
Directory.CreateDirectory(pages);
foreach (var (page, color) in new[] { (1, Color.Red), (2, Color.Blue) })
{
    using var bmp = new Bitmap(600, 600);
    using var g = Graphics.FromImage(bmp);
    g.Clear(color);
    bmp.Save(Path.Combine(pages, $"page-{page:D3}.png"), ImageFormat.Png);
}
var images = new AyahImageService(ws);
var absent = await images.GetOrExtractAyahImageAsync(1, 1);
Record("A01", "Aucune région : refuser l'extraction", !absent.Success,
    $"Success={absent.Success}; taille={absent.Recipe.Width}x{absent.Recipe.Height}");
using (var db = new AppDbContext(dbPath))
{
    db.Regions.AddRange(
        new AyahRegionEntity { AyahId = "1:1", PageNumber = 1, OrderIndex = 1, PixelX = 10, PixelY = 10, PixelWidth = 100, PixelHeight = 50 },
        new AyahRegionEntity { AyahId = "1:1", PageNumber = 2, OrderIndex = 2, PixelX = 10, PixelY = 100, PixelWidth = 100, PixelHeight = 50 });
    db.SaveChanges();
}
var multi = await images.GetOrExtractAyahImageAsync(1, 1, true);
int blue = 0;
using (var bmp = new Bitmap(multi.ImagePath))
    for (var y = 0; y < bmp.Height; y++)
        for (var x = 0; x < bmp.Width; x++)
            if (bmp.GetPixel(x, y).ToArgb() == Color.Blue.ToArgb()) blue++;
Record("A02", "Ayah sur deux pages : inclure les pixels de la deuxième page", blue > 0, $"Pixels bleus={blue}");

var annotations = new AnnotationService(ws);
AyahPassageEntity Passage(PassageKind kind, double start) => new() {
    AudioSourceId = "husary_murattal_warsh:001", AyahId = "1:1", SurahNumber = 1, AyahNumber = 1,
    StartTimeSec = start, EndTimeSec = start + 2, Kind = kind,
    IsValidatedByUser = false, IsSelectedForExport = true
};
await annotations.SavePassageAsync(Passage(PassageKind.PartialRetake, 1), 40);
var prepared = await new CompositionService(ws).PrepareCompositionAsync(1, 1, VideoOrientation.Horizontal);
Record("A03", "Refuser Ready pour une reprise partielle et une image non approuvées",
    !prepared.Success || prepared.Composition?.Status != CompositionStatus.Ready,
    $"Success={prepared.Success}; status={prepared.Composition?.Status}; image={multi.Recipe.Status}");
await annotations.SavePassageAsync(Passage(PassageKind.FullRetake, 10), 40);
await annotations.SavePassageAsync(Passage(PassageKind.FullRetake, 20), 40);
var takes = (await annotations.GetPassagesForAyahAsync("husary_murattal_warsh:001", "1:1"))
    .Where(p => p.Kind == PassageKind.FullRetake).ToList();
Record("A04", "Conserver deux nouvelles reprises distinctes", takes.Count == 2,
    $"Reprises conservées={takes.Count}; débuts={string.Join(',', takes.Select(p => p.StartTimeSec))}");

await ws.SaveSessionStateAsync(new SessionState { LastSurahNumber = 2, LastAyahNumber = 4 });
var backups = new BackupService(ws);
var zip = Path.Combine(root, "backup.zip");
var backup = await backups.CreateBackupAsync(zip);
if (!backup.Success) throw new Exception(backup.ErrorMessage);
using (var archive = ZipFile.OpenRead(zip))
    Record("A05", "La sauvegarde complète conserve la session config.json",
        archive.GetEntry("config.json") != null,
        $"config.json présent={archive.GetEntry("config.json") != null}");
var corrupted = Path.Combine(root, "corrupted.zip");
File.Copy(zip, corrupted, true);
using (var archive = ZipFile.Open(corrupted, ZipArchiveMode.Update))
{
    archive.GetEntry("data/quran.db")!.Delete();
    using var writer = new StreamWriter(archive.CreateEntry("data/quran.db").Open());
    writer.Write("corrupted database; manifest hash intentionally unchanged");
}
var restored = await backups.RestoreBackupAsync(corrupted, Path.Combine(root, "restore"));
Record("A06", "Refuser une sauvegarde dont la base ne correspond pas à son empreinte",
    !restored.Success, $"Restore.Success={restored.Success}");
using (var importWs = new WorkspaceService())
{
    importWs.InitializeWorkspace(Path.Combine(root, "real-import"));
    try
    {
        var pack = Path.GetFullPath("../data/mushaf/editions/marw-newMushaf/20260913-full");
        var imported = await new CorpusImportService(importWs).ImportPackAsync(pack);
        Record("A07", "Importer le manifeste réel du dépôt", imported.Success, imported.ErrorMessage ?? "success");
    }
    catch (Exception ex)
    {
        Record("A07", "Importer le manifeste réel du dépôt", false, $"{ex.GetType().Name}: {ex.Message}");
    }
}
var tone = Path.Combine(root, "tone.mp3");
var psi = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
foreach (var a in new[] { "-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=0.5", tone }) psi.ArgumentList.Add(a);
using (var proc = Process.Start(psi)!)
{
    var err = await proc.StandardError.ReadToEndAsync();
    await proc.WaitForExitAsync();
    if (proc.ExitCode != 0) throw new Exception(err);
}
using (var http = new HttpClient(new LocalAudioResponse(await File.ReadAllBytesAsync(tone))))
{
    var download = await new RecitationLibraryService(ws, http).DownloadSurahAudioAsync("husary_murattal_warsh", 1);
    Record("A08", "Téléchargement simulé localement : enregistrer le MP3 reçu", download.Success, download.ErrorMessage ?? "success");
}
await File.WriteAllTextAsync(Path.Combine(root, "results.json"),
    JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));

void Record(string id, string expected, bool passed, string actual)
{
    results.Add(new { id, expected, passed, actual });
    Console.WriteLine($"{id}: {(passed ? "PASS" : "FAIL")} | {actual}");
}

sealed class LocalAudioResponse(byte[] bytes) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
}
