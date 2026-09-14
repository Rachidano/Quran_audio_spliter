namespace Quran.Application.DTOs;

public record WajhDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameArabic { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int SortOrder { get; init; }
    public string RouteCode { get; init; } = string.Empty;
    public string? TransmissionRouteName { get; init; }
    public string Category { get; init; } = "Tahrirat";
    public string? RuleGroup { get; init; }
    public string? ExcludedScope { get; init; }
    public int? SourceBranch { get; init; }
    public string? SourceBooks { get; init; }
    public string ReviewStatus { get; init; } = "PendingReview";
    public string? ReviewerUserId { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public string? RejectionReason { get; init; }
    public string SourceCitation { get; init; } = string.Empty;
    public string DefinitionJson { get; init; } = "{}";
    public int Version { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public int UsageCount { get; init; }
    public List<WajhComponentDto> Components { get; init; } = new();
    public List<WajhScopeDto> Scopes { get; init; } = new();
    public List<EvidenceCitationDto> Citations { get; init; } = new();
    public List<WajhRevisionDto> Revisions { get; init; } = new();
}

public record WajhComponentDto(string ComponentKey, string ValueArabic, int? ValueNumeric = null);

public record WajhScopeDto(int? SourceSurah, int? SourceAyahStart, int? SourceAyahEnd, string MarwMappingStatus = "NotMapped", bool AutomaticAssignment = false, string? ContextDescription = null);

public record EvidenceCitationDto(int ReferenceSourceId, string SourceCode, string SourceTitle, string? Locator = null, string? QuoteOrNote = null);

public record WajhRevisionDto(int RevisionNumber, string NameArabic, string ReviewStatus, string? ChangedByUserId, DateTimeOffset ChangedAt, string? ChangeReason);

public record CreateWajhDto(
    string Code,
    string NameArabic,
    string RouteCode,
    string? Category,
    string? RuleGroup,
    string Description,
    string? ExcludedScope,
    string SourceCitation,
    List<WajhComponentDto>? Components = null,
    List<WajhScopeDto>? Scopes = null,
    List<int>? ReferenceSourceIds = null
);

public record UpdateWajhDto(
    string NameArabic,
    string RouteCode,
    string? Category,
    string? RuleGroup,
    string Description,
    string? ExcludedScope,
    string SourceCitation,
    int Version,
    string? ChangeReason = null,
    List<WajhComponentDto>? Components = null,
    List<WajhScopeDto>? Scopes = null,
    List<int>? ReferenceSourceIds = null
);

public record ReviewWajhDto(
    string Action, // "Submit", "Approve", "Reject", "Archive"
    string? Reason = null
);

public record WajhUsageReportDto(
    int WajhId,
    string Code,
    string NameArabic,
    string ReviewStatus,
    int TotalUsages,
    int AudioSegmentsCount,
    int RecitationsCount,
    int MushafPassagesCount,
    List<AudioSegmentUsageDto> AudioSegments,
    List<RecitationUsageDto> Recitations
);

public record AudioSegmentUsageDto(Guid Id, string? Reference, long StartMs, long EndMs, string Status, string? Edition, Guid SourceAudioId);
public record RecitationUsageDto(long Id, int SurahNumber, int AyahNumber, string Status, bool IsPublished);

public record TransmissionRouteDto(int Id, string Code, string NameArabic, string Riwaya, string Tariq, string? SubTariq, string CoverageStatus, int AwjuhCount);

public record ReferenceSourceDto(int Id, string SourceCode, string Title, string? Author, string? Locator, string? Url, string? Sha256, string Kind);

public record CombinationConstraintDto(
    int Id,
    string Code,
    string RouteCode,
    string NameArabic,
    string ContextArabic,
    string ConditionJson,
    string EffectJson,
    string ExplanationArabic,
    string ReviewStatus,
    bool AutomaticEnforcement,
    string MarwMappingStatus
);

public record DocumentaryCoverageEntryDto(
    int Id,
    string TransmissionRouteCode,
    string RouteName,
    string SourceWorkTitle,
    string ChapterOrSection,
    int? SurahNumber,
    string ScopeContext,
    string AnalysisStatus,
    int EntriesCount,
    int ConstraintsCount,
    string? Notes,
    DateTimeOffset? LastReviewedAt
);

public record UpdateCoverageEntryDto(
    int Id,
    string AnalysisStatus,
    string? Notes = null
);

public record AwjuhSearchFilter(
    string? RouteCode = null,
    string? RuleGroup = null,
    string? Status = null,
    int? SurahNumber = null,
    string? Query = null,
    bool? HasUsages = null
);
