using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quran.Application.DTOs;
using Quran.Application.Interfaces;
using Quran.Domain.Entities;
using Quran.Infrastructure.Persistence;

namespace Quran.Infrastructure.Services;

public class MushafImageService : IMushafImageService
{
    private readonly QuranDbContext _db;
    private readonly ILogger<MushafImageService> _logger;
    private readonly string _baseMushafPath;

    public MushafImageService(QuranDbContext db, IConfiguration configuration, ILogger<MushafImageService> logger)
    {
        _db = db;
        _logger = logger;

        var appDir = Directory.GetCurrentDirectory();
        var candidate = Path.Combine(appDir, "..", "..", "data", "mushaf");
        _baseMushafPath = Directory.Exists(candidate)
            ? Path.GetFullPath(candidate)
            : Path.GetFullPath(Path.Combine(appDir, "data", "mushaf"));

        Directory.CreateDirectory(Path.Combine(_baseMushafPath, "masters", "warsh"));
        Directory.CreateDirectory(Path.Combine(_baseMushafPath, "web", "warsh"));
    }

    public async Task<MushafEditionDto> GetOrCreateDefaultEditionAsync(CancellationToken cancellationToken = default)
    {
        var edition = await _db.MushafEditions.FirstOrDefaultAsync(e => e.Code == "MADINAH_WARSH", cancellationToken);
        if (edition == null)
        {
            edition = new MushafEdition
            {
                Code = "MADINAH_WARSH",
                NameArabic = "مصحف المدينة النبوية - رواية ورش عن نافع",
                NameLatin = "Madinah Mushaf - Warsh",
                RiwayaId = 1,
                Publisher = "مجمع الملك فهد لطباعة المصحف الشريف",
                SourceUrl = "https://qurancomplex.gov.sa/",
                IsActive = true
            };
            _db.MushafEditions.Add(edition);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new MushafEditionDto(
            edition.Id,
            edition.Code,
            edition.NameArabic,
            edition.NameLatin,
            edition.RiwayaId,
            edition.Publisher,
            edition.SourceUrl,
            edition.IsActive
        );
    }

    public async Task<MushafPageDto?> GetPageAsync(int pageNumber, int? editionId = null, CancellationToken cancellationToken = default)
    {
        var targetEditionId = editionId ?? await _db.MushafEditions
            .Where(e => e.Code == "MADINAH_WARSH")
            .Select(e => (int?)e.Id).FirstOrDefaultAsync(cancellationToken);
        if (targetEditionId == null) return null;

        var page = await _db.MushafPages
            .FirstOrDefaultAsync(p => p.MushafEditionId == targetEditionId && p.PageNumber == pageNumber, cancellationToken);

        if (page == null)
        {
            return null; // Reading a missing page must never download or create an image.
        }

        return ToPageDto(page);
    }

    public async Task<MushafPageViewerDto> GetPageViewerContextAsync(int pageNumber, int? editionId = null, CancellationToken cancellationToken = default)
    {
        var pageDto = await GetPageAsync(pageNumber, editionId, cancellationToken);
        if (pageDto == null)
            throw new KeyNotFoundException($"Page {pageNumber} introuvable.");

        var regions = await _db.AyahImageRegions
            .Include(r => r.Ayah).ThenInclude(a => a.Surah)
            .Include(r => r.MushafPage)
            .Where(r => r.MushafPageId == pageDto.Id)
            .OrderBy(r => r.Ayah.NumberInQuran)
            .ThenBy(r => r.Sequence)
            .Select(r => new AyahImageRegionDto(
                r.Id,
                r.AyahId,
                r.Ayah.AyahNumber,
                r.Ayah.Surah.Number,
                r.Ayah.Surah.NameArabic,
                r.MushafPageId,
                r.MushafPage.PageNumber,
                r.X,
                r.Y,
                r.Width,
                r.Height,
                r.Sequence,
                r.Status
            ))
            .ToListAsync(cancellationToken);

        var ayahsOnPage = await _db.Ayahs
            .Include(a => a.Surah)
            .Where(a => a.PageNumber == pageNumber)
            .OrderBy(a => a.NumberInQuran)
            .Select(a => new AyahSummaryDto(
                a.Id,
                a.Surah.Number,
                a.Surah.NameArabic,
                a.AyahNumber,
                a.NumberInQuran,
                a.TextWarsh,
                a.PageNumber
            ))
            .ToListAsync(cancellationToken);

        // Si la liste des ayahs sur la page est vide (par ex. pour page 1 ou 2), déduire selon la sourate
        if (!ayahsOnPage.Any())
        {
            if (pageNumber == 1)
            {
                ayahsOnPage = await _db.Ayahs
                    .Include(a => a.Surah)
                    .Where(a => a.Surah.Number == 1)
                    .OrderBy(a => a.AyahNumber)
                    .Select(a => new AyahSummaryDto(a.Id, a.Surah.Number, a.Surah.NameArabic, a.AyahNumber, a.NumberInQuran, a.TextWarsh, 1))
                    .ToListAsync(cancellationToken);
            }
            else if (pageNumber == 2)
            {
                ayahsOnPage = await _db.Ayahs
                    .Include(a => a.Surah)
                    .Where(a => a.Surah.Number == 2 && a.AyahNumber >= 1 && a.AyahNumber <= 5)
                    .OrderBy(a => a.AyahNumber)
                    .Select(a => new AyahSummaryDto(a.Id, a.Surah.Number, a.Surah.NameArabic, a.AyahNumber, a.NumberInQuran, a.TextWarsh, 2))
                    .ToListAsync(cancellationToken);
            }
        }

        return new MushafPageViewerDto(pageDto, regions, ayahsOnPage);
    }

    public async Task<IReadOnlyList<AyahImageRegionDto>> GetAyahRegionsAsync(long ayahId, CancellationToken cancellationToken = default)
    {
        return await _db.AyahImageRegions
            .Include(r => r.Ayah).ThenInclude(a => a.Surah)
            .Include(r => r.MushafPage)
            .Where(r => r.AyahId == ayahId)
            .OrderBy(r => r.Sequence)
            .Select(r => new AyahImageRegionDto(
                r.Id,
                r.AyahId,
                r.Ayah.AyahNumber,
                r.Ayah.Surah.Number,
                r.Ayah.Surah.NameArabic,
                r.MushafPageId,
                r.MushafPage.PageNumber,
                r.X,
                r.Y,
                r.Width,
                r.Height,
                r.Sequence,
                r.Status
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<AyahImageRegionDto> CreateAyahRegionAsync(CreateAyahRegionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateNormalizedBounds(request.X, request.Y, request.Width, request.Height);

        var ayah = await _db.Ayahs.Include(a => a.Surah).FirstOrDefaultAsync(a => a.Id == request.AyahId, cancellationToken);
        if (ayah == null) throw new KeyNotFoundException("Ayah introuvable.");

        var page = await _db.MushafPages.FirstOrDefaultAsync(p => p.Id == request.MushafPageId, cancellationToken);
        if (page == null) throw new KeyNotFoundException("MushafPage introuvable.");

        var region = new AyahImageRegion
        {
            AyahId = request.AyahId,
            MushafPageId = request.MushafPageId,
            X = Math.Round(request.X, 4),
            Y = Math.Round(request.Y, 4),
            Width = Math.Round(request.Width, 4),
            Height = Math.Round(request.Height, 4),
            Sequence = request.Sequence,
            Status = request.Status,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.AyahImageRegions.Add(region);
        await _db.SaveChangesAsync(cancellationToken);

        return new AyahImageRegionDto(
            region.Id,
            region.AyahId,
            ayah.AyahNumber,
            ayah.Surah.Number,
            ayah.Surah.NameArabic,
            region.MushafPageId,
            page.PageNumber,
            region.X,
            region.Y,
            region.Width,
            region.Height,
            region.Sequence,
            region.Status
        );
    }

    public async Task<AyahImageRegionDto> UpdateAyahRegionAsync(long regionId, UpdateAyahRegionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateNormalizedBounds(request.X, request.Y, request.Width, request.Height);

        var region = await _db.AyahImageRegions
            .Include(r => r.Ayah).ThenInclude(a => a.Surah)
            .Include(r => r.MushafPage)
            .FirstOrDefaultAsync(r => r.Id == regionId, cancellationToken);

        if (region == null) throw new KeyNotFoundException($"Région {regionId} introuvable.");

        region.X = Math.Round(request.X, 4);
        region.Y = Math.Round(request.Y, 4);
        region.Width = Math.Round(request.Width, 4);
        region.Height = Math.Round(request.Height, 4);
        region.Sequence = request.Sequence;
        region.Status = request.Status;

        await _db.SaveChangesAsync(cancellationToken);

        return new AyahImageRegionDto(
            region.Id,
            region.AyahId,
            region.Ayah.AyahNumber,
            region.Ayah.Surah.Number,
            region.Ayah.Surah.NameArabic,
            region.MushafPageId,
            region.MushafPage.PageNumber,
            region.X,
            region.Y,
            region.Width,
            region.Height,
            region.Sequence,
            region.Status
        );
    }

    public async Task DeleteAyahRegionAsync(long regionId, CancellationToken cancellationToken = default)
    {
        var region = await _db.AyahImageRegions.FirstOrDefaultAsync(r => r.Id == regionId, cancellationToken);
        if (region == null) return;

        _db.AyahImageRegions.Remove(region);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AudioSegmentImageRegionDto> CreateSegmentImageRegionAsync(CreateSegmentImageRegionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateNormalizedBounds(request.X, request.Y, request.Width, request.Height);

        var segment = await _db.AudioSegments.FirstOrDefaultAsync(s => s.Id == request.AudioSegmentId, cancellationToken);
        if (segment == null) throw new KeyNotFoundException("Segment introuvable.");

        var page = await _db.MushafPages.FirstOrDefaultAsync(p => p.Id == request.MushafPageId, cancellationToken);
        if (page == null) throw new KeyNotFoundException("Page introuvable.");

        var region = new AudioSegmentImageRegion
        {
            AudioSegmentId = request.AudioSegmentId,
            MushafPageId = request.MushafPageId,
            X = Math.Round(request.X, 4),
            Y = Math.Round(request.Y, 4),
            Width = Math.Round(request.Width, 4),
            Height = Math.Round(request.Height, 4),
            Sequence = request.Sequence,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.AudioSegmentImageRegions.Add(region);
        await _db.SaveChangesAsync(cancellationToken);

        return new AudioSegmentImageRegionDto(
            region.Id,
            region.AudioSegmentId,
            region.MushafPageId,
            page.PageNumber,
            region.X,
            region.Y,
            region.Width,
            region.Height,
            region.Sequence
        );
    }

    public async Task<IReadOnlyList<AudioSegmentImageRegionDto>> GetSegmentImageRegionsAsync(Guid segmentId, CancellationToken cancellationToken = default)
    {
        return await _db.AudioSegmentImageRegions
            .Include(r => r.MushafPage)
            .Where(r => r.AudioSegmentId == segmentId)
            .OrderBy(r => r.Sequence)
            .Select(r => new AudioSegmentImageRegionDto(
                r.Id,
                r.AudioSegmentId,
                r.MushafPageId,
                r.MushafPage.PageNumber,
                r.X,
                r.Y,
                r.Width,
                r.Height,
                r.Sequence
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteSegmentImageRegionAsync(long regionId, CancellationToken cancellationToken = default)
    {
        var region = await _db.AudioSegmentImageRegions.FirstOrDefaultAsync(r => r.Id == regionId, cancellationToken);
        if (region == null) return;

        _db.AudioSegmentImageRegions.Remove(region);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<MushafPageDto> ImportPageAsync(int pageNumber, int? editionId = null, CancellationToken cancellationToken = default)
    {
        // The old Archive.org leaf offset is invalid. Never regenerate those pages.
        throw new NotSupportedException("Ancien import Archive.org désactivé : utiliser --import-warsh-pilot avec le paquet versionné.");
    }

    public async Task<int> EnsurePagesImportedAsync(int fromPage, int toPage, int? editionId = null, CancellationToken cancellationToken = default)
    {
        int count = 0;
        for (int p = fromPage; p <= toPage; p++)
        {
            await ImportPageAsync(p, editionId, cancellationToken);
            count++;
        }
        return count;
    }

    private static void ValidateNormalizedBounds(decimal x, decimal y, decimal width, decimal height)
    {
        if (x < 0 || x > 1) throw new ArgumentException("X doit être compris entre 0.0 et 1.0.");
        if (y < 0 || y > 1) throw new ArgumentException("Y doit être compris entre 0.0 et 1.0.");
        if (width <= 0 || width > 1) throw new ArgumentException("Width doit être compris entre 0.0 et 1.0.");
        if (height <= 0 || height > 1) throw new ArgumentException("Height doit être compris entre 0.0 et 1.0.");
        if (x + width > 1.01m) throw new ArgumentException("X + Width dépasse la largeur de la page.");
        if (y + height > 1.01m) throw new ArgumentException("Y + Height dépasse la hauteur de la page.");
    }

    private static MushafPageDto ToPageDto(MushafPage p)
    {
        var publicUrl = "/" + p.WebStorageKey.Replace('\\', '/').TrimStart('/');
        return new MushafPageDto(
            p.Id,
            p.MushafEditionId,
            p.PageNumber,
            publicUrl,
            p.MasterStorageKey,
            p.WebStorageKey,
            p.Width,
            p.Height,
            p.ChecksumSha256
        );
    }
}
