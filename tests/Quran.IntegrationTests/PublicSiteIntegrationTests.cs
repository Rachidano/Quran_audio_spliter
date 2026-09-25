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
        Assert.Contains("/mushaf/warsh/page/1", html);
    }

    [Theory]
    [InlineData("/surah/2", "/mushaf/warsh/page/2?Reference=2:1")]
    [InlineData("/quran/2/4", "/mushaf/warsh/page/2?Reference=2:4")]
    [InlineData("/quran/1/1?wajh=taqlil", "/mushaf/warsh/page/1?Reference=1:1&wajh=taqlil")]
    public async Task TextPages_RedirectToImageMushaf(string path, string target)
    {
        var response = await _anonClient.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(target, response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("/surah/115")]
    [InlineData("/quran/2/286")]
    public async Task InvalidCanonicalReference_ReturnsNotFound(string path) =>
        Assert.Equal(HttpStatusCode.NotFound, (await _anonClient.GetAsync(path)).StatusCode);

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
