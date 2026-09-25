using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Quran.Application.DTOs;
using Quran.Domain.Enums;
using Xunit;

namespace Quran.IntegrationTests;

public class ReviewWorkflowIntegrationTests : IClassFixture<QuranTestWebApplicationFactory>
{
    private readonly QuranTestWebApplicationFactory _factory;
    private readonly HttpClient _anonClient;

    public ReviewWorkflowIntegrationTests(QuranTestWebApplicationFactory factory)
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

        var adminHtml = await client.GetStringAsync("/Admin");
        var csrf = Regex.Match(adminHtml, "name=\"csrf-token\" content=\"([^\"]+)\"");
        Assert.True(csrf.Success);
        client.DefaultRequestHeaders.Add("RequestVerificationToken", System.Net.WebUtility.HtmlDecode(csrf.Groups[1].Value));
        return client;
    }

    [Fact]
    public async Task AnonymousUser_CannotAccessReviewQueuePageOrApi()
    {
        var pageRes = await _anonClient.GetAsync("/Admin/ReviewQueue");
        Assert.True(pageRes.StatusCode == HttpStatusCode.Redirect || pageRes.StatusCode == HttpStatusCode.Unauthorized);

        var apiRes = await _anonClient.GetAsync("/api/admin/audio-editor/review-queue");
        Assert.True(apiRes.StatusCode == HttpStatusCode.Redirect || apiRes.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReviewQueuePage_AuthenticatedUser_ReturnsSuccess()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var response = await authClient.GetAsync("/Admin/ReviewQueue");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("طابور المراجعة والاعتماد والنشر", html);
    }

    [Fact]
    public async Task ReviewWorkflow_Endpoints_EnforceValidationAndStateTransitions()
    {
        var authClient = await CreateAuthenticatedClientAsync();

        // 1. Get editor context for Surah 1
        var ctxRes = await authClient.GetAsync("/api/admin/audio-editor/surah/1");
        Assert.Equal(HttpStatusCode.OK, ctxRes.StatusCode);
        var ctx = await ctxRes.Content.ReadFromJsonAsync<AudioEditorContextDto>();
        Assert.NotNull(ctx);

        var ayah = ctx.Ayahs.First();

        // 2. Create Draft segment
        var createReq = new CaptureEndAyahRequest(ctx.SourceAudio.Id, 6000, 2000, "1:1", null, null,
            "Test review pipeline", "MARW_newMushaf_20260913-full", null, Guid.NewGuid().ToString());
        var createRes = await authClient.PostAsJsonAsync("/api/admin/audio-editor/capture/end-ayah", createReq);
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var capture = await createRes.Content.ReadFromJsonAsync<CaptureEndAyahResult>();
        var segment = capture!.SavedOccurrence;
        Assert.NotNull(segment);
        Assert.Equal(AudioSegmentStatus.Draft, segment.Status);

        try
        {
            // 3. Submit for review
            var submitRes = await authClient.PostAsync($"/api/admin/audio-editor/segments/{segment.Id}/submit-review", null);
            Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);
            var reviewed = await submitRes.Content.ReadFromJsonAsync<AudioSegmentDto>();
            Assert.NotNull(reviewed);
            Assert.Equal(AudioSegmentStatus.Reviewed, reviewed.Status);

            // 4. Reject with empty reason -> should fail with 400
            var badReject = await authClient.PostAsJsonAsync($"/api/admin/audio-editor/segments/{segment.Id}/reject", new RejectSegmentRequest("   "));
            Assert.Equal(HttpStatusCode.BadRequest, badReject.StatusCode);

            // 5. Reject with valid reason -> 200 OK and status Rejected
            var rejectRes = await authClient.PostAsJsonAsync($"/api/admin/audio-editor/segments/{segment.Id}/reject", new RejectSegmentRequest("تداخل في الصوت وبتر عند البداية"));
            Assert.Equal(HttpStatusCode.OK, rejectRes.StatusCode);
            var rejected = await rejectRes.Content.ReadFromJsonAsync<AudioSegmentDto>();
            Assert.NotNull(rejected);
            Assert.Equal(AudioSegmentStatus.Rejected, rejected.Status);
            Assert.Equal("تداخل في الصوت وبتر عند البداية", rejected.RejectionReason);

            // 6. Resubmit for review
            var resubmitRes = await authClient.PostAsync($"/api/admin/audio-editor/segments/{segment.Id}/submit-review", null);
            Assert.Equal(HttpStatusCode.OK, resubmitRes.StatusCode);

            // 7. Approve
            var approveRes = await authClient.PostAsync($"/api/admin/audio-editor/segments/{segment.Id}/approve", null);
            Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);
            var approved = await approveRes.Content.ReadFromJsonAsync<AudioSegmentDto>();
            Assert.NotNull(approved);
            Assert.Equal(AudioSegmentStatus.Approved, approved.Status);
            Assert.Null(approved.RejectionReason);

            // 8. Query Review Queue via API
            var queueRes = await authClient.GetAsync($"/api/admin/audio-editor/review-queue?surahNumber=1&status=Approved");
            Assert.Equal(HttpStatusCode.OK, queueRes.StatusCode);
            var queue = await queueRes.Content.ReadFromJsonAsync<List<AudioSegmentDto>>();
            Assert.NotNull(queue);
            Assert.Contains(queue, q => q.Id == segment.Id);
        }
        finally
        {
            // Cleanup test segment
            await authClient.DeleteAsync($"/api/admin/audio-editor/segments/{segment.Id}");
        }
    }
}
