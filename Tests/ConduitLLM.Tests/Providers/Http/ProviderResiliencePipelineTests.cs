using System.Net;

using ConduitLLM.Providers.Extensions;
using ConduitLLM.Providers.Http;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Polly.CircuitBreaker;
using Polly.Timeout;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Providers.Http
{
    /// <summary>
    /// Behavior tests for the provider resilience pipeline
    /// (total timeout → retry → circuit breaker → per-attempt timeout).
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Providers")]
    public class ProviderResiliencePipelineTests : TestBase
    {
        public ProviderResiliencePipelineTests(ITestOutputHelper output) : base(output)
        {
        }

        private static ServiceProvider BuildServiceProvider(
            Dictionary<string, string?>? config = null,
            Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(
                new ConfigurationBuilder().AddInMemoryCollection(config ?? new()).Build());
            services.AddLogging();
            services.AddLLMProviderHttpClients();
            configure?.Invoke(services);
            return services.BuildServiceProvider();
        }

        private static void UseHandler(IServiceCollection services, string clientName, HttpMessageHandler handler)
        {
            services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        }

        [Fact]
        public async Task Retry_HonorsShortRetryAfterHeader()
        {
            var attempts = 0;
            using var provider = BuildServiceProvider(configure: s =>
                UseHandler(s, "OpenAILLMClient", new ScriptedHandler(() =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                        resp.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                            TimeSpan.FromSeconds(1));
                        return resp;
                    }
                    return new HttpResponseMessage(HttpStatusCode.OK);
                })));

            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("OpenAILLMClient");
            var started = DateTime.UtcNow;
            var response = await client.GetAsync("http://localhost:59999/v1/chat");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, attempts);
            Assert.True(DateTime.UtcNow - started >= TimeSpan.FromSeconds(0.9),
                "Retry should have waited for the Retry-After delay");
        }

        [Fact]
        public async Task Retry_DeclinesWhenRetryAfterExceedsCap()
        {
            var attempts = 0;
            using var provider = BuildServiceProvider(configure: s =>
                UseHandler(s, "OpenAILLMClient", new ScriptedHandler(() =>
                {
                    attempts++;
                    var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    resp.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                        TimeSpan.FromSeconds(60));
                    return resp;
                })));

            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("OpenAILLMClient");
            var response = await client.GetAsync("http://localhost:59999/v1/chat");

            // Provider told us to come back in 60s (> 5s cap): fail fast, exactly one attempt.
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.Equal(1, attempts);
        }

        [Fact]
        public async Task AttemptTimeout_BoundsHungAttempts_AndIsRetried()
        {
            var attempts = 0;
            using var provider = BuildServiceProvider(
                config: new Dictionary<string, string?>
                {
                    ["Conduit:ProviderHttp:Budgets:Chat:AttemptTimeoutSeconds"] = "0.3",
                    ["Conduit:ProviderHttp:Budgets:Chat:TotalTimeoutSeconds"] = "5",
                    ["Conduit:ProviderHttp:Budgets:Chat:MaxRetryAttempts"] = "1",
                    ["Conduit:ProviderHttp:Retry:BaseDelaySeconds"] = "0.05",
                },
                configure: s =>
                    UseHandler(s, "OpenAILLMClient", new ScriptedHandler(async ct =>
                    {
                        attempts++;
                        await Task.Delay(TimeSpan.FromSeconds(10), ct);
                        return new HttpResponseMessage(HttpStatusCode.OK);
                    })));

            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("OpenAILLMClient");

            await Assert.ThrowsAsync<TimeoutRejectedException>(
                () => client.GetAsync("http://localhost:59999/v1/chat"));
            Assert.Equal(2, attempts); // initial + 1 retry, each cut off by the attempt timeout
        }

        [Fact]
        public async Task OperationClassOption_SelectsPerRequestBudget()
        {
            using var provider = BuildServiceProvider(
                config: new Dictionary<string, string?>
                {
                    // Chat budget too small for the handler's delay; images budget generous.
                    ["Conduit:ProviderHttp:Budgets:Chat:AttemptTimeoutSeconds"] = "0.2",
                    ["Conduit:ProviderHttp:Budgets:Chat:MaxRetryAttempts"] = "0",
                    ["Conduit:ProviderHttp:Budgets:Images:AttemptTimeoutSeconds"] = "10",
                },
                configure: s =>
                    UseHandler(s, "OpenAILLMClient", new ScriptedHandler(async ct =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), ct);
                        return new HttpResponseMessage(HttpStatusCode.OK);
                    })));

            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("OpenAILLMClient");

            // Default (chat) budget: attempt times out.
            await Assert.ThrowsAsync<TimeoutRejectedException>(
                () => client.GetAsync("http://localhost:59999/v1/images"));

            // Same client, images operation class: generous budget applies, request succeeds.
            var imageRequest = new HttpRequestMessage(HttpMethod.Get, "http://localhost:59999/v1/images")
                .WithOperationClass(ConduitHttpOptions.Images);
            var response = await client.SendAsync(imageRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CircuitBreaker_OpensPerAuthority_NotGlobally()
        {
            using var provider = BuildServiceProvider(
                config: new Dictionary<string, string?>
                {
                    ["Conduit:ProviderHttp:CircuitBreaker:MinimumThroughput"] = "2",
                    ["Conduit:ProviderHttp:CircuitBreaker:FailureRatio"] = "0.5",
                    ["Conduit:ProviderHttp:CircuitBreaker:SamplingDurationSeconds"] = "30",
                    ["Conduit:ProviderHttp:Budgets:Chat:MaxRetryAttempts"] = "0",
                },
                configure: s =>
                    UseHandler(s, "OpenAILLMClient", new ScriptedHandler(() =>
                        new HttpResponseMessage(HttpStatusCode.InternalServerError))));

            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("OpenAILLMClient");

            // Hammer host A until its circuit opens.
            BrokenCircuitException? broken = null;
            for (var i = 0; i < 20 && broken == null; i++)
            {
                try
                {
                    await client.GetAsync("http://host-a.localtest:59999/v1/chat");
                }
                catch (BrokenCircuitException ex)
                {
                    broken = ex;
                }
            }
            Assert.NotNull(broken);

            // Host B goes through a separate pipeline instance: still gets real responses.
            var response = await client.GetAsync("http://host-b.localtest:59999/v1/chat");
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task Pipeline_DoesNotCancelResponseStream_AfterHeaders()
        {
            using var provider = BuildServiceProvider(
                config: new Dictionary<string, string?>
                {
                    ["Conduit:ProviderHttp:Budgets:Chat:AttemptTimeoutSeconds"] = "0.5",
                    ["Conduit:ProviderHttp:Budgets:Chat:TotalTimeoutSeconds"] = "0.5",
                },
                configure: s =>
                    UseHandler(s, "OpenAILLMClient", new ScriptedHandler(() =>
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new SlowStreamingContent(chunks: 5, delayPerChunk: TimeSpan.FromSeconds(0.4)),
                        })));

            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("OpenAILLMClient");

            var response = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "http://localhost:59999/v1/chat"),
                HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Reading the body takes ~2s — far beyond both pipeline timeouts. The pipeline acts
            // on SendAsync only (completed at headers) and must not cancel the stream.
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(new string('x', 5 * 10), body);
        }

        private sealed class ScriptedHandler : HttpMessageHandler
        {
            private readonly Func<CancellationToken, Task<HttpResponseMessage>> _responseFactory;

            public ScriptedHandler(Func<HttpResponseMessage> responseFactory)
                : this(_ => Task.FromResult(responseFactory()))
            {
            }

            public ScriptedHandler(Func<CancellationToken, Task<HttpResponseMessage>> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => _responseFactory(cancellationToken);
        }

        private sealed class SlowStreamingContent : HttpContent
        {
            private readonly int _chunks;
            private readonly TimeSpan _delayPerChunk;

            public SlowStreamingContent(int chunks, TimeSpan delayPerChunk)
            {
                _chunks = chunks;
                _delayPerChunk = delayPerChunk;
            }

            protected override async Task SerializeToStreamAsync(
                Stream stream, System.Net.TransportContext? context)
            {
                var chunk = System.Text.Encoding.UTF8.GetBytes(new string('x', 10));
                for (var i = 0; i < _chunks; i++)
                {
                    await Task.Delay(_delayPerChunk);
                    await stream.WriteAsync(chunk);
                    await stream.FlushAsync();
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = -1;
                return false;
            }
        }
    }
}
