using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using QuotesApi.Extensions;

namespace Quotes.Tests.Unit;

public sealed class AuthRefreshEndpointTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"quotes-unit-tests-{Guid.NewGuid():N}.db");
    private QuotesApiFactory? _factory;

    public Task InitializeAsync()
    {
        // Arrange
        _factory = new QuotesApiFactory(_databasePath);

        // Act
        // Assert
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // Arrange
        _factory?.Dispose();

        // Act
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);

        // Assert
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Refresh_FirstUseOfValidToken_ReturnsRotatedTokens()
    {
        // Arrange
        using var client = _factory!.CreateClient();
        var login = await LoginAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new AuthEndpointExtensions.RefreshRequest(login.RefreshToken));
        var refreshed = await response.Content.ReadFromJsonAsync<TokenResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        refreshed.Should().NotBeNull();
        refreshed!.AccessToken.Should().NotBeNullOrWhiteSpace();
        refreshed.RefreshToken.Should().NotBe(login.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ReusedRevokedToken_ReturnsUnauthorized()
    {
        // Arrange
        using var client = _factory!.CreateClient();
        var login = await LoginAsync(client);
        var firstUse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new AuthEndpointExtensions.RefreshRequest(login.RefreshToken));
        firstUse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var reuse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new AuthEndpointExtensions.RefreshRequest(login.RefreshToken));

        // Assert
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<TokenResponse> LoginAsync(HttpClient client)
    {
        // Arrange
        var request = new AuthEndpointExtensions.LoginRequest("test@example.com", "Password123!");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", request);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        token.Should().NotBeNull();
        return token!;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken);

    private sealed class QuotesApiFactory(string databasePath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}"
                });
            });
        }
    }
}
