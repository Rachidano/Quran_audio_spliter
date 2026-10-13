using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using Xunit;

namespace QuranVideo.Tests;

public class SequenceValidationTests : IDisposable
{
    private readonly string _testDir;
    private readonly WorkspaceService _workspaceService;
    private readonly string _dbPath;

    public SequenceValidationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"quran_seq_val_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testDir);
        _dbPath = _workspaceService.GetDatabasePath();

        // Initialiser la base SQLite de test avec toutes les tables
        using (var db = new AppDbContext(_dbPath))
        {
            db.Database.EnsureCreated();
        }
        var migrationService = new DatabaseMigrationService();
        migrationService.MigrateDatabaseAsync(_dbPath).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch { /* ignore cleanup errors */ }
    }

    private void SeedMockSurahAndAudio(int surahNumber, string reciterId, string sourceId, double durationSec)
    {
        using var db = new AppDbContext(_dbPath);

        // Créer le fichier audio factice sur disque
        var audioRelPath = $"media/audio/surah_{surahNumber:D3}.mp3";
        var audioFullPath = Path.Combine(_testDir, audioRelPath);
        Directory.CreateDirectory(Path.GetDirectoryName(audioFullPath)!);
        File.WriteAllBytes(audioFullPath, new byte[] { 0xFF, 0xFB, 0x90, 0x64 });

        if (!db.Reciters.Any(r => r.Id == reciterId))
        {
            db.Reciters.Add(new ReciterEntity { Id = reciterId, NameArabic = "القارئ", NameLatin = reciterId });
        }

        var colId = $"{reciterId}_col";
        if (!db.RecitationCollections.Any(c => c.Id == colId))
        {
            db.RecitationCollections.Add(new RecitationCollectionEntity
            {
                Id = colId,
                ReciterId = reciterId,
                TitleArabic = "المصحف المرتل"
            });
        }

        var source = new AudioSourceEntity
        {
            Id = sourceId,
            CollectionId = colId,
            SurahNumber = surahNumber,
            RelativeFilePath = audioRelPath,
            DurationSeconds = durationSec,
            Sha256 = "dummy_sha"
        };
        db.AudioSources.Add(source);
        db.SaveChanges();
    }

    private void SeedMockImageRecipe(int surahNumber, int ayahNumber, bool createPhysicalFile = true)
    {
        using var db = new AppDbContext(_dbPath);

        if (!db.Surahs.Any(s => s.Number == surahNumber))
        {
            db.Surahs.Add(new SurahEntity
            {
                Number = surahNumber,
                NameArabic = "سورة",
                NameLatin = "Surah",
                AyahCount = 286,
                StartPage = 1,
                EndPage = 1
            });
            db.SaveChanges();
        }

        var ayahId = $"{surahNumber}:{ayahNumber}";
        if (!db.Ayahs.Any(a => a.Id == ayahId))
        {
            db.Ayahs.Add(new AyahEntity
            {
                Id = ayahId,
                SurahNumber = surahNumber,
                AyahNumber = ayahNumber,
                VerseNo = surahNumber * 1000 + ayahNumber,
                PageNumber = 1
            });
            db.SaveChanges();
        }

        var relPath = $"media/images/ayahs/ayah_{surahNumber:D3}_{ayahNumber:D3}.png";
        if (createPhysicalFile)
        {
            var fullPath = Path.Combine(_testDir, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        }

        var recipe = new AyahImageRecipeEntity
        {
            AyahId = ayahId,
            SurahNumber = surahNumber,
            AyahNumber = ayahNumber,
            RelativeImagePath = relPath,
            Status = AyahImageStatus.Approved,
            Revision = 1
        };
        db.ImageRecipes.Add(recipe);
        db.SaveChanges();
    }

    private void SeedMockPassage(string sourceId, int surahNumber, int ayahNumber, double start, double end, PassageKind kind = PassageKind.Full)
    {
        using var db = new AppDbContext(_dbPath);
        var passage = new AyahPassageEntity
        {
            AudioSourceId = sourceId,
            SurahNumber = surahNumber,
            AyahNumber = ayahNumber,
            AyahId = $"{surahNumber}:{ayahNumber}",
            StartTimeSec = start,
            EndTimeSec = end,
            Kind = kind,
            IsValidatedByUser = true,
            Revision = 1
        };
        db.Passages.Add(passage);
        db.SaveChanges();
    }

    [Fact]
    public async Task ValidateSequence_OutOfBoundsOrInvalidRange_ReturnsGlobalError()
    {
        var validator = new SequenceValidationService(_workspaceService);

        // Al-Fatiha a 7 versets : verset 8 hors limites
        var r1 = await validator.ValidateSequenceAsync(1, 1, 8, "saber");
        Assert.False(r1.IsValid);
        Assert.Contains("خارج حدود سورة الفاتحة", r1.GlobalErrors[0]);

        // Start > End
        var r2 = await validator.ValidateSequenceAsync(1, 5, 2, "saber");
        Assert.False(r2.IsValid);
        Assert.Contains("يجب أن تسبق أو تساوي", r2.GlobalErrors[0]);

        // Numéro de sourate invalide
        var r3 = await validator.ValidateSequenceAsync(115, 1, 3, "saber");
        Assert.False(r3.IsValid);
        Assert.Contains("رقم السورة (115) غير صحيح", r3.GlobalErrors[0]);
    }

    [Fact]
    public async Task ValidateSequence_MissingAyahInSequence_BlocksEntireExport()
    {
        // Scénario explicite de V3-02-09 : Plage 10 à 15 où l'ayah 12 manque
        int surah = 2; // Al-Baqarah
        string sourceId = "src_baqarah";
        SeedMockSurahAndAudio(surah, "saber", sourceId, 500.0);

        for (int ayah = 10; ayah <= 15; ayah++)
        {
            SeedMockImageRecipe(surah, ayah, createPhysicalFile: true);
            if (ayah != 12) // Ayah 12 n'est PAS annotée
            {
                SeedMockPassage(sourceId, surah, ayah, ayah * 10.0, (ayah + 1) * 10.0);
            }
        }

        var validator = new SequenceValidationService(_workspaceService);
        var result = await validator.ValidateSequenceAsync(surah, 10, 15, "saber", sourceId);

        Assert.False(result.IsValid);
        var allErrors = result.GetAllErrorMessages();

        // L'ayah 12 doit être explicitement signalée comme manquante
        Assert.Contains(allErrors, e => e.Contains("12") && e.Contains("غير محدد"));

        var item12 = result.Items.FirstOrDefault(i => i.AyahNumber == 12);
        Assert.NotNull(item12);
        Assert.False(item12.IsValid);
    }

    [Fact]
    public async Task ValidateSequence_PartialRetake_CannotReplaceFullAyah()
    {
        int surah = 1;
        string sourceId = "src_fatiha";
        SeedMockSurahAndAudio(surah, "saber", sourceId, 60.0);

        SeedMockImageRecipe(surah, 1);
        SeedMockImageRecipe(surah, 2);

        SeedMockPassage(sourceId, surah, 1, 0.0, 5.0, PassageKind.Full);
        // Ayah 2 n'a qu'un PartialRetake (reprise partielle)
        SeedMockPassage(sourceId, surah, 2, 5.0, 8.0, PassageKind.PartialRetake);

        var validator = new SequenceValidationService(_workspaceService);
        var result = await validator.ValidateSequenceAsync(surah, 1, 2, "saber", sourceId);

        Assert.False(result.IsValid);
        var allErrors = result.GetAllErrorMessages();
        Assert.Contains(allErrors, e => e.Contains("2") && e.Contains("استدراك جزئي"));
    }

    [Fact]
    public async Task ValidateSequence_TemporalOverlap_BlocksExport()
    {
        int surah = 1;
        string sourceId = "src_fatiha";
        SeedMockSurahAndAudio(surah, "saber", sourceId, 60.0);

        SeedMockImageRecipe(surah, 1);
        SeedMockImageRecipe(surah, 2);

        // Ayah 1 finit à 5.0s, mais Ayah 2 commence à 4.0s (chevauchement interdit de 1 seconde)
        SeedMockPassage(sourceId, surah, 1, 0.0, 5.0, PassageKind.Full);
        SeedMockPassage(sourceId, surah, 2, 4.0, 9.0, PassageKind.Full);

        var validator = new SequenceValidationService(_workspaceService);
        var result = await validator.ValidateSequenceAsync(surah, 1, 2, "saber", sourceId);

        Assert.False(result.IsValid);
        var allErrors = result.GetAllErrorMessages();
        Assert.Contains(allErrors, e => e.Contains("تداخل زمني بين الآية 1 والآية 2"));
    }

    [Fact]
    public async Task ValidateSequence_GapBetweenTakes_FlagsJunctionWarning()
    {
        int surah = 1;
        string sourceId = "src_fatiha";
        SeedMockSurahAndAudio(surah, "saber", sourceId, 60.0);

        SeedMockImageRecipe(surah, 1);
        SeedMockImageRecipe(surah, 2);

        // Ayah 1 finit à 5.0s, Ayah 2 commence à 6.5s (pause de 1.5s entre les prises)
        SeedMockPassage(sourceId, surah, 1, 0.0, 5.0, PassageKind.Full);
        SeedMockPassage(sourceId, surah, 2, 6.5, 11.0, PassageKind.Full);

        var validator = new SequenceValidationService(_workspaceService);
        var result = await validator.ValidateSequenceAsync(surah, 1, 2, "saber", sourceId);

        // Une jonction avec pause non confirmée doit bloquer l'export
        Assert.False(result.IsValid);
        Assert.True(result.HasJunctionWarnings);

        var item2 = result.Items[1];
        Assert.True(item2.HasGapWithPrevious);
        Assert.Equal(1.5, item2.GapSeconds, 1);
        Assert.Contains("فاصل زمني", item2.Errors[0]);

        // Une fois la jonction confirmée par l'utilisateur, l'export est débloqué
        var confirmedResult = await validator.ValidateSequenceAsync(surah, 1, 2, "saber", sourceId, reviewedJunctions: new HashSet<int> { 2 });
        Assert.True(confirmedResult.IsValid);
        Assert.False(confirmedResult.HasJunctionWarnings);
    }

    [Fact]
    public async Task ValidateSequence_CompleteValidSequence_PassesAllChecks()
    {
        int surah = 1;
        string sourceId = "src_fatiha";
        SeedMockSurahAndAudio(surah, "saber", sourceId, 60.0);

        // Versets 1 à 3 parfaitement consécutifs sans trou
        for (int a = 1; a <= 3; a++)
        {
            SeedMockImageRecipe(surah, a);
        }

        SeedMockPassage(sourceId, surah, 1, 0.0, 5.0);
        SeedMockPassage(sourceId, surah, 2, 5.0, 10.0);
        SeedMockPassage(sourceId, surah, 3, 10.0, 16.0);

        var validator = new SequenceValidationService(_workspaceService);
        var result = await validator.ValidateSequenceAsync(surah, 1, 3, "saber", sourceId);

        Assert.True(result.IsValid);
        Assert.False(result.HasJunctionWarnings);
        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, item => Assert.True(item.IsValid));
    }
}
