using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Data;
using QuotesApi.Extensions;
using QuotesApi.Models;

namespace Quotes.Tests.Integration;

public sealed class QuotesApiIntegrationTests(SqlServerContainerFixture sqlServer)
    : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public async Task GetRoot_ApplicationStarted_ReturnsRunningMessage()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Quotes API is running!");
    }

    [Fact]
    public async Task GetQuotes_NoAuthentication_ReturnsPagedQuotes()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/quotes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetQuotes_InvalidPage_ReturnsValidationProblemDetails()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/quotes?page=0");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        problem!.Title.Should().Be("One or more validation errors occurred.");
        problem.Errors.Should().ContainKey("page");
    }

    [Fact]
    public async Task PostQuote_NoAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/quotes", new QuoteEndpointExtensions.QuoteRequest("test@example.com", "A quote"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostQuote_ExpiredLocalJwt_ReturnsUnauthorized()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();
        var expiredToken = CreateExpiredLocalAccessToken(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/quotes", new QuoteEndpointExtensions.QuoteRequest("test@example.com", "A quote"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostQuote_EntraShapedToken_RoutesToEntraValidationAndReturnsUnauthorized()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();
        var entraShapedToken = CreateEntraShapedAccessToken(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", entraShapedToken);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/quotes", new QuoteEndpointExtensions.QuoteRequest("test@example.com", "A quote"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostQuote_AuthenticatedWithLocalJwt_ReturnsCreated()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/quotes", new QuoteEndpointExtensions.QuoteRequest("test@example.com", "A quote"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.Should().MatchRegex("^/api/quotes/[0-9]+$");
    }

    [Fact]
    public async Task PostQuote_InvalidRequest_ReturnsValidationProblemDetails()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/quotes", new QuoteEndpointExtensions.QuoteRequest("", "A quote"));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        problem!.Errors.Should().ContainKey("author");
        problem.Errors["author"].Should().Contain(error => error.Contains("Author is required."));
    }

    [Fact]
    public async Task GetQuote_ExistingQuote_ReturnsQuote()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();
        var quoteId = await SeedQuoteAsync(factory, "test@example.com", "Stored quote");

        // Act
        var response = await client.GetAsync($"/api/quotes/{quoteId}");
        var quote = await response.Content.ReadFromJsonAsync<QuoteResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        quote!.Author.Should().Be("test@example.com");
        quote.Text.Should().Be("Stored quote");
    }

    [Fact]
    public async Task GetQuote_MissingQuote_ReturnsNotFound()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/quotes/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteQuote_NoAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();
        var quoteId = await SeedQuoteAsync(factory, "test@example.com", "Stored quote");

        // Act
        var response = await client.DeleteAsync($"/api/quotes/{quoteId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteQuote_AuthenticatedOwner_ReturnsNoContent()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        var quoteId = await SeedQuoteAsync(factory, "test@example.com", "Stored quote");

        // Act
        var response = await client.DeleteAsync($"/api/quotes/{quoteId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteQuote_AuthenticatedNonOwner_ReturnsForbidden()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        var quoteId = await SeedQuoteAsync(factory, "other@example.com", "Stored quote");

        // Act
        var response = await client.DeleteAsync($"/api/quotes/{quoteId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostCollection_ValidRequest_ReturnsCreated()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/collections", new CollectionEndpointExtensions.CollectionRequest("Favourites", 1));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetCollectionsByOwner_MultipleCollectionsForOwner_ReturnsSummariesOnlyForThatOwner()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();
        var quoteId = await SeedQuoteAsync(factory, "test@example.com", "Stored quote");
        var owned = GetId(await client.PostAsJsonAsync(
            "/api/collections", new CollectionEndpointExtensions.CollectionRequest("Favourites", 1)));
        await client.PostAsJsonAsync(
            "/api/collections", new CollectionEndpointExtensions.CollectionRequest("Someone Else's", 2));
        await client.PostAsJsonAsync(
            $"/api/collections/{owned}/items",
            new CollectionEndpointExtensions.AddCollectionItemRequest(quoteId));

        // Act
        var response = await client.GetAsync("/api/collections?ownerId=1");
        var summaries = await response.Content.ReadFromJsonAsync<List<CollectionSummaryResponse>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        summaries.Should().ContainSingle();
        summaries!.Single().Id.Should().Be(owned);
        summaries.Single().ItemCount.Should().Be(1);
    }

    [Fact]
    public async Task GetCollection_ExistingAndMissingCollection_ReturnsOkThenNotFound()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync(
            "/api/collections", new CollectionEndpointExtensions.CollectionRequest("Favourites", 1));
        var collectionId = GetId(created);

        // Act
        var existing = await client.GetAsync($"/api/collections/{collectionId}");
        var missing = await client.GetAsync("/api/collections/999");

        // Assert
        existing.StatusCode.Should().Be(HttpStatusCode.OK);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostCollectionItem_ExistingCollectionAndQuote_StoresFixedClockTimestamp()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();
        var quoteId = await SeedQuoteAsync(factory, "test@example.com", "Stored quote");
        var collection = await client.PostAsJsonAsync(
            "/api/collections", new CollectionEndpointExtensions.CollectionRequest("Favourites", 1));
        var collectionId = GetId(collection);

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/collections/{collectionId}/items",
            new CollectionEndpointExtensions.AddCollectionItemRequest(quoteId));
        var stored = await GetCollectionAsync(client, collectionId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stored.Items.Should().ContainSingle();
        stored.Items.Single().AddedAt.Should().Be(QuotesApiFactory.FixedUtcNow.UtcDateTime);
    }

    [Fact]
    public async Task PostCollectionItem_MissingCollection_ReturnsNotFound()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/collections/999/items", new CollectionEndpointExtensions.AddCollectionItemRequest(1));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCollectionItem_ExistingThenMissingItem_ReturnsNoContentThenProblemDetails()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();
        var quoteId = await SeedQuoteAsync(factory, "test@example.com", "Stored quote");
        var collection = await client.PostAsJsonAsync(
            "/api/collections", new CollectionEndpointExtensions.CollectionRequest("Favourites", 1));
        var collectionId = GetId(collection);
        await client.PostAsJsonAsync(
            $"/api/collections/{collectionId}/items",
            new CollectionEndpointExtensions.AddCollectionItemRequest(quoteId));

        // Act
        var deleted = await client.DeleteAsync($"/api/collections/{collectionId}/items/{quoteId}");
        var missing = await client.DeleteAsync($"/api/collections/{collectionId}/items/{quoteId}");

        // Assert
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        missing.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        missing.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Login_ValidAndInvalidCredentials_ReturnsOkThenUnauthorized()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();

        // Act
        var valid = await client.PostAsJsonAsync(
            "/api/auth/login", new AuthEndpointExtensions.LoginRequest("test@example.com", "Password123!"));
        var invalid = await client.PostAsJsonAsync(
            "/api/auth/login", new AuthEndpointExtensions.LoginRequest("test@example.com", "wrong"));

        // Assert
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
        invalid.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ValidThenReusedToken_ReturnsOkThenUnauthorized()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);

        // Act
        var firstUse = await client.PostAsJsonAsync(
            "/api/auth/refresh", new AuthEndpointExtensions.RefreshRequest(login.RefreshToken));
        var reuse = await client.PostAsJsonAsync(
            "/api/auth/refresh", new AuthEndpointExtensions.RefreshRequest(login.RefreshToken));

        // Assert
        firstUse.StatusCode.Should().Be(HttpStatusCode.OK);
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Database_ApplicationStarted_AppliesMigrations()
    {
        // Arrange
        await using var factory = new QuotesApiFactory(sqlServer);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;

        // Act
        var appliedMigrations = await database.GetAppliedMigrationsAsync();

        // Assert
        appliedMigrations.Should().NotBeEmpty();
    }

    private static async Task AuthenticateAsync(HttpClient client)
    {
        // Arrange
        var login = await LoginAsync(client);

        // Act
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Assert
        client.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");
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

    private static string CreateExpiredLocalAccessToken(QuotesApiFactory factory)
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var key = scope.ServiceProvider.GetRequiredService<IConfiguration>()["Jwt:SigningKey"]!;
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "1"),
            new Claim(JwtRegisteredClaimNames.Email, "test@example.com"),
            new Claim("scope", "quotes.write")
        };

        // Act
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateEntraShapedAccessToken(QuotesApiFactory factory)
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var tenantId = scope.ServiceProvider.GetRequiredService<IConfiguration>()["Entra:TenantId"]!;
        var entraAuthority = $"https://login.microsoftonline.com/{tenantId}/v2.0";

        // Act
        var token = new JwtSecurityToken(issuer: entraAuthority, claims: []);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<int> SeedQuoteAsync(QuotesApiFactory factory, string author, string text)
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quote = Quote.Create(author, text, DateTimeOffset.UtcNow);

        // Act
        database.Quotes.Add(quote);
        await database.SaveChangesAsync();

        // Assert
        quote.Id.Should().BePositive();
        return quote.Id;
    }

    private static int GetId(HttpResponseMessage response)
    {
        // Arrange
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        var id = int.Parse(response.Headers.Location!.OriginalString.Split('/').Last());

        // Assert
        id.Should().BePositive();
        return id;
    }

    private static async Task<CollectionResponse> GetCollectionAsync(HttpClient client, int collectionId)
    {
        // Arrange

        // Act
        var response = await client.GetAsync($"/api/collections/{collectionId}");
        var collection = await response.Content.ReadFromJsonAsync<CollectionResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return collection!;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken);

    private sealed record QuoteResponse(string Author, string Text);

    private sealed record CollectionResponse(List<CollectionItemResponse> Items);

    private sealed record CollectionItemResponse(int QuoteId, DateTime AddedAt);

    private sealed record CollectionSummaryResponse(int Id, string Name, int OwnerId, int ItemCount, DateTime? LastAddedAt);

    private sealed record ProblemDetailsResponse(
        string? Title,
        Dictionary<string, string[]> Errors);
}
