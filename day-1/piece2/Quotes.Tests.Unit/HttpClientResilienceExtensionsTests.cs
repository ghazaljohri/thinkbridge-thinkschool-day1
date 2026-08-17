using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using QuotesApi.Extensions;
using Xunit.Abstractions;

namespace Quotes.Tests.Unit;

public class HttpClientResilienceExtensionsTests(ITestOutputHelper output)
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
    public async Task SendAsync_TransientFailuresThenSuccess_LogsEachRetry()
    {
        var handler = new ScriptedHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK);
        var logs = new CapturingLoggerProvider();

        using var client = BuildClient(handler, logs);
        var response = await client.GetAsync("https://entra-metadata.test/.well-known/openid-configuration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var line in logs.Messages)
        {
            output.WriteLine(line);
        }

        var retryLogs = logs.Messages
            .Where(m => m.Contains("QuotesApi.Resilience.test") && m.Contains("Retry"))
            .ToList();
        retryLogs.Should().HaveCount(2, "two 503s were returned before the third attempt succeeded");
        retryLogs[0].Should().Contain("Retry 1 for test");
        retryLogs[1].Should().Contain("Retry 2 for test");
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

    private static HttpClient BuildClient(HttpMessageHandler primaryHandler, ILoggerProvider? loggerProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            if (loggerProvider is not null)
            {
                b.AddProvider(loggerProvider);
            }
        });
        services.AddHttpClient("test")
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler)
            .AddDefaultResilience("test");

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string categoryName, List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (messages)
                {
                    messages.Add($"[{logLevel}] {categoryName}: {formatter(state, exception)}");
                }
            }
        }
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
