using System.Net;

using ConduitLLM.Core.Policies;
using ConduitLLM.Providers;

namespace ConduitLLM.Tests.Core.Policies;

public sealed class HttpRetryPoliciesTests
{
    [Fact]
    public async Task CoreAndProviderPolicies_RetryTheSameTransientStatuses()
    {
        var coreAttempts = await ExecuteWithFailuresAsync(
            HttpRetryPolicies.GetStandardRetryPolicy(
                maxRetries: 2,
                initialDelay: TimeSpan.FromMilliseconds(1),
                maxDelay: TimeSpan.FromMilliseconds(1)));
        var providerAttempts = await ExecuteWithFailuresAsync(
            ResiliencePolicies.GetRetryPolicy(
                maxRetries: 2,
                initialDelay: TimeSpan.FromMilliseconds(1),
                maxDelay: TimeSpan.FromMilliseconds(1)));

        Assert.Equal(3, coreAttempts);
        Assert.Equal(coreAttempts, providerAttempts);
    }

    private static async Task<int> ExecuteWithFailuresAsync(
        Polly.IAsyncPolicy<HttpResponseMessage> policy)
    {
        var attempts = 0;
        using var response = await policy.ExecuteAsync(() =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(
                attempts < 3
                    ? HttpStatusCode.ServiceUnavailable
                    : HttpStatusCode.OK));
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return attempts;
    }
}
