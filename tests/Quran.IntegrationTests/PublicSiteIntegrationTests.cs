using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Quran.IntegrationTests;

public class PublicSiteIntegrationTests : IClassFixture<QuranTestWebApplicationFactory>
{
    private readonly QuranTestWebApplicationFactory _factory;
    private readonly HttpClient _anonClient;

    public PublicSiteIntegrationTests(QuranTestWebApplicationFactory factory)
    {
        _factory = factory;
        _anonClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task HomePage_LoadsSuccessfully_WithCanonicalLinks()
    {
        var response = await _anonClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("مصحف رواية ورش عن نافع", html);
        Assert.Contains("/surah/1", html);
        Assert.Contains("/mushaf/page/1", html);
    }

    [Fact]
    public async Task SurahPage_CanonicalRoute_LoadsSuccessfully()
    {
        var response = await _anonClient.GetAsync("/surah/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("سورة الفاتحة", html);
        Assert.Contains("/quran/1/1", html);
    }

    [Fact]
    public async Task AyahPage_CanonicalRoute_LoadsSuccessfully_WithPermaAndWajhSelector()
    {
        var response = await _anonClient.GetAsync("/quran/1/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("سورة الفاتحة", html);
        Assert.Contains("مصحف الجزائر الإلكتروني", html);
        Assert.Contains("mushaf-crop-container", html);
        Assert.Contains("share-canonical-btn", html);
    }

    [Fact]
    public async Task AyahPage_WithWajhQueryParameter_BindsWajhCorrectly()
    {
        var response = await _anonClient.GetAsync("/quran/1/1?wajh=taqlil");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("سورة الفاتحة", html);
    }

    [Fact]
    public async Task MushafPage_CanonicalRoute_LoadsSuccessfully_WithRegionsAndViewer()
    {
        var response = await _anonClient.GetAsync("/mushaf/page/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("مصحف الجزائر الإلكتروني — ورش", html);
        Assert.Contains("pilot-sheet", html);
        Assert.Contains("pilot-regions", html);
        Assert.Contains("passage-player", html);
    }

    [Fact]
    public async Task PlaybackApi_ForValidReference_ReturnsOk()
    {
        var response = await _anonClient.GetAsync("/api/annotations/playback?reference=1:1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PreviewApi_Anonymous_IsRejected()
    {
        var response = await _anonClient.GetAsync("/api/annotations/preview?reference=1:1");
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Redirect);
    }
}
