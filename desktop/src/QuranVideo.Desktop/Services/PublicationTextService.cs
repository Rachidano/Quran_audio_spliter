using System.IO;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.Services;

/// <summary>
/// Écrit à côté de chaque vidéo un fichier texte (même nom, extension .txt) contenant un titre et
/// une description prêts à copier pour la publication : sourate, ayah(s), récitateur, riwaya, source.
/// Aucune publication automatique : le fichier sert uniquement à éviter les fautes de saisie.
/// </summary>
public sealed class PublicationTextService
{
    public const string Riwaya = "رواية ورش عن نافع";
    public const string MushafSource = "صفحات المصحف من البوابة الرسمية لوزارة الشؤون الدينية والأوقاف الجزائرية — مصحف الجزائر الإلكتروني";

    private readonly WorkspaceService _workspace;

    public PublicationTextService(WorkspaceService workspace)
    {
        _workspace = workspace;
    }

    public static string GetTextPath(string videoPath) => Path.ChangeExtension(videoPath, ".txt");

    public static string BuildText(string surahName, int startAyah, int endAyah, string? reciterName)
    {
        var ayatLabel = startAyah == endAyah
            ? $"الآية {startAyah}"
            : $"الآيات {startAyah} إلى {endAyah}";
        var reciter = string.IsNullOrWhiteSpace(reciterName) ? null : reciterName.Trim();

        var title = reciter == null
            ? $"سورة {surahName} - {ayatLabel} | {Riwaya}"
            : $"سورة {surahName} - {ayatLabel} | {reciter} | {Riwaya}";

        var sb = new StringBuilder();
        sb.AppendLine("العنوان:");
        sb.AppendLine(title);
        sb.AppendLine();
        sb.AppendLine("الوصف:");
        sb.AppendLine($"سورة {surahName} — {ayatLabel}");
        if (reciter != null) sb.AppendLine($"القارئ: {reciter}");
        sb.AppendLine($"الرواية: {Riwaya.Replace("رواية ", string.Empty)}");
        sb.AppendLine(MushafSource);
        sb.AppendLine();
        sb.AppendLine($"#القرآن_الكريم #رواية_ورش #سورة_{surahName.Replace(' ', '_')}");
        return sb.ToString();
    }

    /// <summary>
    /// Écrit le fichier texte de publication d'une vidéo. Une erreur n'interrompt jamais l'export :
    /// elle est journalisée et la méthode retourne null.
    /// </summary>
    public async Task<string?> WriteForVideoAsync(
        string videoPath,
        int surahNumber,
        int startAyah,
        int endAyah,
        string audioSourceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var surahName = SurahCatalogService.GetSurah(surahNumber)?.NameArabic ?? surahNumber.ToString();
            var reciterName = await ResolveReciterNameAsync(audioSourceId, cancellationToken);
            var textPath = GetTextPath(videoPath);
            // UTF-8 avec BOM : l'arabe s'affiche correctement dans tous les éditeurs Windows
            await File.WriteAllTextAsync(textPath, BuildText(surahName, startAyah, endAyah, reciterName),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
            return textPath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogWarning($"Fichier de publication non écrit pour {videoPath} : {ex.Message}");
            return null;
        }
    }

    private async Task<string?> ResolveReciterNameAsync(string audioSourceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(audioSourceId)) return null;
        using var db = new AppDbContext(_workspace.GetDatabasePath());
        return await db.AudioSources
            .Where(a => a.Id == audioSourceId)
            .Select(a => a.Collection!.Reciter!.NameArabic)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Sépare le titre et la description d'un fichier écrit par <see cref="BuildText"/>.</summary>
    public static PublicationText? Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var titleIndex = Array.FindIndex(lines, l => l.Trim() == "العنوان:");
        var descriptionIndex = Array.FindIndex(lines, l => l.Trim() == "الوصف:");
        if (titleIndex < 0 || descriptionIndex < 0 || descriptionIndex < titleIndex) return null;

        var title = string.Join(" ", lines[(titleIndex + 1)..descriptionIndex].Where(l => l.Trim().Length > 0)).Trim();
        var description = string.Join("\n", lines[(descriptionIndex + 1)..]).Trim();
        return title.Length == 0 ? null : new PublicationText(title, description);
    }

    /// <summary>Lit le titre et la description de la vidéo, ou null si le fichier n'existe pas.</summary>
    public static PublicationText? Read(string videoPath)
    {
        var textPath = GetTextPath(videoPath);
        if (!File.Exists(textPath)) return null;
        try
        {
            return Parse(File.ReadAllText(textPath));
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// (Re)crée le texte d'une vidéo déjà produite. Les informations viennent de la base (projet,
    /// composition ou tâche de lot qui a produit ce fichier), sinon du nom du fichier.
    /// Retourne null si la sourate et les ayat ne peuvent pas être déterminées.
    /// </summary>
    public async Task<string?> GenerateForExistingVideoAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        var target = await ResolveVideoTargetAsync(videoPath, cancellationToken);
        if (target == null) return null;

        if (target.ReciterIdFallback != null)
        {
            // Nom de fichier seul : retrouver le nom du récitateur à partir de son identifiant
            var surahName = SurahCatalogService.GetSurah(target.SurahNumber)?.NameArabic ?? target.SurahNumber.ToString();
            string? reciterName = null;
            if (target.ReciterIdFallback.Length > 0 && File.Exists(_workspace.GetDatabasePath()))
            {
                using var db = new AppDbContext(_workspace.GetDatabasePath());
                reciterName = await db.Reciters
                    .Where(r => r.Id == target.ReciterIdFallback)
                    .Select(r => r.NameArabic)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            var textPath = GetTextPath(videoPath);
            await File.WriteAllTextAsync(textPath, BuildText(surahName, target.StartAyah, target.EndAyah, reciterName),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
            return textPath;
        }

        return await WriteForVideoAsync(videoPath, target.SurahNumber, target.StartAyah, target.EndAyah,
            target.AudioSourceId ?? string.Empty, cancellationToken);
    }

    private sealed record VideoTarget(int SurahNumber, int StartAyah, int EndAyah, string? AudioSourceId, string? ReciterIdFallback);

    private async Task<VideoTarget?> ResolveVideoTargetAsync(string videoPath, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(videoPath);
        bool SameFile(string? stored) => !string.IsNullOrEmpty(stored) &&
            string.Equals(Path.GetFileName(stored), fileName, StringComparison.OrdinalIgnoreCase);

        if (File.Exists(_workspace.GetDatabasePath()))
        using (var db = new AppDbContext(_workspace.GetDatabasePath()))
        {
            var project = (await db.Projects.Where(p => p.OutputVideoPath != null && p.OutputVideoPath.EndsWith(fileName))
                    .ToListAsync(cancellationToken))
                .FirstOrDefault(p => SameFile(p.OutputVideoPath));
            if (project != null)
                return new VideoTarget(project.SurahNumber, project.StartAyahNumber, project.EndAyahNumber, project.AudioSourceId, null);

            var composition = (await db.Compositions.Where(c => c.RelativeVideoPath.EndsWith(fileName))
                    .ToListAsync(cancellationToken))
                .FirstOrDefault(c => SameFile(c.RelativeVideoPath));
            if (composition != null)
                return new VideoTarget(composition.SurahNumber, composition.AyahNumber, composition.AyahNumber, composition.AudioSourceId, null);

            var job = (await db.BatchJobs.Where(j => j.RelativeVideoPath.EndsWith(fileName))
                    .ToListAsync(cancellationToken))
                .FirstOrDefault(j => SameFile(j.RelativeVideoPath));
            if (job != null)
                return new VideoTarget(job.SurahNumber, job.AyahNumber, job.AyahNumber, job.AudioSourceId, null);
        }

        // Noms produits par l'application (voir CompositionService et BatchExportService)
        var name = Path.GetFileNameWithoutExtension(videoPath);
        var single = System.Text.RegularExpressions.Regex.Match(name, @"^ayah_(\d{3})_(\d{3})_(.+)_[hv]_p\d+_r\d+_i\d+$");
        if (single.Success)
            return Target(single.Groups[1].Value, single.Groups[2].Value, single.Groups[2].Value, single.Groups[3].Value);

        var range = System.Text.RegularExpressions.Regex.Match(name, @"^project_p\d+_(\d{3})_(\d{3})_(\d{3})_(.+)_[hv]_r\d+$");
        if (range.Success)
            return Target(range.Groups[1].Value, range.Groups[2].Value, range.Groups[3].Value, range.Groups[4].Value);

        var batch = System.Text.RegularExpressions.Regex.Match(name, @"^ayah_(\d{3})_(\d{3})_[hv]_rev\d+$");
        if (batch.Success)
            return Target(batch.Groups[1].Value, batch.Groups[2].Value, batch.Groups[2].Value, string.Empty);

        return null;

        static VideoTarget? Target(string surah, string start, string end, string reciterId)
        {
            int s = int.Parse(surah), a = int.Parse(start), b = int.Parse(end);
            var info = SurahCatalogService.GetSurah(s);
            if (info == null || a < 1 || b < a || b > info.AyahCount) return null;
            return new VideoTarget(s, a, b, null, reciterId);
        }
    }
}

public sealed record PublicationText(string Title, string Description);
