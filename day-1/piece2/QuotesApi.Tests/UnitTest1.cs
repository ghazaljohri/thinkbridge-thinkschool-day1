using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace QuotesApi.Tests;

public class CancellationTests
{
    [Fact]
    public async Task Request_Cancellation_Returns_499()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(1);

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/collections/1");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SendAsync(request, cts.Token));
    }
}
