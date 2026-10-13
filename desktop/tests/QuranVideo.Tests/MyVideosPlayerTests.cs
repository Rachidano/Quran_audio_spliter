using System.IO;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using Xunit;

namespace QuranVideo.Tests;

public class MyVideosPlayerTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly string _videosDir;

    public MyVideosPlayerTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"myvideos_test_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);

        _videosDir = Path.Combine(_testWorkspaceDir, "media", "videos");
        Directory.CreateDirectory(_videosDir);
    }

    [Fact]
    public void RefreshVideos_WithMediaFiles_ShouldPopulateBothMp4AndMp3()
    {
        var v1 = Path.Combine(_videosDir, "ayah_001_001.mp4");
        var a1 = Path.Combine(_videosDir, "ayah_001_002.mp3");
        var other = Path.Combine(_videosDir, "notes.txt");

        File.WriteAllBytes(v1, new byte[100]);
        File.WriteAllBytes(a1, new byte[50]);
        File.WriteAllBytes(other, new byte[20]);

        var vm = new MyVideosViewModel(_workspaceService);

        Assert.Equal(2, vm.Videos.Count);
        Assert.True(vm.HasVideos);
        Assert.False(vm.HasNoVideos);
        Assert.NotNull(vm.SelectedVideo);
    }

    [Fact]
    public void SelectingVideo_ShouldActivatePlayerAndSetUri()
    {
        var v1 = Path.Combine(_videosDir, "sample_video.mp4");
        File.WriteAllBytes(v1, new byte[200]);

        var vm = new MyVideosViewModel(_workspaceService);

        Assert.NotNull(vm.SelectedVideo);
        Assert.True(vm.IsPlayerActive);
        Assert.NotNull(vm.CurrentMediaUri);
        Assert.Equal(new Uri(v1), vm.CurrentMediaUri);
        Assert.False(vm.HasPlayerError);
    }

    [Fact]
    public void PlayerCommands_TogglePlayPause_ShouldFireEvents()
    {
        var v1 = Path.Combine(_videosDir, "sample_video.mp4");
        File.WriteAllBytes(v1, new byte[200]);

        var vm = new MyVideosViewModel(_workspaceService);

        bool playFired = false;
        bool pauseFired = false;
        vm.PlayRequested += () => playFired = true;
        vm.PauseRequested += () => pauseFired = true;

        vm.TogglePlayPause();
        Assert.True(vm.IsPlaying);
        Assert.True(playFired);

        vm.TogglePlayPause();
        Assert.False(vm.IsPlaying);
        Assert.True(pauseFired);
    }

    [Fact]
    public void PlayerCommands_StopAndSeek_ShouldUpdateState()
    {
        var v1 = Path.Combine(_videosDir, "sample_video.mp4");
        File.WriteAllBytes(v1, new byte[200]);

        var vm = new MyVideosViewModel(_workspaceService);

        bool stopFired = false;
        double soughtPos = -1;
        vm.StopRequested += () => stopFired = true;
        vm.SeekRequested += pos => soughtPos = pos;

        vm.Seek(12.5);
        Assert.Equal(12.5, vm.PlaybackPositionSec);
        Assert.Equal(12.5, soughtPos);

        vm.Stop();
        Assert.False(vm.IsPlaying);
        Assert.Equal(0, vm.PlaybackPositionSec);
        Assert.True(stopFired);
    }

    [Fact]
    public void DeleteVideo_ShouldReleasePlayerAndRemoveFile()
    {
        var v1 = Path.Combine(_videosDir, "to_delete.mp4");
        File.WriteAllBytes(v1, new byte[200]);

        var vm = new MyVideosViewModel(_workspaceService);
        vm.ConfirmDeleteHandler = _ => true; // Always confirm

        bool closeFired = false;
        vm.ClosePlayerRequested += () => closeFired = true;

        Assert.Single(vm.Videos);
        vm.DeleteVideo(vm.Videos[0]);

        Assert.True(closeFired);
        Assert.False(File.Exists(v1));
        Assert.Empty(vm.Videos);
    }

    [Fact]
    public void RenameVideo_ShouldReleasePlayerAndRenameFile()
    {
        var v1 = Path.Combine(_videosDir, "old_name.mp4");
        File.WriteAllBytes(v1, new byte[200]);

        var vm = new MyVideosViewModel(_workspaceService);

        bool closeFired = false;
        vm.ClosePlayerRequested += () => closeFired = true;

        vm.StartRenameVideo(vm.Videos[0]);
        Assert.True(vm.IsRenaming);
        Assert.Equal("old_name", vm.NewVideoName);

        vm.NewVideoName = "new_blessed_video";
        vm.ConfirmRename();

        Assert.True(closeFired);
        Assert.False(File.Exists(v1));
        Assert.True(File.Exists(Path.Combine(_videosDir, "new_blessed_video.mp4")));
        Assert.Single(vm.Videos);
        Assert.Equal("new_blessed_video.mp4", vm.Videos[0].FileName);
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
        catch { }
    }
}
