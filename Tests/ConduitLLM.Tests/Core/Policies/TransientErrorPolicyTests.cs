using System.Net;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Policies;

namespace ConduitLLM.Tests.Core.Policies;

public sealed class TransientErrorPolicyTests
{
    [Fact]
    public void CallerCancellation_IsNeverTransient()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.False(TransientErrorPolicy.IsTransient(
            new TaskCanceledException("caller aborted"),
            source.Token));
        Assert.False(TransientErrorPolicy.IsTransient(
            new HttpRequestException("request stopped"),
            source.Token));
    }

    [Fact]
    public void TimeoutWithoutCallerCancellation_IsTransient()
    {
        Assert.True(TransientErrorPolicy.IsTransient(new TaskCanceledException("timeout")));
        Assert.True(TransientErrorPolicy.IsTransient(new RequestTimeoutException("timeout")));
    }

    [Fact]
    public void TransientInnerException_IsDetected()
    {
        var exception = new InvalidOperationException(
            "wrapper",
            new IOException("connection reset"));

        Assert.True(TransientErrorPolicy.IsTransient(exception));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.NotImplemented, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    public void ProviderStatus_UsesProviderErrorClassification(
        HttpStatusCode statusCode,
        bool expected)
    {
        var exception = new LLMCommunicationException("provider failed", statusCode, null);

        Assert.Equal(expected, TransientErrorPolicy.IsTransient(exception));
    }

    [Fact]
    public void MessageSubstringAlone_IsNotTransient()
    {
        Assert.False(TransientErrorPolicy.IsTransient(
            new InvalidOperationException("rate limit maybe")));
    }
}
