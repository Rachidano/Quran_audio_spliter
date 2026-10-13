using System.IO;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using Xunit;

namespace QuranVideo.Tests;

public class MyVideosTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly MyVideosViewModel _viewModel;

    public MyVideosTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"my_videos_test_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);
        _viewModel = new MyVideosViewModel(_workspaceService)
        {
            ConfirmDeleteHandler = _ => true
        };
    }

    public void Dispose()
    {
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir)) Directory.Delete(_testWorkspaceDir, true);
        }
        catch { }
    }

    [Fact]
    public void RefreshVideos_IncludesBothMp4AndMp3Files()
    {
        var videosDir = Path.Combine(_testWorkspaceDir, "media", "videos");
        Directory.CreateDirectory(videosDir);

        File.WriteAllText(Path.Combine(videosDir, "ayah_001_001_h_rev1.mp4"), "fake video");
        File.WriteAllText(Path.Combine(videosDir, "ayah_001_001_audio.mp3"), "fake audio");

        _viewModel.RefreshVideos();

        Assert.Equal(2, _viewModel.Videos.Count);
        Assert.True(_viewModel.HasVideos);
        Assert.False(_viewModel.HasNoVideos);
        Assert.Contains(_viewModel.Videos, v => v.FileName == "ayah_001_001_h_rev1.mp4");
        Assert.Contains(_viewModel.Videos, v => v.FileName == "ayah_001_001_audio.mp3");
    }

    [Fact]
    public void DeleteVideo_DeletesFileFromDiskAndRemovesFromCollection()
    {
        var videosDir = Path.Combine(_testWorkspaceDir, "media", "videos");
        Directory.CreateDirectory(videosDir);
        var filePath = Path.Combine(videosDir, "test_video.mp4");
        File.WriteAllText(filePath, "dummy data");

        _viewModel.RefreshVideos();
        Assert.Single(_viewModel.Videos);

        var videoItem = _viewModel.Videos[0];
        _viewModel.DeleteVideo(videoItem);

        Assert.False(File.Exists(filePath));
        Assert.Empty(_viewModel.Videos);
        Assert.True(_viewModel.HasNoVideos);
    }

    [Fact]
    public void RenameVideo_RenamesFileOnDiskAndUpdatesCollection()
    {
        var videosDir = Path.Combine(_testWorkspaceDir, "media", "videos");
        Directory.CreateDirectory(videosDir);
        var originalPath = Path.Combine(videosDir, "original_name.mp4");
        File.WriteAllText(originalPath, "dummy video");

        _viewModel.RefreshVideos();
        var videoItem = _viewModel.Videos[0];

        _viewModel.StartRenameVideo(videoItem);
        Assert.True(_viewModel.IsRenaming);
        Assert.Equal("original_name", _viewModel.NewVideoName);

        _viewModel.NewVideoName = "custom_renamed_video";
        _viewModel.ConfirmRename();

        Assert.False(_viewModel.IsRenaming);
        Assert.False(File.Exists(originalPath));
        var newExpectedPath = Path.Combine(videosDir, "custom_renamed_video.mp4");
        Assert.True(File.Exists(newExpectedPath));

        Assert.Single(_viewModel.Videos);
        Assert.Equal("custom_renamed_video.mp4", _viewModel.Videos[0].FileName);
    }
}
