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
}
