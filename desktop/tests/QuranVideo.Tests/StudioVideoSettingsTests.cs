using System.IO;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Services;
using QuranVideo.Desktop.ViewModels;
using Xunit;

namespace QuranVideo.Tests;

/// <summary>
/// Studio — carte « إنشاء الفيديو » : résumé des réglages et fenêtre « تهيئة ».
/// </summary>
public class StudioVideoSettingsTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly WorkspaceService _workspaceService;
    private readonly StudioViewModel _vm;

    public StudioVideoSettingsTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"studio_settings_{Guid.NewGuid():N}");
        _workspaceService = new WorkspaceService();
        _workspaceService.InitializeWorkspace(_testWorkspaceDir);
        _vm = new StudioViewModel(_workspaceService);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _workspaceService.Dispose();
        try
        {
            if (Directory.Exists(_testWorkspaceDir)) Directory.Delete(_testWorkspaceDir, true);
        }
        catch { }
    }

    [Fact]
    public void Defaults_SummarizeHorizontalLightWithoutWatermark()
    {
        Assert.Equal("📐 أفقي 16:9", _vm.OrientationSummary);
        Assert.Equal("⚪ خلفية فاتحة", _vm.BackgroundSummary);
        Assert.Equal("🚫 بدون توقيع", _vm.WatermarkSummary);
        Assert.False(_vm.HasWatermark);
        Assert.False(_vm.IsVideoSettingsOpen);
    }

    [Fact]
    public void ChangingSettings_UpdatesSummaryAndNotifiesView()
    {
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _vm.IsVertical = true;
        _vm.SetStudioBackground("Dark");
        _vm.SetWatermarkType("Text");
        _vm.WizardWatermarkText = "قناة التلاوة";

        Assert.Equal("📱 عمودي 9:16", _vm.OrientationSummary);
        Assert.Equal("🌑 خلفية داكنة", _vm.BackgroundSummary);
        Assert.Equal("✍️ قناة التلاوة", _vm.WatermarkSummary);
        Assert.True(_vm.IsWatermarkText);
        Assert.True(_vm.HasWatermark);
        Assert.Contains(nameof(StudioViewModel.OrientationSummary), changed);
        Assert.Contains(nameof(StudioViewModel.BackgroundSummary), changed);
        Assert.Contains(nameof(StudioViewModel.WatermarkSummary), changed);
        Assert.Contains(nameof(StudioViewModel.IsWatermarkText), changed);
    }

    [Fact]
    public void LogoWatermark_ShowsFileName()
    {
        _vm.WizardWatermarkImagePath = Path.Combine("media", "assets", "logo_chaine.png");
        _vm.WizardWatermark = WatermarkType.Logo;

        Assert.True(_vm.IsWatermarkLogo);
        Assert.False(_vm.IsWatermarkText);
        Assert.Equal("logo_chaine.png", _vm.WatermarkLogoFileName);
        Assert.Equal("🏷️ logo_chaine.png", _vm.WatermarkSummary);
    }

    [Fact]
    public void OpenAndCloseSettings()
    {
        _vm.OpenVideoSettingsCommand.Execute(null);
        Assert.True(_vm.IsVideoSettingsOpen);

        _vm.CloseVideoSettingsCommand.Execute(null);
        Assert.False(_vm.IsVideoSettingsOpen);
    }
}
