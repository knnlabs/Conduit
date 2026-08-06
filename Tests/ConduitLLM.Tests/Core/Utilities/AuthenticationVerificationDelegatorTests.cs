using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Utilities;

using Moq;

namespace ConduitLLM.Tests.Core.Utilities;

public sealed class AuthenticationVerificationDelegatorTests
{
    [Fact]
    public async Task VerifyAsync_UnsupportedClient_ReturnsFailure()
    {
        var client = Mock.Of<ILLMClient>();

        var result = await AuthenticationVerificationDelegator.VerifyAsync(
            client,
            "TestClient",
            apiKey: null,
            baseUrl: null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("does not support", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetHealthCheckUrl_UnsupportedClient_DoesNotInventProviderUrl()
    {
        var client = Mock.Of<ILLMClient>();

        Assert.Throws<NotSupportedException>(() =>
            AuthenticationVerificationDelegator.GetHealthCheckUrl(client, baseUrl: null));
    }
}
