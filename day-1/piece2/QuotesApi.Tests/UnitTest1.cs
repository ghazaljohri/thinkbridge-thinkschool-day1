using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using QuotesApi.Extensions;

namespace QuotesApi.Tests;

public sealed class AuthorizationTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"quotes-api-tests-{Guid.NewGuid():N}.db");
    private QuotesApiFactory? _factory;

    public Task InitializeAsync()
    {
        _factory = new QuotesApiFactory(_databasePath);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();

        if (File.Exists(_databasePath))
            File.Delete(_databasePath);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Unauthenticated_post_returns_401()
    {
        using var client = _factory!.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/quotes",
            new QuoteEndpointExtensions.QuoteRequest("test@example.com", "A quote"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_with_scope_can_create_quote()
    {
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(client));

        var response = await client.PostAsJsonAsync(
            "/api/quotes",
            new QuoteEndpointExtensions.QuoteRequest("test@example.com", "A quote"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_cannot_delete_someone_elses_quote()
    {
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(client));

        var quoteId = await CreateQuoteAsync(client, "other@example.com");

        var response = await client.DeleteAsync($"/api/quotes/{quoteId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_can_delete_their_own_quote()
    {
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(client));

        var quoteId = await CreateQuoteAsync(client, "test@example.com");

        var response = await client.DeleteAsync($"/api/quotes/{quoteId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<int> CreateQuoteAsync(HttpClient client, string author)
    {
        var response = await client.PostAsJsonAsync(
            "/api/quotes",
            new QuoteEndpointExtensions.QuoteRequest(author, "A quote"));
        response.EnsureSuccessStatusCode();

        return int.Parse(response.Headers.Location!
            .OriginalString
            .TrimEnd('/')
            .Split('/')
            .Last());
    }

    private static async Task<string> GetAccessTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new AuthEndpointExtensions.LoginRequest("test@example.com", "Password123!"));
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return Assert.IsType<string>(token?.AccessToken);
    }

    private sealed record LoginResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);

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
