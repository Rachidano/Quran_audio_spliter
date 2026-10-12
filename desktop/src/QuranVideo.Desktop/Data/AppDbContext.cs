using Microsoft.EntityFrameworkCore;

namespace QuranVideo.Desktop.Data;

public class AppDbContext : DbContext
{
    private readonly string _dbPath;

    public DbSet<MushafEditionEntity> Editions => Set<MushafEditionEntity>();
    public DbSet<SurahEntity> Surahs => Set<SurahEntity>();
    public DbSet<AyahEntity> Ayahs => Set<AyahEntity>();
    public DbSet<MushafPageEntity> Pages => Set<MushafPageEntity>();
    public DbSet<AyahRegionEntity> Regions => Set<AyahRegionEntity>();
    public DbSet<AyahTextEntity> Texts => Set<AyahTextEntity>();
    public DbSet<ReciterEntity> Reciters => Set<ReciterEntity>();
    public DbSet<RecitationCollectionEntity> RecitationCollections => Set<RecitationCollectionEntity>();
    public DbSet<AudioSourceEntity> AudioSources => Set<AudioSourceEntity>();
    public DbSet<AyahPassageEntity> Passages => Set<AyahPassageEntity>();
    public DbSet<AyahImageRecipeEntity> ImageRecipes => Set<AyahImageRecipeEntity>();
    public DbSet<VideoCompositionEntity> Compositions => Set<VideoCompositionEntity>();
    public DbSet<BatchExportJobEntity> BatchJobs => Set<BatchExportJobEntity>();

    public AppDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AyahEntity>()
            .HasIndex(a => new { a.SurahNumber, a.AyahNumber })
            .IsUnique();

        modelBuilder.Entity<AyahEntity>()
            .HasIndex(a => a.VerseNo)
            .IsUnique();

        modelBuilder.Entity<AyahRegionEntity>()
            .HasIndex(r => new { r.AyahId, r.OrderIndex });

        modelBuilder.Entity<AyahTextEntity>()
            .HasIndex(t => new { t.CorpusId, t.AyahId })
            .IsUnique();

        modelBuilder.Entity<AyahPassageEntity>()
            .HasIndex(p => new { p.AudioSourceId, p.SurahNumber, p.AyahNumber });

        modelBuilder.Entity<AyahImageRecipeEntity>()
            .HasIndex(r => r.AyahId);

        modelBuilder.Entity<VideoCompositionEntity>()
            .HasIndex(c => new { c.SurahNumber, c.AyahNumber, c.Orientation });

        modelBuilder.Entity<BatchExportJobEntity>()
            .HasIndex(j => new { j.Status, j.CreatedAtUtc });
    }
}
