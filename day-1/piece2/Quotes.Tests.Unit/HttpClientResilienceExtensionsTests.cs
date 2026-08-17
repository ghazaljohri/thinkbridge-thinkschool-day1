using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using QuotesApi.Extensions;

namespace Quotes.Tests.Unit;

public class HttpClientResilienceExtensionsTests
{
    [Fact]
    public async Task SendAsync_TransientFailuresThenSuccess_RetriesUntilSuccessful()
    {
        var handler = new ScriptedHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK);

        using var client = BuildClient(handler);

        var response = await client.GetAsync("https://entra-metadata.test/.well-known/openid-configuration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.AttemptCount.Should().Be(3);
    }

    [Fact]
    public async Task SendAsync_PersistentFailures_OpensCircuitAfterMinimumThroughput()
    {
        var handler = new ScriptedHandler(alwaysReturn: HttpStatusCode.ServiceUnavailable);
        using var client = BuildClient(handler);

        // A failing 503 isn't an exception - HttpClient only throws on transport
        // failures, not non-2xx statuses - so once retries are exhausted the call
        // just returns the last 503. The single outer call retries 3 times, so it
        // already delivers 4 failure data points to the circuit breaker: at its
        // MinimumThroughput of 4 with a 100% failure ratio, well past the 50%
        // threshold, so the circuit opens during this very call.
        var firstResponse = await client.GetAsync("https://entra-metadata.test/.well-known/openid-configuration");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var attemptsAfterFirstCall = handler.AttemptCount;
        attemptsAfterFirstCall.Should().Be(4);

        // With the circuit now open, a follow-up call should fail fast without
        // reaching the handler at all - the attempt count must not grow.
        var secondCallAct = async () => await client.GetAsync("https://entra-metadata.test/.well-known/openid-configuration");
        await secondCallAct.Should().ThrowAsync<BrokenCircuitException>();
        handler.AttemptCount.Should().Be(attemptsAfterFirstCall);
    }

    private static HttpClient BuildClient(HttpMessageHandler primaryHandler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient("test")
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler)
            .AddDefaultResilience("test");

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode>? _scripted;
        private readonly HttpStatusCode? _alwaysReturn;

        public int AttemptCount { get; private set; }

        public ScriptedHandler(params HttpStatusCode[] scripted)
        {
            _scripted = new Queue<HttpStatusCode>(scripted);
        }

        public ScriptedHandler(HttpStatusCode alwaysReturn)
        {
            _alwaysReturn = alwaysReturn;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AttemptCount++;
            var statusCode = _alwaysReturn ?? _scripted!.Dequeue();
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
