using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Quran.IntegrationTests;

public class QualityReportIntegrationTests : IClassFixture<QuranTestWebApplicationFactory>
{
    private readonly QuranTestWebApplicationFactory _factory;
    private readonly HttpClient _anonClient;

    public QualityReportIntegrationTests(QuranTestWebApplicationFactory factory)
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
    public async Task AnonymousUser_CannotAccessQualityReportPage()
    {
        var response = await _anonClient.GetAsync("/Admin/QualityReport");
        Assert.True(response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedAdmin_CanAccessQualityReportPage()
    {
        var authClient = await CreateAuthenticatedClientAsync();
        var response = await authClient.GetAsync("/Admin/QualityReport");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("تقرير الجودة ومراقبة التغطية الصوتية", html);
        Assert.Contains("مصفوفة التغطية الإجمالية للسور", html);
    }
}
