using System.Net;

using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Exceptions;
using ConduitLLM.Functions.Providers.Tavily;

using Microsoft.Extensions.Logging.Abstractions;

namespace ConduitLLM.Tests.Functions.Providers;

public sealed class FunctionProviderClientTests
{
    [Fact]
    public async Task TavilyAuthentication_UsesProviderSpecificStatusHook()
    {
        var client = CreateTavilyClient((_, _) => Task.FromResult(
            new HttpResponseMessage((HttpStatusCode)432)
            {
                Content = new StringContent("""{"detail":"limit"}""")
            }));

        var result = await client.VerifyAuthenticationAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("Plan usage limit exceeded", result.Message);
    }

    [Fact]
    public async Task TavilyExecution_ThrowsMappedCommunicationException()
    {
        var client = CreateTavilyClient((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("""{"detail":"maintenance"}""")
            }));

        var exception = await Assert.ThrowsAsync<FunctionCommunicationException>(() =>
            client.ExecuteAsync(new Dictionary<string, object> { ["query"] = "test" }));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal("Tavily", exception.ProviderName);
    }

    [Fact]
    public async Task Authentication_PropagatesCallerCancellation()
    {
        var client = CreateTavilyClient((_, token) =>
            Task.FromCanceled<HttpResponseMessage>(token));
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.VerifyAuthenticationAsync(cancellationToken: source.Token));
    }

    private static TavilyClient CreateTavilyClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) =>
        new(
            new FunctionConfiguration
            {
                ConfigurationName = "Tavily",
                ProviderType = FunctionProviderType.Tavily,
                Purpose = FunctionPurpose.Search
            },
            new FunctionCredential
            {
                ProviderType = FunctionProviderType.Tavily,
                ApiKey = "tvly-test"
            },
            new StubHttpClientFactory(response),
            NullLogger<TavilyClient>.Instance);

    private sealed class StubHttpClientFactory(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new StubHttpMessageHandler(response));
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            response(request, cancellationToken);
    }
}
