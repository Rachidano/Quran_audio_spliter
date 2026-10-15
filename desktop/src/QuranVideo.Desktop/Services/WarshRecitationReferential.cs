namespace QuranVideo.Desktop.Services;

/// <summary>
/// Récitation Warsh de référence : fichiers MP3 des 114 sourates et, si publiés, minutages ayah par ayah.
/// </summary>
/// <param name="TimingReadId">Identifiant "read" des minutages mp3quran.net (= identifiant du moshaf), ou null.</param>
public sealed record WarshRecitation(
    string CollectionId,
    string ReciterId,
    string NameArabic,
    string NameLatin,
    string TitleArabic,
    string UrlTemplate,
    int? TimingReadId);

/// <summary>
/// Récitations Warsh intégrées à l'application (source : API mp3quran.net, moshaf « ورش عن نافع - مرتل »).
/// Pour chacune, les 114 sourates sont disponibles. Vérification du 15/10/2026 sur les 114 sourates :
/// les minutages de 120, 80, 16 et 14 suivent exactement le compte Warsh (6 214 ayat).
/// Le cheikh صابر عبد الحكم est géré à part (liens archive.org intégrés, pas de minutages Warsh publiés).
/// </summary>
public static class WarshRecitationReferential
{
    public static IReadOnlyList<WarshRecitation> All { get; } = new[]
    {
        new WarshRecitation(
            RecitationCatalogService.HusaryCollection, "husary",
            "الشيخ محمود خليل الحصري", "Mahmoud Khalil Al-Husary",
            "الشيخ محمود خليل الحصري (ورش عن نافع)",
            RecitationCatalogService.HusaryUrlTemplate, RecitationCatalogService.HusaryTimingReadId),
        new WarshRecitation(
            "qazabri_warsh", "omar_qazabri",
            "الشيخ عمر القزابري", "Omar Al-Qazabri",
            "الشيخ عمر القزابري (ورش عن نافع)",
            "https://cdn.mp3quran.net/audio/omar-kazabri/r1/{0:D3}.mp3", 80),
        new WarshRecitation(
            "koushi_warsh", "laayoun_kouchi",
            "الشيخ العيون الكوشي", "Al-Oyoun Al-Koushi",
            "الشيخ العيون الكوشي (ورش عن نافع)",
            "https://cdn.mp3quran.net/audio/laayoun-kouchi/r1/{0:D3}.mp3", 16),
        new WarshRecitation(
            "yassen_warsh", "yassen_jazairi",
            "القارئ ياسين", "Al-Qari Yassen",
            "القارئ ياسين (ورش عن نافع)",
            "https://cdn.mp3quran.net/audio/yassen-jazairi/r1/{0:D3}.mp3", 14),
    };
}
