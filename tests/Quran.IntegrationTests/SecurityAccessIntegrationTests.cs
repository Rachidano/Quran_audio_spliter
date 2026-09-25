using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Quran.IntegrationTests;

public class SecurityAccessIntegrationTests : IClassFixture<QuranTestWebApplicationFactory>
{
    private readonly QuranTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SecurityAccessIntegrationTests(QuranTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task AnonymousUser_AccessingAdminDashboard_IsRedirectedToLogin()
    {
        var response = await _client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task AnonymousUser_AccessingAdminAudioEditor_IsRedirectedToLogin()
    {
        var response = await _client.GetAsync("/Admin/AudioEditor");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("/Admin/MushafRegionEditor/1")]
    [InlineData("/Admin/CorpusMapping")]
    [InlineData("/Admin/Validation")]
    public async Task RetiredAdminPage_ReturnsNotFound(string path)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task AnonymousUser_CallingAdminRecitationsApi_IsBlocked()
    {
        var response = await _client.GetAsync("/api/admin/Recitations");
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Redirect);
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
        }
    }

    [Fact]
    public async Task AnonymousUser_CallingAnnotationsImport_IsBlocked()
    {
        var content = JsonContent.Create(new { edition = "marw-newMushaf", source = new { storageKey = "sources/002/original.mp3", sha256 = "dummy" }, passages = Array.Empty<object>() });
        var response = await _client.PostAsync("/api/annotations/import", content);
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task AnonymousUser_CallingAnnotationsPublish_IsBlocked()
    {
        var content = JsonContent.Create(new { version = 1, referenceChecked = true });
        var response = await _client.PostAsync($"/api/annotations/{Guid.NewGuid()}/publish", content);
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task AnonymousUser_CallingAnnotationsDelete_IsBlocked()
    {
        var response = await _client.DeleteAsync($"/api/annotations/{Guid.NewGuid()}?version=1");
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task AnonymousUser_CallingAnnotationsPreview_IsBlocked()
    {
        var response = await _client.GetAsync("/api/annotations/preview?reference=2:1");
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task AnonymousUser_CallingPrivateSourceAudio_IsBlocked()
    {
        var response = await _client.GetAsync("/audio/sources/002/original.mp3");
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task AnonymousUser_CallingPrivateWorkingAudio_IsBlocked()
    {
        var response = await _client.GetAsync("/audio/working/temp.mp3");
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task AnonymousUser_CallingPublicPagesAndApis_Succeeds()
    {
        // 1. Home page
        var homeRes = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeRes.StatusCode);

        // 2. Mushaf public page
        var mushafRes = await _client.GetAsync("/Mushaf/Page?pageNumber=1");
        Assert.Equal(HttpStatusCode.OK, mushafRes.StatusCode);

        // 3. Public playback API
        var playbackRes = await _client.GetAsync("/api/annotations/playback?reference=2:1");
        Assert.Equal(HttpStatusCode.OK, playbackRes.StatusCode);

        // 4. Public catalog API
        var catalogRes = await _client.GetAsync("/api/awjuh/catalog");
        Assert.Equal(HttpStatusCode.OK, catalogRes.StatusCode);

        // 5. Login page
        var loginRes = await _client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedAdmin_CanAccessAdminDashboard()
    {
        var authClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = true
        });

        var loginPageResponse = await authClient.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, loginPageResponse.StatusCode);
        var loginHtml = await loginPageResponse.Content.ReadAsStringAsync();

        var tokenMatch = System.Text.RegularExpressions.Regex.Match(loginHtml, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        var token = tokenMatch.Success ? tokenMatch.Groups[1].Value : "";

        var formData = new Dictionary<string, string>
        {
            ["Input.Email"] = "admin@quranwarsh.local",
            ["Input.Password"] = "Admin@123456",
            ["Input.RememberMe"] = "false"
        };
        if (!string.IsNullOrEmpty(token))
        {
            formData["__RequestVerificationToken"] = token;
        }

        var postLogin = await authClient.PostAsync("/Account/Login", new FormUrlEncodedContent(formData));
        Assert.Equal(HttpStatusCode.OK, postLogin.StatusCode);

        var adminRes = await authClient.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.OK, adminRes.StatusCode);
        var adminHtml = await adminRes.Content.ReadAsStringAsync();
        Assert.Contains("لوحة المتابعة", adminHtml);

        Assert.DoesNotContain("CorpusMapping", adminHtml);
        Assert.DoesNotContain("MushafRegionEditor", adminHtml);
        Assert.Equal(HttpStatusCode.NotFound, (await authClient.GetAsync("/Admin/CorpusMapping")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await authClient.GetAsync("/api/admin/Recitations")).StatusCode);

    }

}
