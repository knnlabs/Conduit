using System.Net;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Providers.Configuration;
using ConduitLLM.Providers.Extensions;
using ConduitLLM.Providers.Http;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;
using Moq.Protected;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Providers.Http
{
    /// <summary>
    /// Regression tests for provider HttpClient registration. These exist because the named
    /// clients requested at runtime (BaseLLMClient: $"{ProviderName}LLMClient") silently received
    /// unconfigured HttpClients for years — IHttpClientFactory returns a default client for any
    /// unknown name, so a registration/request name mismatch disables all resilience policies
    /// without any error.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Providers")]
    public class ProviderHttpClientRegistrationTests : TestBase
    {
        public ProviderHttpClientRegistrationTests(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// The exact named-client strings provider clients request at runtime. Deliberately
        /// hardcoded literals (NOT derived from ProviderHttpClientNames): this test pins the
        /// contract between registration and the strings BaseLLMClient actually builds. If a
        /// prefix in ProviderHttpClientNames changes, this test must fail.
        /// </summary>
        public static TheoryData<string> RuntimeClientNames => new()
        {
            "OpenAILLMClient",
            "groqLLMClient",
            "minimaxLLMClient",
            "ReplicateLLMClient",
            "FireworksLLMClient",
            "CloudflareLLMClient",
            "OpenRouterLLMClient",
            "metaLLMClient",
            "cerebrasLLMClient",
            "sambanovaLLMClient",
            "DeepInfraLLMClient",
        };

        private static ServiceProvider BuildServiceProvider(
            IProviderErrorTrackingService? errorTracker = null,
            Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddLogging();
            if (errorTracker != null)
            {
                services.AddSingleton(errorTracker);
            }
            services.AddLLMProviderHttpClients();
            configure?.Invoke(services);
            return services.BuildServiceProvider();
        }

        [Theory]
        [MemberData(nameof(RuntimeClientNames))]
        public void AddLLMProviderHttpClients_RegistersPolicyHandlers_ForRuntimeClientName(string clientName)
        {
            using var provider = BuildServiceProvider();

            var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();
            var options = optionsMonitor.Get(clientName);

            Assert.True(
                options.HttpMessageHandlerBuilderActions.Count > 0,
                $"Named client '{clientName}' has no message handler configuration — " +
                "resilience policies are NOT attached to it.");
        }

        [Fact]
        public void AddLLMProviderHttpClients_RegistersEveryTypeInProviderHttpClientNames()
        {
            using var provider = BuildServiceProvider();
            var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();

            foreach (var providerType in ProviderHttpClientNames.RegisteredTypes)
            {
                foreach (var name in new[]
                {
                    ProviderHttpClientNames.Chat(providerType),
                    ProviderHttpClientNames.Auth(providerType),
                    ProviderHttpClientNames.Video(providerType),
                })
                {
                    Assert.True(
                        optionsMonitor.Get(name).HttpMessageHandlerBuilderActions.Count > 0,
                        $"'{name}' ({providerType}) is not registered with policies.");
                }
            }
        }

        [Theory]
        [InlineData(ProviderType.OpenAI)]
        [InlineData(ProviderType.Groq)]
        [InlineData(ProviderType.MiniMax)]
        [InlineData(ProviderType.Replicate)]
        [InlineData(ProviderType.Fireworks)]
        [InlineData(ProviderType.Cloudflare)]
        [InlineData(ProviderType.OpenRouter)]
        [InlineData(ProviderType.Meta)]
        [InlineData(ProviderType.Cerebras)]
        [InlineData(ProviderType.SambaNova)]
        [InlineData(ProviderType.DeepInfra)]
        [InlineData(ProviderType.OpenAICompatible)]
        public async Task ClientCreatedViaRegistry_RequestsTheRegisteredClientName(ProviderType providerType)
        {
            var requestedNames = new List<string>();
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{}"),
                });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock
                .Setup(x => x.CreateClient(Capture.In(requestedNames)))
                .Returns(() => new HttpClient(handlerMock.Object)
                {
                    BaseAddress = new Uri("http://localhost:59999/"),
                });

            var provider = new Provider
            {
                Id = 1,
                ProviderName = "UserFacingDisplayName", // must NOT influence the client name
                ProviderType = providerType,
                IsEnabled = true,
                BaseUrl = "http://localhost:59999",
            };
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-key",
                IsPrimary = true,
                IsEnabled = true,
            };

            var creator = ClientCreatorRegistry.GetCreator(providerType);
            Assert.NotNull(creator);

            var client = creator!(provider, keyCredential, "test-model", new ClientCreationContext
            {
                LoggerFactory = LoggerFactory.Create(_ => { }),
                HttpClientFactory = factoryMock.Object,
            });

            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message> { new Message { Role = MessageRole.User, Content = "hi" } },
            };

            try
            {
                await client.CreateChatCompletionAsync(request, cancellationToken: CancellationToken.None);
            }
            catch
            {
                // The canned 500 response makes the call fail — irrelevant; the factory has
                // already been asked for a named client by the time the request is sent.
            }

            var expected = ProviderHttpClientNames.Chat(providerType);
            Assert.True(
                requestedNames.Contains(expected),
                $"{providerType} client requested [{string.Join(", ", requestedNames)}] " +
                $"but registration configures '{expected}'.");
        }

        [Fact]
        public async Task RegisteredClient_RetriesTransientErrors_AndSucceeds()
        {
            var attempts = 0;
            using var provider = BuildServiceProvider(configure: services =>
            {
                services.AddHttpClient("OpenAILLMClient")
                    .ConfigurePrimaryHttpMessageHandler(() => new ScriptedHandler(() =>
                    {
                        attempts++;
                        return attempts < 3
                            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                            : new HttpResponseMessage(HttpStatusCode.OK)
                            {
                                Content = new StringContent("{}"),
                            };
                    }));
            });

            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("OpenAILLMClient");

            var response = await client.GetAsync("http://localhost:59999/v1/models");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(3, attempts);
        }

        [Fact]
        public async Task RegisteredClient_WithErrorTracking_AttributesRateLimitErrorsToCurrentKey()
        {
            var trackedErrors = new List<ProviderErrorInfo>();
            var trackerMock = new Mock<IProviderErrorTrackingService>();
            trackerMock
                .Setup(x => x.TrackErrorAsync(Capture.In(trackedErrors)))
                .Returns(Task.CompletedTask);

            using var provider = BuildServiceProvider(trackerMock.Object, services =>
            {
                services.AddHttpClient("groqLLMClient")
                    .ConfigurePrimaryHttpMessageHandler(() => new ScriptedHandler(() =>
                        new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
            });

            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("groqLLMClient");

            using (ProviderKeyContext.Set(keyId: 42, providerId: 7))
            {
                var response = await client.GetAsync("http://localhost:59999/v1/chat");
                Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            }

            Assert.NotEmpty(trackedErrors);
            Assert.All(trackedErrors, e =>
            {
                Assert.Equal(42, e.KeyCredentialId);
                Assert.Equal(7, e.ProviderId);
                Assert.Equal(ProviderErrorType.RateLimitExceeded, e.ErrorType);
            });
        }

        private sealed class ScriptedHandler : HttpMessageHandler
        {
            private readonly Func<HttpResponseMessage> _responseFactory;

            public ScriptedHandler(Func<HttpResponseMessage> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_responseFactory());
        }
    }
}
