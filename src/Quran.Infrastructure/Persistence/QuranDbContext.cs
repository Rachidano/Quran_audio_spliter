using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Quran.Domain.Entities;
using Quran.Infrastructure.Identity;

namespace Quran.Infrastructure.Persistence;

public class QuranDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public QuranDbContext(DbContextOptions<QuranDbContext> options) : base(options)
    {
    }

    public DbSet<Surah> Surahs => Set<Surah>();
    public DbSet<Ayah> Ayahs => Set<Ayah>();
    public DbSet<Reciter> Reciters => Set<Reciter>();
    public DbSet<Riwaya> Riwayas => Set<Riwaya>();
    public DbSet<Wajh> Awjuh => Set<Wajh>();
    public DbSet<MushafPassage> MushafPassages => Set<MushafPassage>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<AudioAsset> AudioAssets => Set<AudioAsset>();
    public DbSet<Recitation> Recitations => Set<Recitation>();
    public DbSet<ValidationReview> ValidationReviews => Set<ValidationReview>();
    public DbSet<MushafEdition> MushafEditions => Set<MushafEdition>();
    public DbSet<MushafPage> MushafPages => Set<MushafPage>();
    public DbSet<AyahImageRegion> AyahImageRegions => Set<AyahImageRegion>();
    public DbSet<AudioSegmentImageRegion> AudioSegmentImageRegions => Set<AudioSegmentImageRegion>();
    public DbSet<SourceAudio> SourceAudios => Set<SourceAudio>();
    public DbSet<AyahWord> AyahWords => Set<AyahWord>();
    public DbSet<AudioSegment> AudioSegments => Set<AudioSegment>();
    public DbSet<QuranCorpus> QuranCorpora => Set<QuranCorpus>();
    public DbSet<CorpusAyah> CorpusAyahs => Set<CorpusAyah>();
    public DbSet<ReferenceMapping> ReferenceMappings => Set<ReferenceMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<MushafPassage>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Version).IsConcurrencyToken();
            entity.HasIndex(p => new { p.Edition, p.SourceHash, p.Reference });
            entity.HasOne(p => p.Wajh).WithMany().HasForeignKey(p => p.WajhId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MushafPassage>().WithMany().HasForeignKey(p => p.RepeatOfId).OnDelete(DeleteBehavior.Restrict);
        });

        // Surah
        modelBuilder.Entity<Surah>(entity =>
        {
            entity.ToTable("Surahs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Number).IsUnique();
            entity.Property(e => e.NameArabic).IsRequired().HasMaxLength(100);
            entity.Property(e => e.NameLatin).IsRequired().HasMaxLength(100);
            entity.Property(e => e.NameTranslation).HasMaxLength(200);
        });

        // Ayah
        modelBuilder.Entity<Ayah>(entity =>
        {
            entity.ToTable("Ayahs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SurahId, e.AyahNumber }).IsUnique();
            entity.HasIndex(e => e.NumberInQuran);
            entity.HasIndex(e => e.SearchText);
            entity.Property(e => e.TextWarsh).IsRequired();
            entity.Property(e => e.SearchText).IsRequired();

            entity.HasOne(e => e.Surah)
                  .WithMany(s => s.Ayahs)
                  .HasForeignKey(e => e.SurahId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Reciter
        modelBuilder.Entity<Reciter>(entity =>
        {
            entity.ToTable("Reciters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NameArabic).IsRequired().HasMaxLength(150);
            entity.Property(e => e.NameLatin).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        // Riwaya
        modelBuilder.Entity<Riwaya>(entity =>
        {
            entity.ToTable("Riwayas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NameArabic).IsRequired().HasMaxLength(150);
            entity.Property(e => e.NameLatin).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        // Wajh
        modelBuilder.Entity<Wajh>(entity =>
        {
            entity.ToTable("Awjuh");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.NameArabic).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Version).IsConcurrencyToken();
        });

        // Source
        modelBuilder.Entity<Source>(entity =>
        {
            entity.ToTable("Sources");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        // AudioAsset
        modelBuilder.Entity<AudioAsset>(entity =>
        {
            entity.ToTable("AudioAssets");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Checksum);
            entity.Property(e => e.StorageKey).IsRequired().HasMaxLength(500);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.MimeType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Checksum).HasMaxLength(100);

            entity.HasOne(e => e.Source)
                  .WithMany(s => s.AudioAssets)
                  .HasForeignKey(e => e.SourceId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Recitation
        modelBuilder.Entity<Recitation>(entity =>
        {
            entity.ToTable("Recitations");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AyahId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.IsPublished);

            entity.HasOne(e => e.Ayah)
                  .WithMany(a => a.Recitations)
                  .HasForeignKey(e => e.AyahId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Reciter)
                  .WithMany(r => r.Recitations)
                  .HasForeignKey(e => e.ReciterId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Riwaya)
                  .WithMany(r => r.Recitations)
                  .HasForeignKey(e => e.RiwayaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Wajh)
                  .WithMany(w => w.Recitations)
                  .HasForeignKey(e => e.WajhId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.AudioAsset)
                  .WithMany(a => a.Recitations)
                  .HasForeignKey(e => e.AudioAssetId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ValidationReview
        modelBuilder.Entity<ValidationReview>(entity =>
        {
            entity.ToTable("ValidationReviews");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ValidatorUserId).HasMaxLength(100);
            entity.Property(e => e.Comment).HasMaxLength(1000);

            entity.HasOne(e => e.Recitation)
                  .WithMany(r => r.ValidationReviews)
                  .HasForeignKey(e => e.RecitationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AyahWord
        modelBuilder.Entity<AyahWord>(entity =>
        {
            entity.ToTable("AyahWords");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.AyahId, e.WordIndex }).IsUnique();
            entity.Property(e => e.TextWarsh).IsRequired().HasMaxLength(150);
            entity.Property(e => e.SearchText).HasMaxLength(150);

            entity.HasOne(e => e.Ayah)
                  .WithMany(a => a.Words)
                  .HasForeignKey(e => e.AyahId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // SourceAudio
        modelBuilder.Entity<SourceAudio>(entity =>
        {
            entity.ToTable("SourceAudios");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.StorageKey).IsUnique();
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(300);
            entity.Property(e => e.StorageKey).IsRequired().HasMaxLength(500);
            entity.Property(e => e.OriginalUrl).HasMaxLength(1000);
            entity.Property(e => e.ChecksumSha256).HasMaxLength(64);
            entity.Property(e => e.Codec).HasMaxLength(20);

            entity.HasOne(e => e.Surah)
                  .WithMany()
                  .HasForeignKey(e => e.SurahId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Reciter)
                  .WithMany()
                  .HasForeignKey(e => e.ReciterId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Riwaya)
                  .WithMany()
                  .HasForeignKey(e => e.RiwayaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // AudioSegment
        modelBuilder.Entity<AudioSegment>(entity =>
        {
            entity.ToTable("AudioSegments");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SourceAudioId);
            entity.HasIndex(e => e.AyahId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.SourceAudioId, e.StartMs });
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UpdatedByUserId).HasMaxLength(100);
            entity.Property(e => e.ValidatedByUserId).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.RowVersion).IsConcurrencyToken();

            entity.HasOne(e => e.SourceAudio)
                  .WithMany(s => s.AudioSegments)
                  .HasForeignKey(e => e.SourceAudioId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Ayah)
                  .WithMany(a => a.AudioSegments)
                  .HasForeignKey(e => e.AyahId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Wajh)
                  .WithMany()
                  .HasForeignKey(e => e.WajhId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.PublishedAudioAsset)
                  .WithMany()
                  .HasForeignKey(e => e.PublishedAudioAssetId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // MushafEdition
        modelBuilder.Entity<MushafEdition>(entity =>
        {
            entity.ToTable("MushafEditions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.NameArabic).IsRequired().HasMaxLength(150);
            entity.Property(e => e.NameLatin).HasMaxLength(150);
            entity.Property(e => e.Publisher).HasMaxLength(200);
            entity.Property(e => e.SourceUrl).HasMaxLength(500);
            entity.Property(e => e.Revision).HasMaxLength(50);

            entity.HasOne(e => e.Riwaya)
                  .WithMany()
                  .HasForeignKey(e => e.RiwayaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Corpus)
                  .WithMany(c => c.Editions)
                  .HasForeignKey(e => e.CorpusId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // QuranCorpus
        modelBuilder.Entity<QuranCorpus>(entity =>
        {
            entity.ToTable("QuranCorpora");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(100);
            entity.Property(e => e.NameArabic).IsRequired().HasMaxLength(150);
            entity.Property(e => e.NameLatin).HasMaxLength(150);
            entity.Property(e => e.RiwayaCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CountingSystem).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TextualRevision).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Provenance).IsRequired().HasMaxLength(500);
            entity.Property(e => e.QualificationStatus).IsRequired().HasMaxLength(50);
        });

        // CorpusAyah
        modelBuilder.Entity<CorpusAyah>(entity =>
        {
            entity.ToTable("CorpusAyahs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CorpusId, e.SurahNumber, e.AyahNumber }).IsUnique();
            entity.HasIndex(e => new { e.CorpusId, e.NumberInQuran });
            entity.Property(e => e.TextArabic).IsRequired();
            entity.Property(e => e.SearchText).IsRequired();

            entity.HasOne(e => e.Corpus)
                  .WithMany(c => c.Ayahs)
                  .HasForeignKey(e => e.CorpusId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ReferenceMapping
        modelBuilder.Entity<ReferenceMapping>(entity =>
        {
            entity.ToTable("ReferenceMappings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SourceCorpusId, e.SourceReference });
            entity.HasIndex(e => new { e.TargetCorpusId, e.TargetReference });
            entity.Property(e => e.SourceReference).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TargetReference).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Cardinality).IsRequired().HasMaxLength(30);
            entity.Property(e => e.ReviewStatus).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DifferenceDescription).HasMaxLength(1000);
            entity.Property(e => e.ProofNotes).HasMaxLength(1000);
            entity.Property(e => e.ReviewedByUserId).HasMaxLength(100);

            entity.HasOne(e => e.SourceCorpus)
                  .WithMany()
                  .HasForeignKey(e => e.SourceCorpusId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetCorpus)
                  .WithMany()
                  .HasForeignKey(e => e.TargetCorpusId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // MushafPage
        modelBuilder.Entity<MushafPage>(entity =>
        {
            entity.ToTable("MushafPages");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MushafEditionId, e.PageNumber }).IsUnique();
            entity.Property(e => e.MasterStorageKey).IsRequired().HasMaxLength(500);
            entity.Property(e => e.WebStorageKey).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ChecksumSha256).HasMaxLength(64);

            entity.HasOne(e => e.MushafEdition)
                  .WithMany(m => m.Pages)
                  .HasForeignKey(e => e.MushafEditionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AyahImageRegion
        modelBuilder.Entity<AyahImageRegion>(entity =>
        {
            entity.ToTable("AyahImageRegions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AyahId);
            entity.HasIndex(e => e.MushafPageId);
            entity.Property(e => e.X).HasPrecision(8, 5);
            entity.Property(e => e.Y).HasPrecision(8, 5);
            entity.Property(e => e.Width).HasPrecision(8, 5);
            entity.Property(e => e.Height).HasPrecision(8, 5);

            entity.HasOne(e => e.Ayah)
                  .WithMany(a => a.ImageRegions)
                  .HasForeignKey(e => e.AyahId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MushafPage)
                  .WithMany(p => p.AyahImageRegions)
                  .HasForeignKey(e => e.MushafPageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AudioSegmentImageRegion
        modelBuilder.Entity<AudioSegmentImageRegion>(entity =>
        {
            entity.ToTable("AudioSegmentImageRegions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AudioSegmentId);
            entity.HasIndex(e => e.MushafPageId);
            entity.Property(e => e.X).HasPrecision(8, 5);
            entity.Property(e => e.Y).HasPrecision(8, 5);
            entity.Property(e => e.Width).HasPrecision(8, 5);
            entity.Property(e => e.Height).HasPrecision(8, 5);

            entity.HasOne(e => e.AudioSegment)
                  .WithMany(s => s.SegmentImageRegions)
                  .HasForeignKey(e => e.AudioSegmentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MushafPage)
                  .WithMany(p => p.SegmentImageRegions)
                  .HasForeignKey(e => e.MushafPageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
