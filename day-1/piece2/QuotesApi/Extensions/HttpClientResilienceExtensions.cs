using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace QuotesApi.Extensions;

public static class HttpClientResilienceExtensions
{
    public static IHttpResiliencePipelineBuilder AddDefaultResilience(
        this IHttpClientBuilder builder,
        string pipelineName)
    {
        return builder.AddResilienceHandler(pipelineName, (resilienceBuilder, context) =>
        {
            var logger = context.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger($"QuotesApi.Resilience.{pipelineName}");

            resilienceBuilder
                // Outermost: caps total time across every attempt, not per attempt.
                .AddTimeout(TimeSpan.FromSeconds(10))
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    // Polly's default base delay is 2s, so 3 exponential retries alone
                    // (2s, 4s, 8s = 14s) can exceed the 10s total timeout before any
                    // request even completes. 200ms keeps cumulative backoff under ~1.5s,
                    // leaving the rest of the budget for actual request time.
                    Delay = TimeSpan.FromMilliseconds(200),
                    OnRetry = args =>
                    {
                        logger.LogWarning(
                            args.Outcome.Exception,
                            "Retry {AttemptNumber} for {Pipeline} after {DelayMs}ms, outcome: {StatusCode}",
                            args.AttemptNumber + 1,
                            pipelineName,
                            args.RetryDelay.TotalMilliseconds,
                            args.Outcome.Result?.StatusCode);
                        return default;
                    }
                })
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 4,
                    BreakDuration = TimeSpan.FromSeconds(30),
                    OnOpened = args =>
                    {
                        logger.LogError(
                            "Circuit breaker opened for {Pipeline} for {BreakDurationSeconds}s",
                            pipelineName,
                            args.BreakDuration.TotalSeconds);
                        return default;
                    },
                    OnClosed = _ =>
                    {
                        logger.LogInformation("Circuit breaker closed for {Pipeline}", pipelineName);
                        return default;
                    }
                });
        });
    }
}
