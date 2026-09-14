namespace Quran.Application.DTOs;

public enum QualityAnomalySeverity
{
    Info,
    Warning,
    Error
}

public enum QualityAnomalyType
{
    Gap,                    // Intervalle non annoté > seuil
    Overlap,                // Chevauchement temporel
    MissingMediaFile,       // Asset déclaré en base mais absent sur le disque
    StalePublication,       // Publication pointant vers un segment non publié ou dépublié
    UnmappedReference,      // Référence non résolue ou ambiguë
    MissingGeometryRegion   // Ayah sans région vectorielle MARW
}

public record QualityAnomalyDto(
    QualityAnomalySeverity Severity,
    QualityAnomalyType Type,
    int SurahNumber,
    string? AyahReference,
    string Description,
    string Details,
    string SuggestedActionUrl,
    string ActionLabel
);

public record SurahQualityCoverageDto(
    int SurahNumber,
    string SurahNameArabic,
    string SurahNameLatin,
    int TotalAyahs,
    int CoveredAyahs,
    int PublishedAyahs,
    int DraftSegments,
    int ReviewedSegments,
    int ApprovedSegments,
    int PublishedSegments,
    bool HasSourceAudio,
    long SourceSizeBytes,
    double SourceDurationSeconds,
    int AnomalyCount,
    double CoveragePercentage
);

public record GlobalQualityReportDto(
    int TotalSurahs,
    int TotalAyahs,
    int TotalCoveredAyahs,
    int TotalPublishedAyahs,
    double GlobalAyahCoveragePercentage,
    int SurahsWithSourceAudio,
    int TotalSegments,
    int TotalPublishedSegments,
    int TotalGaps,
    int TotalOverlaps,
    int TotalMissingMedia,
    int TotalUnmappedReferences,
    IReadOnlyList<QualityAnomalyDto> Anomalies,
    IReadOnlyList<SurahQualityCoverageDto> SurahCoverages
);
