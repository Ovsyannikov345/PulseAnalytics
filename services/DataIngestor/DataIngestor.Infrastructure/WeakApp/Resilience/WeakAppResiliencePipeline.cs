using System.Net;
using DataIngestor.Infrastructure.WeakApp.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace DataIngestor.Infrastructure.WeakApp.Resilience;

internal static class WeakAppResiliencePipeline
{
    public const string Name = "weak-app";

    public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder, WeakAppResilienceSettings settings)
    {
        builder
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = settings.CircuitBreakerFailureRatio,
                MinimumThroughput = settings.CircuitBreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(settings.CircuitBreakerSamplingDurationInSeconds),
                BreakDuration = TimeSpan.FromSeconds(settings.CircuitBreakerBreakDurationInSeconds),
                ShouldHandle = ShouldBreak
            })
            .AddTimeout(TimeSpan.FromSeconds(settings.TotalTimeoutInSeconds))
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = settings.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(settings.RetryBaseDelayInSeconds),
                ShouldHandle = ShouldRetry
            })
            .AddTimeout(TimeSpan.FromSeconds(settings.AttemptTimeoutInSeconds));
    }

    private static ValueTask<bool> ShouldRetry(RetryPredicateArguments<HttpResponseMessage> args)
    {
        var shouldRetry = HttpClientResiliencePredicates.IsTransient(args.Outcome)
            && !IsRateLimited(args.Outcome);

        return ValueTask.FromResult(shouldRetry);
    }

    private static ValueTask<bool> ShouldBreak(CircuitBreakerPredicateArguments<HttpResponseMessage> args)
    {
        var shouldBreak = HttpClientResiliencePredicates.IsTransient(args.Outcome)
            || IsRateLimited(args.Outcome);

        return ValueTask.FromResult(shouldBreak);
    }

    private static bool IsRateLimited(Outcome<HttpResponseMessage> outcome)
    {
        return outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests;
    }
}
