using System.Text.Json;

namespace Quran.Infrastructure.Services;

/// <summary>Explicit visual observations bound to the exact source images and maps.</summary>
public static class MarwOpeningReview
{
    public sealed record Entry(int Surah, int Page, int LineCount, int FirstTextLine, string MapSha256, string ImageSha256);
    public sealed record Report(string Revision, string ReviewVersion, string Scope, string Reviewer, IReadOnlyList<Entry> Entries);
    public static Report Current { get; } = Load();

    private static Report Load()
    {
        using var stream = typeof(MarwOpeningReview).Assembly.GetManifestResourceStream("Quran.Infrastructure.Data.marw-opening-review.json")
            ?? throw new InvalidDataException("Rapport de revue MARW absent.");
        var report = JsonSerializer.Deserialize<Report>(stream) ?? throw new InvalidDataException("Rapport de revue MARW invalide.");
        if (report.Revision != MarwPilotCorpus.Revision || report.Entries.Count != 114
            || !report.Entries.Select(e => e.Surah).SequenceEqual(Enumerable.Range(1, 114))
            || report.Entries.Any(e => e.Page is < 1 or > 604 || e.LineCount != (e.Page <= 2 ? 7 : 15)
                || e.FirstTextLine < 0 || e.FirstTextLine >= e.LineCount))
            throw new InvalidDataException("Revue MARW incompatible avec cette édition.");
        return report;
    }

    public static double Start(int page, int surah, string mapSha256, string imageSha256)
    {
        var entry = Current.Entries[surah - 1];
        if (entry.Page != page || !entry.MapSha256.Equals(mapSha256, StringComparison.OrdinalIgnoreCase)
            || !entry.ImageSha256.Equals(imageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("La correction MARW ne correspond pas aux fichiers relus.");
        return entry.FirstTextLine * 100.0 / entry.LineCount;
    }
}
