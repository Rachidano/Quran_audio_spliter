using Quran.Domain.Entities;
using Quran.Domain.Enums;
using Quran.Infrastructure.Persistence;
namespace Quran.Infrastructure.Services;

internal static class CanonicalAudioQueries
{
    internal static IQueryable<CorpusAyah> References(QuranDbContext db) =>
        db.CorpusAyahs.Where(a => a.Corpus!.Code == "marw-newMushaf");

    internal static IQueryable<AudioSegment> Segments(QuranDbContext db) =>
        db.AudioSegments.Where(s => s.Edition == MarwPilotCorpus.EditionCode);

    internal static IQueryable<AudioSegment> Published(QuranDbContext db) =>
        Segments(db).Where(s => s.Status == AudioSegmentStatus.Published && s.PublishedAudioAsset != null
            && (s.Wajh == null || s.Wajh.ReviewStatus == "Approved"));

    internal static IQueryable<string?> FullAyahReferences(QuranDbContext db) =>
        Published(db).Where(s => s.SegmentType == AudioSegmentType.FullAyah).Select(s => s.Reference).Distinct();
}
