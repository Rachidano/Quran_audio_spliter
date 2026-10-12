using System.IO;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using Xunit;

namespace QuranVideo.Tests;

public class AnnotationTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly AnnotationService _annotationService;

    public AnnotationTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"quran_test_annotation_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);

        // Ensure database schema is created
        var dbPath = Path.Combine(_testWorkspaceDir, "data", "quran.db");
        using (var db = new AppDbContext(dbPath))
        {
            db.Database.EnsureCreated();
            
            // Seed foreign key prerequisites
            db.Reciters.Add(new ReciterEntity { Id = "husary", NameArabic = "محمود خليل الحصري", NameLatin = "Al-Husary" });
            db.RecitationCollections.Add(new RecitationCollectionEntity
            {
                Id = "husary_murattal_warsh",
                ReciterId = "husary",
                TitleArabic = "المصحف المرتل برواية ورش"
            });
            db.Surahs.Add(new SurahEntity
            {
                Number = 1,
                NameArabic = "الفاتحة",
                NameLatin = "Al-Fatihah",
                AyahCount = 7,
                StartPage = 1,
                EndPage = 1
            });
            for (int i = 1; i <= 7; i++)
            {
                db.Ayahs.Add(new AyahEntity
                {
                    Id = $"1:{i}",
                    SurahNumber = 1,
                    AyahNumber = i,
                    VerseNo = i,
                    PageNumber = 1
                });
            }

            db.AudioSources.Add(new AudioSourceEntity
            {
                Id = "husary_murattal_warsh:001",
                CollectionId = "husary_murattal_warsh",
                SurahNumber = 1,
                Sha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
                DurationSeconds = 45.0
            });
            db.SaveChanges();
        }

        _annotationService = new AnnotationService(_workspaceService);
    }

    public void Dispose()
    {
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir))
            {
                Directory.Delete(_testWorkspaceDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task SavePassage_ValidBounds_SavesSuccessfully()
    {
        var passage = new AyahPassageEntity
        {
            AudioSourceId = "husary_murattal_warsh:001",
            SurahNumber = 1,
            AyahNumber = 1,
            AyahId = "1:1",
            StartTimeSec = 0.0,
            EndTimeSec = 5.8,
            Kind = PassageKind.Full,
            IsSelectedForExport = true
        };

        var (success, error, saved) = await _annotationService.SavePassageAsync(passage, maxAudioDurationSec: 45.0);

        Assert.True(success);
        Assert.Null(error);
        Assert.True(saved.Id > 0);
        Assert.Equal(1, saved.Revision);

        var list = await _annotationService.GetPassagesForSurahAsync("husary_murattal_warsh:001", 1);
        Assert.Single(list);
        Assert.Equal(0.0, list[0].StartTimeSec);
        Assert.Equal(5.8, list[0].EndTimeSec);
    }

    [Fact]
    public async Task SavePassage_NegativeStartTime_ReturnsFailure()
    {
        var passage = new AyahPassageEntity
        {
            AudioSourceId = "husary_murattal_warsh:001",
            SurahNumber = 1,
            AyahNumber = 1,
            AyahId = "1:1",
            StartTimeSec = -1.5,
            EndTimeSec = 5.0
        };

        var (success, error, _) = await _annotationService.SavePassageAsync(passage);

        Assert.False(success);
        Assert.Contains("سالباً", error);
    }

    [Fact]
    public async Task SavePassage_EndTimeBeforeOrEqualStartTime_ReturnsFailure()
    {
        var passage = new AyahPassageEntity
        {
            AudioSourceId = "husary_murattal_warsh:001",
            SurahNumber = 1,
            AyahNumber = 1,
            AyahId = "1:1",
            StartTimeSec = 5.0,
            EndTimeSec = 5.0
        };

        var (success, error, _) = await _annotationService.SavePassageAsync(passage);

        Assert.False(success);
        Assert.Contains("أكبر", error);
    }

    [Fact]
    public async Task SavePassage_EndTimeExceedsMaxAudioDuration_ReturnsFailure()
    {
        var passage = new AyahPassageEntity
        {
            AudioSourceId = "husary_murattal_warsh:001",
            SurahNumber = 1,
            AyahNumber = 1,
            AyahId = "1:1",
            StartTimeSec = 40.0,
            EndTimeSec = 60.0
        };

        var (success, error, _) = await _annotationService.SavePassageAsync(passage, maxAudioDurationSec: 45.0);

        Assert.False(success);
        Assert.Contains("يتجاوز المدة", error);
    }

    [Fact]
    public async Task SavePassage_UpdatingBounds_IncrementsRevisionForInvalidation()
    {
        var passage = new AyahPassageEntity
        {
            AudioSourceId = "husary_murattal_warsh:001",
            SurahNumber = 1,
            AyahNumber = 2,
            AyahId = "1:2",
            StartTimeSec = 5.8,
            EndTimeSec = 11.2,
            Kind = PassageKind.Full
        };

        var (_, _, saved1) = await _annotationService.SavePassageAsync(passage);
        Assert.Equal(1, saved1.Revision);

        // Edit bounds (L06-07)
        saved1.EndTimeSec = 11.5;
        var (_, _, saved2) = await _annotationService.SavePassageAsync(saved1);

        Assert.Equal(2, saved2.Revision);
    }

    [Fact]
    public async Task SavePassage_SavingRetake_PreservesKindAndDoesNotReplaceFull()
    {
        var fullPassage = new AyahPassageEntity
        {
            AudioSourceId = "husary_murattal_warsh:001",
            SurahNumber = 1,
            AyahNumber = 3,
            AyahId = "1:3",
            StartTimeSec = 11.2,
            EndTimeSec = 15.0,
            Kind = PassageKind.Full
        };
        await _annotationService.SavePassageAsync(fullPassage);

        var retakePassage = new AyahPassageEntity
        {
            AudioSourceId = "husary_murattal_warsh:001",
            SurahNumber = 1,
            AyahNumber = 3,
            AyahId = "1:3",
            StartTimeSec = 15.0,
            EndTimeSec = 19.2,
            Kind = PassageKind.FullRetake
        };
        await _annotationService.SavePassageAsync(retakePassage);

        var list = await _annotationService.GetPassagesForAyahAsync("husary_murattal_warsh:001", "1:3");
        Assert.Equal(2, list.Count);
        Assert.Contains(list, p => p.Kind == PassageKind.Full);
        Assert.Contains(list, p => p.Kind == PassageKind.FullRetake);
    }

    [Fact]
    public async Task UndoLastSave_RevertsInsertedPassage()
    {
        var passage = new AyahPassageEntity
        {
            AudioSourceId = "husary_murattal_warsh:001",
            SurahNumber = 1,
            AyahNumber = 4,
            AyahId = "1:4",
            StartTimeSec = 20.0,
            EndTimeSec = 25.0,
            Kind = PassageKind.Full
        };

        await _annotationService.SavePassageAsync(passage);
        var beforeUndo = await _annotationService.GetPassagesForSurahAsync("husary_murattal_warsh:001", 1);
        Assert.Single(beforeUndo);

        var undone = await _annotationService.UndoLastSaveAsync();
        Assert.NotNull(undone);
        Assert.Equal(4, undone.AyahNumber);

        var afterUndo = await _annotationService.GetPassagesForSurahAsync("husary_murattal_warsh:001", 1);
        Assert.Empty(afterUndo);
    }

    [Fact]
    public async Task ValidatePassage_TogglesUserValidationState()
    {
        var passage = new AyahPassageEntity
        {
            AudioSourceId = "husary_murattal_warsh:001",
            SurahNumber = 1,
            AyahNumber = 5,
            AyahId = "1:5",
            StartTimeSec = 25.0,
            EndTimeSec = 30.0,
            IsValidatedByUser = false
        };

        var (_, _, saved) = await _annotationService.SavePassageAsync(passage);
        Assert.False(saved.IsValidatedByUser);

        var ok = await _annotationService.ValidatePassageAsync(saved.Id, true);
        Assert.True(ok);

        var list = await _annotationService.GetPassagesForAyahAsync("husary_murattal_warsh:001", "1:5");
        Assert.True(list[0].IsValidatedByUser);
    }

    [Fact]
    public async Task ExportAndImportAnnotations_ValidData_RoundtripSucceeds()
    {
        await _annotationService.SavePassageAsync(new AyahPassageEntity
        {
            AudioSourceId = "husary_murattal_warsh:001",
            SurahNumber = 1,
            AyahNumber = 1,
            AyahId = "1:1",
            StartTimeSec = 0.0,
            EndTimeSec = 5.8,
            Kind = PassageKind.Full,
            IsValidatedByUser = true
        });

        await _annotationService.SavePassageAsync(new AyahPassageEntity
        {
            AudioSourceId = "husary_murattal_warsh:001",
            SurahNumber = 1,
            AyahNumber = 2,
            AyahId = "1:2",
            StartTimeSec = 5.8,
            EndTimeSec = 11.2,
            Kind = PassageKind.Full,
            IsValidatedByUser = true
        });

        var exportFile = Path.Combine(_testWorkspaceDir, "annotations", "export_fatiha.json");
        await _annotationService.ExportAnnotationsToJsonAsync(
            "husary_murattal_warsh:001",
            1,
            exportFile,
            "newMushaf");

        Assert.True(File.Exists(exportFile));

        // Import into fresh state
        var expectedSha = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";
        var (success, error, count) = await _annotationService.ImportAnnotationsFromJsonAsync(
            exportFile,
            expectedSha,
            "husary_murattal_warsh:001",
            1,
            "newMushaf");

        Assert.True(success);
        Assert.Empty(error);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ImportAnnotations_MismatchedAudioSha256_RejectedWithArabicMessage()
    {
        var exportFile = Path.Combine(_testWorkspaceDir, "annotations", "mismatch_test.json");
        Directory.CreateDirectory(Path.GetDirectoryName(exportFile)!);

        var json = """
        {
            "formatVersion": "1.0",
            "editionCode": "newMushaf",
            "surahNumber": 1,
            "audioSourceId": "husary_murattal_warsh:001",
            "audioSha256": "AAAAAAAAAA111111111122222222223333333333444444444455555555556666",
            "passages": []
        }
        """;
        await File.WriteAllTextAsync(exportFile, json);

        var (success, error, count) = await _annotationService.ImportAnnotationsFromJsonAsync(
            exportFile,
            "DIFFERENT_SHA256_HASH",
            "husary_murattal_warsh:001",
            1);

        Assert.False(success);
        Assert.Equal(0, count);
        Assert.Contains("البصمة الصوتية (SHA-256)", error);
    }

    [Fact]
    public async Task ImportAnnotations_IncompatibleFormatVersion_Rejected()
    {
        var exportFile = Path.Combine(_testWorkspaceDir, "annotations", "v2_test.json");
        Directory.CreateDirectory(Path.GetDirectoryName(exportFile)!);

        var json = """
        {
            "formatVersion": "9.9",
            "editionCode": "newMushaf",
            "surahNumber": 1,
            "passages": []
        }
        """;
        await File.WriteAllTextAsync(exportFile, json);

        var (success, error, _) = await _annotationService.ImportAnnotationsFromJsonAsync(
            exportFile,
            "",
            "husary_murattal_warsh:001",
            1);

        Assert.False(success);
        Assert.Contains("إصدار التنسيق غير مدعوم", error);
    }

    [Fact]
    public async Task StudioViewModel_SaveEndAndAdvance_AdvancesAyahAndPreFillsNextStart()
    {
        var vm = new StudioViewModel(_workspaceService, _annotationService, surahNumber: 1, ayahNumber: 1);
        // This fixture contains Husary only; select its source explicitly.
        vm.CurrentReciterId = "husary";
        vm.CurrentCollectionId = "husary_murattal_warsh";
        vm.CurrentAudioSourceId = "husary_murattal_warsh:001";
        vm.StartTimeSec = 0.0;
        vm.Seek(5.8);

        await vm.SaveEndAndAdvanceAsync();

        Assert.Equal(2, vm.CurrentAyahNumber);
        Assert.Equal(5.8, vm.StartTimeSec);
        Assert.Equal(1, vm.RetakeTargetAyahNumber); // L06-06: retake target stays targeting previous ayah
    }

    [Fact]
    public async Task StudioViewModel_SaveEndAndAdvance_AtLastAyahOfSurah_CompletesWithoutOverflow()
    {
        var vm = new StudioViewModel(_workspaceService, _annotationService, surahNumber: 1, ayahNumber: 7);
        // This fixture contains Husary only; select its source explicitly.
        vm.CurrentReciterId = "husary";
        vm.CurrentCollectionId = "husary_murattal_warsh";
        vm.CurrentAudioSourceId = "husary_murattal_warsh:001";
        vm.StartTimeSec = 35.0;
        vm.Seek(42.0);

        await vm.SaveEndAndAdvanceAsync();

        // Must stay at 7 and not overflow to 8
        Assert.Equal(7, vm.CurrentAyahNumber);
        Assert.Contains("اكتملت جميع آيات", vm.StatusMessage);
    }

    [Fact]
    public void StudioViewModel_ManualAyahChange_SyncsRetakeTarget()
    {
        var vm = new StudioViewModel(_workspaceService, _annotationService, surahNumber: 1, ayahNumber: 2);
        Assert.Equal(2, vm.RetakeTargetAyahNumber);

        vm.NextAyahCommand.Execute(null);
        Assert.Equal(3, vm.CurrentAyahNumber);
        Assert.Equal(3, vm.RetakeTargetAyahNumber);
    }

    [Fact]
    public void StudioViewModel_FormatTime_FormatsMinutesSecondsAccurately()
    {
        Assert.Equal("00:05.80", StudioViewModel.FormatTime(5.8));
        Assert.Equal("01:23.45", StudioViewModel.FormatTime(83.45));
        Assert.Equal("00:00.00", StudioViewModel.FormatTime(0.0));
        Assert.Equal("00:00.00", StudioViewModel.FormatTime(-5.0));
    }

    [Fact]
    public void StudioViewModel_StepForwardAndBackward_UpdatesTimeAndFormattedString()
    {
        var vm = new StudioViewModel(_workspaceService, _annotationService, surahNumber: 1, ayahNumber: 1);
        vm.CurrentPlaybackTimeSec = 10.0;

        vm.StepForwardCommand.Execute(null);
        Assert.Equal(11.0, vm.CurrentPlaybackTimeSec);
        Assert.Equal("00:11.00", vm.FormattedPlaybackTime);

        vm.StepBackwardCommand.Execute(null);
        Assert.Equal(10.0, vm.CurrentPlaybackTimeSec);
        Assert.Equal("00:10.00", vm.FormattedPlaybackTime);
    }

    [Fact]
    public void StudioViewModel_OrientationAndGeneratingFlags_WorkCorrectly()
    {
        var vm = new StudioViewModel(_workspaceService, _annotationService, surahNumber: 1, ayahNumber: 1);
        Assert.True(vm.IsHorizontal);
        Assert.False(vm.IsVertical);
        Assert.True(vm.IsNotGenerating);

        vm.IsVertical = true;
        Assert.False(vm.IsHorizontal);
        Assert.True(vm.IsVertical);

        vm.IsGenerating = true;
        Assert.False(vm.IsNotGenerating);
    }

    [Fact]
    public void StudioViewModel_SelectPassage_UpdatesTargetAndTimings()
    {
        var vm = new StudioViewModel(_workspaceService, _annotationService, surahNumber: 1, ayahNumber: 1);
        var passage = new QuranVideo.Desktop.Data.AyahPassageEntity
        {
            SurahNumber = 1,
            AyahNumber = 3,
            StartTimeSec = 15.2,
            EndTimeSec = 22.8,
            IsValidatedByUser = true
        };

        vm.SelectPassageCommand.Execute(passage);

        Assert.Equal(3, vm.CurrentAyahNumber);
        Assert.Equal(15.2, vm.StartTimeSec);
        Assert.Equal(22.8, vm.EndTimeSec);
        Assert.Equal(15.2, vm.CurrentPlaybackTimeSec);
        Assert.True(vm.IsCurrentPassageValidated);
        Assert.Equal("7.60 ثانية", passage.FormattedDuration);
    }

    [Fact]
    public async Task StudioViewModel_SaveCurrentPassage_SavesWithoutAdvancingAyah()
    {
        var vm = new StudioViewModel(_workspaceService, _annotationService, surahNumber: 1, ayahNumber: 2);
        vm.CurrentReciterId = "husary";
        vm.CurrentCollectionId = "husary_murattal_warsh";
        vm.CurrentAudioSourceId = "husary_murattal_warsh:001";
        vm.StartTimeSec = 10.0;
        vm.EndTimeSec = 15.5;

        await vm.SaveCurrentPassageAsync();

        // Must remain on ayah 2 (not advance to 3)
        Assert.Equal(2, vm.CurrentAyahNumber);
        Assert.Equal(10.0, vm.StartTimeSec);
        Assert.Equal(15.5, vm.EndTimeSec);
        Assert.True(vm.HasCurrentPassage);
        Assert.Contains("تم حفظ تعديل مقطع الآية 2", vm.StatusMessage);

        // Verify in database
        var passages = await _annotationService.GetPassagesForSurahAsync(vm.CurrentAudioSourceId, 1);
        var p2 = passages.FirstOrDefault(p => p.AyahNumber == 2);
        Assert.NotNull(p2);
        Assert.Equal(10.0, p2.StartTimeSec);
        Assert.Equal(15.5, p2.EndTimeSec);
    }

    [Fact]
    public async Task StudioViewModel_DeletePassage_RemovesFromDatabaseAndUpdatesList()
    {
        var vm = new StudioViewModel(_workspaceService, _annotationService, surahNumber: 1, ayahNumber: 1);
        vm.CurrentReciterId = "husary";
        vm.CurrentCollectionId = "husary_murattal_warsh";
        vm.CurrentAudioSourceId = "husary_murattal_warsh:001";
        vm.StartTimeSec = 2.0;
        vm.EndTimeSec = 6.0;

        await vm.SaveCurrentPassageAsync();
        Assert.True(vm.HasCurrentPassage);

        // Delete current passage
        await vm.DeleteCurrentPassageAsync();

        Assert.False(vm.HasCurrentPassage);
        Assert.Contains("تم حذف مقطع الآية 1", vm.StatusMessage);

        var passages = await _annotationService.GetPassagesForSurahAsync(vm.CurrentAudioSourceId, 1);
        Assert.DoesNotContain(passages, p => p.AyahNumber == 1);
    }
}

