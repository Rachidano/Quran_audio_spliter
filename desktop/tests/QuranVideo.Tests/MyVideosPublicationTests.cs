using System.IO;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using Xunit;

namespace QuranVideo.Tests;

/// <summary>
/// « فيديوهاتي » : affichage, copie et (re)génération du titre et de la description de publication.
/// </summary>
public class MyVideosPublicationTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly string _videosDir;
    private readonly WorkspaceService _workspaceService;
    private readonly MyVideosViewModel _viewModel;
    private readonly List<string> _clipboard = new();
    private readonly Action<string> _originalClipboard = MyVideosViewModel.ClipboardSetter;

    public MyVideosPublicationTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"my_videos_publication_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);
        new RecitationCatalogService(_workspaceService).InitializeAsync().GetAwaiter().GetResult();

        _videosDir = Path.Combine(_testWorkspaceDir, "media", "videos");
        Directory.CreateDirectory(_videosDir);
        MyVideosViewModel.ClipboardSetter = _clipboard.Add;
        _viewModel = new MyVideosViewModel(_workspaceService) { ConfirmDeleteHandler = _ => true };
    }

    public void Dispose()
    {
        MyVideosViewModel.ClipboardSetter = _originalClipboard;
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir)) Directory.Delete(_testWorkspaceDir, true);
        }
        catch { }
    }

    private VideoFileInfo Select(string fileName)
    {
        _viewModel.RefreshVideos();
        var video = _viewModel.Videos.Single(v => v.FileName == fileName);
        _viewModel.SelectedVideo = video;
        return video;
    }

    [Fact]
    public void SelectingVideoWithText_ShowsTitleAndDescription_AndCopiesThem()
    {
        var video = Path.Combine(_videosDir, "ayah_002_255_husary_h_p1_r1_i1.mp4");
        File.WriteAllText(video, "fake");
        File.WriteAllText(PublicationTextService.GetTextPath(video),
            PublicationTextService.BuildText("البقرة", 255, 255, "الشيخ محمود خليل الحصري"));

        Select("ayah_002_255_husary_h_p1_r1_i1.mp4");

        Assert.True(_viewModel.IsPublicationPanelVisible);
        Assert.True(_viewModel.HasPublicationText);
        Assert.Equal("سورة البقرة - الآية 255 | الشيخ محمود خليل الحصري | رواية ورش عن نافع", _viewModel.PublicationTitle);
        Assert.StartsWith("سورة البقرة — الآية 255", _viewModel.PublicationDescription);
        Assert.Contains("#سورة_البقرة", _viewModel.PublicationDescription);
        Assert.DoesNotContain("العنوان", _viewModel.PublicationDescription);

        _viewModel.CopyPublicationTitle();
        _viewModel.CopyPublicationDescription();
        Assert.Equal(new[] { _viewModel.PublicationTitle, _viewModel.PublicationDescription }, _clipboard);
        Assert.Contains("نسخ الوصف", _viewModel.PublicationStatusMessage);
    }

    [Fact]
    public async Task OldVideoWithoutText_GeneratedFromFileName_WithReciterName()
    {
        File.WriteAllText(Path.Combine(_videosDir, "ayah_002_255_husary_h_p12_r1_i1.mp4"), "fake");
        Select("ayah_002_255_husary_h_p12_r1_i1.mp4");
        Assert.False(_viewModel.HasPublicationText);
        Assert.Equal("✨ إنشاء النص", _viewModel.GeneratePublicationTextLabel);

        await _viewModel.GeneratePublicationTextAsync();

        Assert.True(_viewModel.HasPublicationText);
        Assert.Equal("سورة البقرة - الآية 255 | الشيخ محمود خليل الحصري | رواية ورش عن نافع", _viewModel.PublicationTitle);
        Assert.Equal("🔄 إعادة إنشاء النص", _viewModel.GeneratePublicationTextLabel);
    }

    [Fact]
    public async Task RenamedProjectVideo_GeneratedFromDatabase_CoversAyahRange()
    {
        File.WriteAllText(Path.Combine(_videosDir, "ma_video_kursi.mp4"), "fake");
        using (var db = new AppDbContext(_workspaceService.GetDatabasePath()))
        {
            db.Projects.Add(new VideoProjectEntity
            {
                Title = "آية الكرسي",
                SurahNumber = 2, StartAyahNumber = 255, EndAyahNumber = 257,
                ReciterId = "husary", AudioSourceId = RecitationCatalogService.HusaryCollection + ":002",
                OutputVideoPath = Path.Combine("media", "videos", "ma_video_kursi.mp4"),
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            });
            db.SaveChanges();
        }
        Select("ma_video_kursi.mp4");

        await _viewModel.GeneratePublicationTextAsync();

        Assert.True(_viewModel.HasPublicationText);
        Assert.StartsWith("سورة البقرة - الآيات 255 إلى 257", _viewModel.PublicationTitle);
    }

    [Fact]
    public async Task UnknownVideo_ExplainsWhyTextCannotBeGenerated()
    {
        File.WriteAllText(Path.Combine(_videosDir, "video_perso.mp4"), "fake");
        Select("video_perso.mp4");

        await _viewModel.GeneratePublicationTextAsync();

        Assert.False(_viewModel.HasPublicationText);
        Assert.Contains("تعذر تحديد السورة", _viewModel.PublicationStatusMessage);
        Assert.False(File.Exists(Path.Combine(_videosDir, "video_perso.txt")));
    }

    [Fact]
    public void AudioFile_HidesPublicationPanel()
    {
        File.WriteAllText(Path.Combine(_videosDir, "ayah_001_001_audio.mp3"), "fake");
        Select("ayah_001_001_audio.mp3");

        Assert.False(_viewModel.IsPublicationPanelVisible);
    }

    [Fact]
    public void RenamingVideo_RenamesItsPublicationText()
    {
        var video = Path.Combine(_videosDir, "ayah_001_001_husary_h_p1_r1_i1.mp4");
        File.WriteAllText(video, "fake");
        File.WriteAllText(PublicationTextService.GetTextPath(video), PublicationTextService.BuildText("الفاتحة", 1, 1, null));

        _viewModel.StartRenameVideo(Select("ayah_001_001_husary_h_p1_r1_i1.mp4"));
        _viewModel.NewVideoName = "fatiha_ayah_1";
        _viewModel.ConfirmRename();

        Assert.True(File.Exists(Path.Combine(_videosDir, "fatiha_ayah_1.txt")));
        Assert.False(File.Exists(PublicationTextService.GetTextPath(video)));
        Assert.True(_viewModel.HasPublicationText);
    }
}
