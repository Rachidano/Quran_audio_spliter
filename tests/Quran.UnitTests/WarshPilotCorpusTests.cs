using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Quran.Infrastructure.Services;

namespace Quran.UnitTests;

public sealed class WarshPilotCorpusTests : IDisposable
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "warsh", WarshPilotCorpus.Revision);
    private readonly string _temporary = Path.Combine(Path.GetTempPath(), "quran-pilot-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void RealBundle_HasEditionSpecificReferencesAndOriginalViewBoxes()
    {
        var corpus = new WarshPilotCorpus(Fixture);
        Assert.Equal(new[] { 1, 2, 3 }, corpus.PageNumbers);
        Assert.Equal(new[] { "2:1", "2:2", "2:3", "2:4" }, corpus.GetPage(2)!.Regions.Select(r => r.Reference));
        Assert.Equal("2:5", corpus.GetPage(3)!.Regions[0].Reference);
        Assert.Equal("-53.8766 -213.6427 345 550", corpus.GetPage(2)!.ViewBox);
        Assert.Equal("-6 0 345 550", corpus.GetPage(3)!.ViewBox);
        Assert.Equal(22, corpus.PageNumbers.Sum(n => corpus.GetPage(n)!.Regions.Count));
        Assert.Null(corpus.GetPage(4));
        Assert.Null(corpus.GetPage(0));
        Assert.Null(corpus.GetImage(-1));
    }

    [Fact]
    public void MultilineAyah_PreservesDisconnectedSubpaths()
    {
        var region = new WarshPilotCorpus(Fixture).GetPage(3)!.Regions.Single(r => r.Reference == "2:12");
        Assert.Equal(3, region.PathData.Count(c => c == 'M'));
        Assert.Equal(3, region.PathData.Count(c => c == 'Z'));
        Assert.Contains("M 63.77 357.5", region.PathData);
    }

    [Fact]
    public void Import_IsIdempotentAndKeepsOriginalBytes()
    {
        WarshPilotCorpus.Import(Fixture, _temporary);
        var destination = Path.Combine(_temporary, WarshPilotCorpus.Revision);
        var before = File.GetLastWriteTimeUtc(Path.Combine(destination, "002.svg"));
        WarshPilotCorpus.Import(Fixture, _temporary);
        Assert.Equal(before, File.GetLastWriteTimeUtc(Path.Combine(destination, "002.svg")));
        Assert.Equal(File.ReadAllBytes(Path.Combine(Fixture, "002.svg")), new WarshPilotCorpus(destination).GetImage(2));
        Assert.Single(Directory.GetDirectories(_temporary));
    }

    [Fact]
    public void CorruptImage_RejectsImportBeforeCreatingDestination()
    {
        var source = CopyFixture();
        File.AppendAllText(Path.Combine(source, "002.svg"), "corruption");
        var destination = Path.Combine(_temporary, "destination");
        Assert.Throws<InvalidDataException>(() => WarshPilotCorpus.Import(source, destination));
        Assert.False(Directory.Exists(destination));
    }

    [Fact]
    public void DifferentJsonGeometry_IsRejectedEvenWithUpdatedChecksum()
    {
        var source = CopyFixture();
        var path = Path.Combine(source, "002.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("210,35", "209,35"));
        UpdateChecksum(source, "002.json");
        Assert.Throws<InvalidDataException>(() => new WarshPilotCorpus(source));
    }

    [Fact]
    public void ManifestCannotEscapeDirectory()
    {
        var source = CopyFixture();
        var path = Path.Combine(source, "manifest.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"001.svg\"", "\"../001.svg\""));
        Assert.Throws<InvalidDataException>(() => new WarshPilotCorpus(source));
    }

    [Fact]
    public void UnknownRevisionIsRejected()
    {
        var source = CopyFixture();
        var path = Path.Combine(source, "manifest.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace(WarshPilotCorpus.Revision, "unreviewed"));
        Assert.Throws<InvalidDataException>(() => new WarshPilotCorpus(source));
    }

    [Fact]
    public void SvgScriptIsRejectedEvenWithUpdatedChecksum()
    {
        var source = CopyFixture();
        var path = Path.Combine(source, "001.svg");
        File.WriteAllText(path, File.ReadAllText(path).Replace("</svg>", "<script>alert(1)</script></svg>"));
        UpdateChecksum(source, "001.svg");
        Assert.Throws<InvalidDataException>(() => new WarshPilotCorpus(source));
    }

    private string CopyFixture()
    {
        var source = Path.Combine(_temporary, "source");
        Directory.CreateDirectory(source);
        foreach (var file in Directory.GetFiles(Fixture)) File.Copy(file, Path.Combine(source, Path.GetFileName(file)));
        return source;
    }

    private static void UpdateChecksum(string source, string name)
    {
        var path = Path.Combine(source, "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(path))!;
        var entry = manifest["files"]!.AsArray().Single(e => (string?)e!["file"] == name)!;
        entry["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(source, name))));
        File.WriteAllText(path, manifest.ToJsonString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporary)) Directory.Delete(_temporary, recursive: true);
    }
}
