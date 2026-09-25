using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Infrastructure.Persistence;

namespace Quran.Infrastructure.Services;

public class CorpusResolutionService : ICorpusResolutionService
{
    private readonly QuranDbContext _db;
    private readonly ILogger<CorpusResolutionService> _logger;
    private readonly MarwPilotCorpus? _marwCorpus;

    public const string HistoricalCorpusCode = "warsh-pilot-historical";
    public const string MarwCorpusCode = "marw-newMushaf";

    public CorpusResolutionService(
        QuranDbContext db,
        ILogger<CorpusResolutionService> logger,
        MarwPilotCorpus? marwCorpus = null)
    {
        _db = db;
        _logger = logger;
        _marwCorpus = marwCorpus;
    }

    public async Task<QuranCorpus?> GetCorpusByCodeAsync(string code, CancellationToken ct = default)
    {
        return await _db.QuranCorpora
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == code, ct);
    }

    public async Task<IReadOnlyList<QuranCorpus>> GetAllCorporaAsync(CancellationToken ct = default)
    {
        return await _db.QuranCorpora
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
    }

    public async Task<CorpusAyah?> ResolveAyahAsync(string corpusCode, int surahNumber, int ayahNumber, CancellationToken ct = default)
    {
        return await _db.CorpusAyahs
            .Include(a => a.Corpus)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Corpus!.Code == corpusCode && a.SurahNumber == surahNumber && a.AyahNumber == ayahNumber, ct);
    }

    public async Task<ReferenceMappingResult> ResolveMappingAsync(string sourceCorpusCode, string targetCorpusCode, int surahNumber, int ayahNumber, CancellationToken ct = default)
    {
        var sourceRef = $"{surahNumber}:{ayahNumber}";

        var mapping = await _db.ReferenceMappings
            .Include(m => m.SourceCorpus)
            .Include(m => m.TargetCorpus)
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.SourceCorpus!.Code == sourceCorpusCode &&
                m.TargetCorpus!.Code == targetCorpusCode &&
                m.SurahNumber == surahNumber &&
                m.SourceReference == sourceRef, ct);

        if (mapping != null)
        {
            return new ReferenceMappingResult
            {
                IsMapped = mapping.ReviewStatus == "Approved",
                SourceCorpusCode = sourceCorpusCode,
                SourceReference = sourceRef,
                TargetCorpusCode = targetCorpusCode,
                TargetReference = mapping.TargetReference,
                Cardinality = mapping.Cardinality,
                DifferenceDescription = mapping.DifferenceDescription,
                ReviewStatus = mapping.ReviewStatus,
                ProofNotes = mapping.ProofNotes
            };
        }

        // Strict: Never automatically assume 1:1 mapping between different counting systems!
        return new ReferenceMappingResult
        {
            IsMapped = false,
            SourceCorpusCode = sourceCorpusCode,
            SourceReference = sourceRef,
            TargetCorpusCode = targetCorpusCode,
            TargetReference = null,
            Cardinality = "Unmapped",
            DifferenceDescription = $"لم يتم توثيق المطابقة بين المرجع {sourceRef} في {sourceCorpusCode} والمصحف {targetCorpusCode}.",
            ReviewStatus = "PendingReview",
            ProofNotes = "مطابقة غير محسومة تحتاج إلى مراجعة بشرية."
        };
    }

    public async Task<IReadOnlyList<ReferenceMapping>> GetMappingsForSurahAsync(int surahNumber, string? sourceCorpus = null, string? targetCorpus = null, CancellationToken ct = default)
    {
        var query = _db.ReferenceMappings
            .Include(m => m.SourceCorpus)
            .Include(m => m.TargetCorpus)
            .AsNoTracking()
            .Where(m => m.SurahNumber == surahNumber);

        if (!string.IsNullOrEmpty(sourceCorpus))
        {
            query = query.Where(m => m.SourceCorpus!.Code == sourceCorpus);
        }

        if (!string.IsNullOrEmpty(targetCorpus))
        {
            query = query.Where(m => m.TargetCorpus!.Code == targetCorpus);
        }

        return await query.OrderBy(m => m.SourceReference).ToListAsync(ct);
    }

    public async Task<ReferenceMapping> SaveMappingAsync(ReferenceMapping mapping, CancellationToken ct = default)
    {
        var local = _db.ReferenceMappings.Local.FirstOrDefault(e => e.Id == mapping.Id);
        if (local != null)
        {
            _db.Entry(local).CurrentValues.SetValues(mapping);
        }
        else if (mapping.Id == 0)
        {
            _db.ReferenceMappings.Add(mapping);
        }
        else
        {
            _db.ReferenceMappings.Update(mapping);
        }

        await _db.SaveChangesAsync(ct);
        return mapping;
    }

    public async Task EnsureCorporaInitializedAsync(CancellationToken ct = default)
    {
        // 1. Ensure Historical Corpus exists
        var historicalCorpus = await _db.QuranCorpora.FirstOrDefaultAsync(c => c.Code == HistoricalCorpusCode, ct);
        if (historicalCorpus == null)
        {
            historicalCorpus = new QuranCorpus
            {
                Code = HistoricalCorpusCode,
                NameArabic = "مصحف ورش (الترقيم التاريخي الأولي)",
                NameLatin = "Warsh Pilot Historical",
                RiwayaCode = "warsh",
                CountingSystem = "kufi-initial",
                TotalAyahs = 6214,
                TextualRevision = "1.0",
                Provenance = "Initial database seed from data/reference",
                QualificationStatus = "HistoricalDraft",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.QuranCorpora.Add(historicalCorpus);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Created corpus {Code}", historicalCorpus.Code);
        }

        // 2. Ensure MARW Corpus exists
        var marwCorpusEntity = await _db.QuranCorpora.FirstOrDefaultAsync(c => c.Code == MarwCorpusCode, ct);
        if (marwCorpusEntity == null)
        {
            marwCorpusEntity = new QuranCorpus
            {
                Code = MarwCorpusCode,
                NameArabic = "مصحف ورش - مصحف الجزائر الإلكتروني (MARW)",
                NameLatin = "Warsh MARW newMushaf",
                RiwayaCode = "warsh",
                CountingSystem = "madani-marw",
                TotalAyahs = 6214,
                TextualRevision = MarwPilotCorpus.Revision,
                Provenance = $"App_Data/marw-newMushaf/{MarwPilotCorpus.Revision}",
                QualificationStatus = "ImagesImportedTextPending",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.QuranCorpora.Add(marwCorpusEntity);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Created corpus {Code}", marwCorpusEntity.Code);
        }

        // 3. Populate Historical CorpusAyahs from existing Ayahs if empty
        var hasHistoricalAyahs = await _db.CorpusAyahs.AnyAsync(a => a.CorpusId == historicalCorpus.Id, ct);
        if (!hasHistoricalAyahs)
        {
            var existingAyahs = await _db.Ayahs
                .Include(a => a.Surah)
                .OrderBy(a => a.NumberInQuran)
                .ToListAsync(ct);

            if (existingAyahs.Count > 0)
            {
                var corpusAyahs = existingAyahs.Select(a => new CorpusAyah
                {
                    CorpusId = historicalCorpus.Id,
                    SurahNumber = a.Surah!.Number,
                    AyahNumber = a.AyahNumber,
                    NumberInQuran = a.NumberInQuran,
                    TextArabic = a.TextWarsh,
                    SearchText = a.SearchText,
                    PageNumberHint = a.PageNumber ?? 0
                }).ToList();

                await _db.CorpusAyahs.AddRangeAsync(corpusAyahs, ct);
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Populated {Count} CorpusAyahs for {Code}", corpusAyahs.Count, historicalCorpus.Code);
            }
        }

        // 4. Populate MARW CorpusAyahs if empty and MarwPilotCorpus is available
        var hasMarwAyahs = await _db.CorpusAyahs.AnyAsync(a => a.CorpusId == marwCorpusEntity.Id, ct);
        if (!hasMarwAyahs && _marwCorpus != null)
        {
            var marwAyahs = new List<CorpusAyah>();
            var seenReferences = new HashSet<string>();
            int numberInQuran = 1;

            foreach (var pageNum in _marwCorpus.PageNumbers)
            {
                var page = _marwCorpus.GetPage(pageNum);
                if (page == null) continue;

                foreach (var region in page.Regions)
                {
                    var refKey = $"{region.SurahNumber}:{region.AyahNumber}";
                    if (seenReferences.Add(refKey))
                    {
                        marwAyahs.Add(new CorpusAyah
                        {
                            CorpusId = marwCorpusEntity.Id,
                            SurahNumber = region.SurahNumber,
                            AyahNumber = region.AyahNumber,
                            NumberInQuran = numberInQuran++,
                            TextArabic = $"[نص مصحف ورش المصور {refKey}]",
                            SearchText = $"سورة {region.SurahNumber} آية {region.AyahNumber}",
                            PageNumberHint = pageNum
                        });
                    }
                }
            }

            if (marwAyahs.Count > 0)
            {
                await _db.CorpusAyahs.AddRangeAsync(marwAyahs, ct);
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Populated {Count} CorpusAyahs for {Code}", marwAyahs.Count, marwCorpusEntity.Code);
            }
        }

        // 5. Seed explicit ReferenceMappings for Al-Baqarah (Surah 2) 2:1 to 2:5
        var hasMappings = await _db.ReferenceMappings
            .AnyAsync(m => m.SourceCorpusId == historicalCorpus.Id && m.TargetCorpusId == marwCorpusEntity.Id && m.SurahNumber == 2, ct);

        if (!hasMappings)
        {
            var mappings = new List<ReferenceMapping>
            {
                new()
                {
                    SourceCorpusId = historicalCorpus.Id,
                    SourceReference = "2:1",
                    TargetCorpusId = marwCorpusEntity.Id,
                    TargetReference = "2:1",
                    SurahNumber = 2,
                    Cardinality = "ManyToOne",
                    DifferenceDescription = "الٓمٓ ليست آية مستقلة في العد المدني المعتمد في مصحف ورش MARW؛ تندرج مع بداية الآية الأولى حتى (هُدًى لِّلْمُتَّقِينَ).",
                    ReviewStatus = "PendingReview",
                    ReviewedByUserId = null,
                    ReviewedAt = null,
                    ProofNotes = "فروق العد المدني الأول/الأخير مقابل العد الكوفي: الٓمٓ لا تعد آية برأسها في البقرة في رواية ورش طبقاً للمصحف المغربي المعتمد."
                },
                new()
                {
                    SourceCorpusId = historicalCorpus.Id,
                    SourceReference = "2:2",
                    TargetCorpusId = marwCorpusEntity.Id,
                    TargetReference = "2:1",
                    SurahNumber = 2,
                    Cardinality = "ManyToOne",
                    DifferenceDescription = "النص (ذَٰلِكَ ٱلْكِتَٰبُ لَا رَيْبَ فِيهِ هُدًى لِّلْمُتَّقِينَ) يمثل عجز الآية 1 في مصحف MARW.",
                    ReviewStatus = "PendingReview",
                    ReviewedByUserId = null,
                    ReviewedAt = null,
                    ProofNotes = "رأس الآية 1 في MARW ينتهي عند (هُدًى لِّلْمُتَّقِينَ)."
                },
                new()
                {
                    SourceCorpusId = historicalCorpus.Id,
                    SourceReference = "2:3",
                    TargetCorpusId = marwCorpusEntity.Id,
                    TargetReference = "2:2",
                    SurahNumber = 2,
                    Cardinality = "OneToOne",
                    DifferenceDescription = "إزاحة بمقدار 1: نص (ٱلَّذِينَ يُؤْمِنُونَ بِٱلْغَيْبِ... يُنفِقُونَ) هو الآية 2 في مصحف MARW.",
                    ReviewStatus = "PendingReview",
                    ReviewedByUserId = null,
                    ReviewedAt = null,
                    ProofNotes = "تطابق نصي مباشر مع إزاحة الترقيم بسبب دمج الآيتين 1 و2 السابقتين."
                },
                new()
                {
                    SourceCorpusId = historicalCorpus.Id,
                    SourceReference = "2:4",
                    TargetCorpusId = marwCorpusEntity.Id,
                    TargetReference = "2:3",
                    SurahNumber = 2,
                    Cardinality = "OneToOne",
                    DifferenceDescription = "إزاحة بمقدار 1: نص (وَٱلَّذِينَ يُؤْمِنُونَ بِمَآ أُنزِلَ إِلَيْكَ... يُوقِنُونَ) هو الآية 3 في مصحف MARW.",
                    ReviewStatus = "PendingReview",
                    ReviewedByUserId = null,
                    ReviewedAt = null,
                    ProofNotes = "تطابق نصي تام مع اختلاف رقم الآية (تاريخي 4 مقابل MARW 3)."
                },
                new()
                {
                    SourceCorpusId = historicalCorpus.Id,
                    SourceReference = "2:5",
                    TargetCorpusId = marwCorpusEntity.Id,
                    TargetReference = "2:4",
                    SurahNumber = 2,
                    Cardinality = "OneToOne",
                    DifferenceDescription = "إزاحة بمقدار 1: نص (أُوْلَٰٓئِكَ عَلَىٰ هُدًى مِّن رَّبِّهِمْۖ وَأُوْلَٰٓئِكَ هُمُ ٱلْمُفْلِحُونَ) هو الآية 4 في مصحف MARW.",
                    ReviewStatus = "PendingReview",
                    ReviewedByUserId = null,
                    ReviewedAt = null,
                    ProofNotes = "الآية 4 في MARW تنتهي بـ (ٱلْمُفْلِحُونَ)، والتي كانت تقابل تاريخياً الآية 5."
                }
            };

            await _db.ReferenceMappings.AddRangeAsync(mappings, ct);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded {Count} explicit ReferenceMappings for Al-Baqarah (2:1 to 2:5)", mappings.Count);
        }
    }
}
