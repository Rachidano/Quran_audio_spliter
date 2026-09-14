using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Quran.Application.DTOs;
using Xunit;

namespace Quran.IntegrationTests;

public class AudioEditorEndToEndIntegrationTests : IClassFixture<QuranTestWebApplicationFactory>
{
    private readonly QuranTestWebApplicationFactory _factory;
    private readonly HttpClient _anonClient;

    public AudioEditorEndToEndIntegrationTests(QuranTestWebApplicationFactory factory)
    {
        _factory = factory;
        _anonClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = true
        });

        var loginPageResponse = await client.GetAsync("/Account/Login");
        var loginHtml = await loginPageResponse.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(loginHtml, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
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

        var postLogin = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(formData));
        Assert.Equal(HttpStatusCode.OK, postLogin.StatusCode);

        return client;
    }

    [Fact]
    public async Task AnonymousUser_CallingAudioEditorEndpoints_IsBlocked()
    {
        var peaksRes = await _anonClient.GetAsync("/api/admin/audio-editor/peaks/1");
        Assert.True(peaksRes.StatusCode == HttpStatusCode.Unauthorized || peaksRes.StatusCode == HttpStatusCode.Redirect);
        if (peaksRes.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Contains("/Account/Login", peaksRes.Headers.Location?.ToString());
        }

        var endAyahRes = await _anonClient.PostAsJsonAsync("/api/admin/audio-editor/capture/end-ayah", new { });
        Assert.True(endAyahRes.StatusCode == HttpStatusCode.Unauthorized || endAyahRes.StatusCode == HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task LegacyAudioReviewPage_RedirectsToAdminAudioEditor()
    {
        var res = await _anonClient.GetAsync("/Mushaf/AudioReview");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("/Admin/AudioEditor", res.Headers.Location?.ToString());
    }

    [Fact]
    public async Task AuthenticatedAdmin_GetPeaks_ReturnsValidData()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        var res = await authClient.GetAsync("/api/admin/audio-editor/peaks/1");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var peaks = await res.Content.ReadFromJsonAsync<AudioPeaksDto>();
        Assert.NotNull(peaks);
    }

    [Fact]
    public async Task AuthenticatedAdmin_GetMarwAyahRegion_ReturnsValidCoordinates()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        var res = await authClient.GetAsync("/api/admin/audio-editor/marw-ayah/1/1");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var content = await res.Content.ReadAsStringAsync();
        Assert.Contains("pageNumber", content);
        Assert.Contains("pathData", content);
        Assert.Contains("imageUrl", content);
    }

    [Fact]
    public async Task AuthenticatedAdmin_CaptureEndAyah_And_Repeat_And_Undo_Flow_WorksSeamlessly()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        // 1. Get Context for Surah 1 (Al-Fatiha)
        var contextRes = await authClient.GetAsync("/api/admin/audio-editor/surah/1");
        Assert.Equal(HttpStatusCode.OK, contextRes.StatusCode);
        var context = await contextRes.Content.ReadFromJsonAsync<AudioEditorContextDto>();
        Assert.NotNull(context);
        var sourceAudioId = context.SourceAudio.Id;

        // 2. Capture Ayah 1 (from 0ms to 4000ms)
        var endAyahReq = new CaptureEndAyahRequest(
            SourceAudioId: sourceAudioId,
            CapturedTimeMs: 4000,
            CurrentStartMs: 0,
            CurrentReference: "1:1",
            CurrentAyahId: context.Ayahs.FirstOrDefault(a => a.AyahNumber == 1)?.Id,
            WajhId: null,
            Notes: "Lot 5 test capture",
            Edition: "MARW_newMushaf_20260913-full",
            SourceHash: context.SourceAudio.StorageKey,
            IdempotencyKey: Guid.NewGuid().ToString()
        );

        var capRes = await authClient.PostAsJsonAsync("/api/admin/audio-editor/capture/end-ayah", endAyahReq);
        Assert.Equal(HttpStatusCode.OK, capRes.StatusCode);
        var capResult = await capRes.Content.ReadFromJsonAsync<CaptureEndAyahResult>();
        Assert.NotNull(capResult);
        Assert.Equal(4000, capResult.NextStartMs);
        Assert.Equal(2, capResult.NextAyahNumber);

        var capturedSegmentId = capResult.SavedOccurrence.Id;

        // 3. Capture Repeat for Ayah 1 (from 4000ms to 7500ms)
        var repeatReq = new CaptureRepeatRequest(
            SourceAudioId: sourceAudioId,
            TargetSegmentId: capturedSegmentId,
            StartMs: 4000,
            EndMs: 7500,
            WajhId: null,
            Notes: "Repeat test",
            Edition: "MARW_newMushaf_20260913-full",
            SourceHash: context.SourceAudio.StorageKey
        );

        var repeatRes = await authClient.PostAsJsonAsync("/api/admin/audio-editor/capture/repeat", repeatReq);
        Assert.Equal(HttpStatusCode.OK, repeatRes.StatusCode);
        var repeatResult = await repeatRes.Content.ReadFromJsonAsync<AudioSegmentDto>();
        Assert.NotNull(repeatResult);
        Assert.Equal(4000, repeatResult.StartMs);
        Assert.Equal(7500, repeatResult.EndMs);

        // 4. Undo Repeat
        var undoRepeatRes = await authClient.PostAsync($"/api/admin/audio-editor/capture/undo?sourceAudioId={sourceAudioId}", null);
        Assert.Equal(HttpStatusCode.OK, undoRepeatRes.StatusCode);
        var undoRepeat = await undoRepeatRes.Content.ReadFromJsonAsync<UndoCaptureResult>();
        Assert.NotNull(undoRepeat);
        Assert.True(undoRepeat.Success);
        Assert.Equal(repeatResult.Id, undoRepeat.RemovedSegmentId);

        // 5. Undo Ayah 1
        var undoAyahRes = await authClient.PostAsync($"/api/admin/audio-editor/capture/undo?sourceAudioId={sourceAudioId}", null);
        Assert.Equal(HttpStatusCode.OK, undoAyahRes.StatusCode);
        var undoAyah = await undoAyahRes.Content.ReadFromJsonAsync<UndoCaptureResult>();
        Assert.NotNull(undoAyah);
        Assert.True(undoAyah.Success);
        Assert.Equal(capturedSegmentId, undoAyah.RemovedSegmentId);
        Assert.Equal(0, undoAyah.RestoredTimeMs);
        Assert.Equal(1, undoAyah.RestoredAyahNumber);
    }
}
