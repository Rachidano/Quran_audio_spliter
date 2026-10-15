using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuranVideo.Desktop.Data;

[Table("mushaf_editions")]
public class MushafEditionEntity
{
    [Key]
    public string Code { get; set; } = string.Empty; // e.g. "newMushaf"
    public string Revision { get; set; } = string.Empty; // e.g. "20260913-full"
    public string ProvenanceUrl { get; set; } = string.Empty;
    public string ManifestSha256 { get; set; } = string.Empty;
    public string CountingSystem { get; set; } = "Madani / Warsh";
    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;
}

[Table("surahs")]
public class SurahEntity
{
    [Key]
    public int Number { get; set; }
    public string NameArabic { get; set; } = string.Empty;
    public string NameLatin { get; set; } = string.Empty;
    public int AyahCount { get; set; }
    public int StartPage { get; set; }
    public int EndPage { get; set; }
}

[Table("ayahs")]
public class AyahEntity
{
    [Key]
    public string Id { get; set; } = string.Empty; // e.g. "1:1", "2:285"
    public int SurahNumber { get; set; }
    public int AyahNumber { get; set; }
    public int VerseNo { get; set; } // Global sequential 1..6214
    public int PageNumber { get; set; }

    [ForeignKey(nameof(SurahNumber))]
    public SurahEntity? Surah { get; set; }
}

[Table("mushaf_pages")]
public class MushafPageEntity
{
    [Key]
    public int PageNumber { get; set; } // 1..604
    public string EditionCode { get; set; } = string.Empty;
    public string RelativeImagePath { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

[Table("ayah_regions")]
public class AyahRegionEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string AyahId { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public int OrderIndex { get; set; }

    // Calculated pixel bounds
    public double PixelX { get; set; }
    public double PixelY { get; set; }
    public double PixelWidth { get; set; }
    public double PixelHeight { get; set; }

    // Raw normalized boxes (as JSON or percentages)
    public double RawRectLeft { get; set; }
    public double RawRectTop { get; set; }
    public double RawRectWidth { get; set; }
    public double RawRectHeight { get; set; }

    [ForeignKey(nameof(AyahId))]
    public AyahEntity? Ayah { get; set; }
}

[Table("ayah_texts")]
public class AyahTextEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string CorpusId { get; set; } = string.Empty; // e.g. "warsh-qpc-v10"
    public string AyahId { get; set; } = string.Empty;
    public int SurahNumber { get; set; }
    public int AyahNumber { get; set; }
    public string OriginalText { get; set; } = string.Empty;

    [ForeignKey(nameof(AyahId))]
    public AyahEntity? Ayah { get; set; }
}

[Table("reciters")]
public class ReciterEntity
{
    [Key]
    public string Id { get; set; } = string.Empty; // e.g. "husary"
    public string NameArabic { get; set; } = string.Empty;
    public string NameLatin { get; set; } = string.Empty;
}

[Table("recitation_collections")]
public class RecitationCollectionEntity
{
    [Key]
    public string Id { get; set; } = string.Empty; // e.g. "husary_murattal_warsh"
    public string ReciterId { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Riwaya { get; set; } = "warsh";
    public string BaseUrlTemplate { get; set; } = string.Empty;

    /// <summary>Identifiant "read" des minutages publiés par mp3quran.net (ex. 120 = الحصري ورش), si disponibles.</summary>
    public int? TimingReadId { get; set; }

    [ForeignKey(nameof(ReciterId))]
    public ReciterEntity? Reciter { get; set; }
}

[Table("audio_sources")]
public class AudioSourceEntity
{
    [Key]
    public string Id { get; set; } = string.Empty; // e.g. "husary_murattal_warsh:001"
    public string CollectionId { get; set; } = string.Empty;
    public int SurahNumber { get; set; }
    public string RelativeFilePath { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(CollectionId))]
    public RecitationCollectionEntity? Collection { get; set; }
}

public enum PassageKind
{
    Full = 0,            // آية كاملة
    Portion = 1,         // مقطع جزئي
    Continuation = 2,    // متابعة / وصل
    FullRetake = 3,      // إعادة كاملة
    PartialRetake = 4    // إعادة جزئية
}

[Table("ayah_passages")]
public class AyahPassageEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string AudioSourceId { get; set; } = string.Empty;
    public int SurahNumber { get; set; }
    public int AyahNumber { get; set; }
    public string AyahId { get; set; } = string.Empty; // e.g. "1:4"

    public double StartTimeSec { get; set; }
    public double EndTimeSec { get; set; }
    public PassageKind Kind { get; set; } = PassageKind.Full;
    public bool IsSelectedForExport { get; set; } = true;
    public int Revision { get; set; } = 1;
    public bool IsValidatedByUser { get; set; } = false;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(AudioSourceId))]
    public AudioSourceEntity? AudioSource { get; set; }

    [ForeignKey(nameof(AyahId))]
    public AyahEntity? Ayah { get; set; }

    [NotMapped]
    public string FormattedDuration => $"{Math.Max(0, EndTimeSec - StartTimeSec).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} ثانية";

    [NotMapped]
    public string FormattedRange => $"{FormattedStartTime} ➔ {FormattedEndTime}";

    [NotMapped]
    public string FormattedStartTime => FormatClock(StartTimeSec);

    [NotMapped]
    public string FormattedEndTime => FormatClock(EndTimeSec);

    /// <summary>État affiché dans le tableau des segments du studio.</summary>
    [NotMapped]
    public string StatusLabel => IsProposedTiming ? "مقترح" : Kind switch
    {
        PassageKind.FullRetake => "إعادة كاملة",
        PassageKind.PartialRetake => "إعادة جزئية",
        PassageKind.Portion => "مقطع جزئي",
        PassageKind.Continuation => "متابعة",
        _ => "مسجّل"
    };

    /// <summary>mm:ss.ff, ou h:mm:ss.ff au-delà d'une heure.</summary>
    public static string FormatClock(double seconds)
    {
        // Arrondi aux centièmes (9,6199999 s -> 09.62) ; les heures pour les longues sourates (Al-Baqara : 4 h)
        var centi = (long)Math.Round(Math.Max(0, double.IsFinite(seconds) ? seconds : 0) * 100, MidpointRounding.AwayFromZero);
        long hours = centi / 360_000, minutes = centi / 6_000 % 60, secs = centi / 100 % 60, cs = centi % 100;
        return hours > 0
            ? $"{hours}:{minutes:D2}:{secs:D2}.{cs:D2}"
            : $"{minutes:D2}:{secs:D2}.{cs:D2}";
    }

    /// <summary>Préfixe de <see cref="Notes"/> pour une prise importée depuis des minutages publiés.</summary>
    public const string ProposedTimingNotePrefix = "mp3quran:";

    /// <summary>Prise importée automatiquement et pas encore réenregistrée par l'utilisateur (« مقترح »).</summary>
    [NotMapped]
    public bool IsProposedTiming => !IsValidatedByUser &&
                                    (Notes ?? string.Empty).StartsWith(ProposedTimingNotePrefix, StringComparison.Ordinal);
}

public enum AyahImageStatus
{
    Suggested = 0,         // مقترحة تلقائياً
    Approved = 1,          // الصورة صحيحة (معتمدة)
    Corrected = 2,         // معدلة يدوياً
    NeedsReview = 3        // تحتاج مراجعة
}

[Table("ayah_image_recipes")]
public class AyahImageRecipeEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string AyahId { get; set; } = string.Empty; // e.g. "1:1"
    public int SurahNumber { get; set; }
    public int AyahNumber { get; set; }
    public int PageNumber { get; set; }
    public AyahImageStatus Status { get; set; } = AyahImageStatus.Suggested;
    public int Revision { get; set; } = 1;

    // Relative file paths in workspace
    public string RelativeImagePath { get; set; } = string.Empty;
    public string ImageSha256 { get; set; } = string.Empty;
    public string SourcePageSha256 { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }

    // Edge adjustment deltas in source pixels
    public int MarginTop { get; set; } = 0;
    public int MarginBottom { get; set; } = 0;
    public int MarginLeft { get; set; } = 0;
    public int MarginRight { get; set; } = 0;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(AyahId))]
    public AyahEntity? Ayah { get; set; }
}

public enum CompositionStatus
{
    Draft = 0,
    Ready = 1,
    Rendering = 2,
    Completed = 3,
    Failed = 4,
    NeedsReview = 5
}

[Table("video_compositions")]
public class VideoCompositionEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string AyahId { get; set; } = string.Empty; // e.g. "1:1"
    public int SurahNumber { get; set; }
    public int AyahNumber { get; set; }
    public string ReciterId { get; set; } = "husary";
    public string AudioSourceId { get; set; } = string.Empty;

    public int PassageId { get; set; }
    public int PassageRevision { get; set; }
    public double StartTimeSec { get; set; }
    public double EndTimeSec { get; set; }

    public int ImageRecipeId { get; set; }
    public int ImageRevision { get; set; }
    public string ImagePath { get; set; } = string.Empty;

    public string Orientation { get; set; } = "Horizontal"; // "Horizontal" or "Vertical"
    public CompositionStatus Status { get; set; } = CompositionStatus.Draft;
    public string? ErrorMessage { get; set; }

    public string RelativeVideoPath { get; set; } = string.Empty;
    public string VideoSha256 { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string ManifestPath { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RenderedAtUtc { get; set; }
}

public enum BatchJobStatus
{
    Pending = 0,       // في الانتظار
    Running = 1,       // جارٍ التصدير
    Completed = 2,     // مكتمل
    Failed = 3,        // فشل
    Cancelled = 4,     // ملغى
    Interrupted = 5    // متوقف / مقطوع
}

[Table("batch_export_jobs")]
public class BatchExportJobEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int SurahNumber { get; set; }
    public int AyahNumber { get; set; }
    public string AyahId { get; set; } = string.Empty;
    public string Orientation { get; set; } = "Horizontal";
    public BatchJobStatus Status { get; set; } = BatchJobStatus.Pending;

    // Snapshot of frozen dependencies (L09-02)
    public string AudioSourceId { get; set; } = string.Empty;
    public string AudioPath { get; set; } = string.Empty;
    public int PassageRevision { get; set; }
    public double StartTimeSec { get; set; }
    public double EndTimeSec { get; set; }
    public int ImageRecipeId { get; set; }
    public int ImageRevision { get; set; }
    public string ImagePath { get; set; } = string.Empty;

    // Output & metrics (L09-03)
    public string RelativeVideoPath { get; set; } = string.Empty;
    public string VideoSha256 { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public int ProgressPercent { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public enum BackgroundType
{
    Light = 0,       // fond clair classique (#FFFFFF)
    Dark = 1,        // fond sombre noble (#111827 / #0F172A) avec panneau clair préservant l'ayah
    CustomImage = 2  // image personnelle importée par l'utilisateur
}

public enum WatermarkType
{
    None = 0,
    Text = 1,
    Logo = 2
}

public enum WatermarkPosition
{
    TopRight = 0,
    TopLeft = 1,
    BottomRight = 2,
    BottomLeft = 3
}

public enum UserAssetType
{
    Background = 0,
    Logo = 1
}

[Table("user_assets")]
public class UserAssetEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public UserAssetType AssetType { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

[Table("video_projects")]
public class VideoProjectEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public int SurahNumber { get; set; }
    public int StartAyahNumber { get; set; }
    public int EndAyahNumber { get; set; }
    public string ReciterId { get; set; } = "saber_abdelhakam_warsh";
    public string AudioSourceId { get; set; } = string.Empty;

    public string Orientation { get; set; } = "Horizontal"; // "Horizontal" (16:9) ou "Vertical" (9:16)
    public BackgroundType Background { get; set; } = BackgroundType.Light;
    public string? CustomBackgroundPath { get; set; }

    public WatermarkType Watermark { get; set; } = WatermarkType.None;
    public string? WatermarkText { get; set; }
    public string? WatermarkImagePath { get; set; }
    public WatermarkPosition WatermarkPosition { get; set; } = WatermarkPosition.BottomLeft;
    public double WatermarkOpacity { get; set; } = 0.85;
    public double WatermarkScale { get; set; } = 1.0;
    public bool IsWatermarkEnabled { get; set; } = true;

    public int Revision { get; set; } = 1;
    public string Status { get; set; } = "Draft"; // "Draft", "Ready", "Rendering", "Completed", "Failed"
    public string? OutputVideoPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? LastRenderedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ProjectAyahEntity> SelectedAyahs { get; set; } = new();
}

[Table("project_ayahs")]
public class ProjectAyahEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public int OrderIndex { get; set; }
    public int SurahNumber { get; set; }
    public int AyahNumber { get; set; }
    public string AyahId { get; set; } = string.Empty; // "surah:ayah"

    public int PassageId { get; set; }
    public int PassageRevision { get; set; }
    public double StartTimeSec { get; set; }
    public double EndTimeSec { get; set; }

    public int ImageRecipeId { get; set; }
    public int ImageRevision { get; set; }
    public string RelativeImagePath { get; set; } = string.Empty;

    public bool JunctionReviewed { get; set; } = false;

    [ForeignKey(nameof(ProjectId))]
    public VideoProjectEntity? Project { get; set; }
}

[Table("collections")]
public class CollectionEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<CollectionItemEntity> Items { get; set; } = new();
}

[Table("collection_items")]
public class CollectionItemEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int CollectionId { get; set; }
    public int ProjectId { get; set; }
    public int OrderIndex { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(CollectionId))]
    public CollectionEntity? Collection { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public VideoProjectEntity? Project { get; set; }
}





