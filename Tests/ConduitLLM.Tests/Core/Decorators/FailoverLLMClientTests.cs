using System.Net;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Decorators;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Providers;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Decorators
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Core")]
    public class FailoverLLMClientTests : TestBase
    {
        private readonly FailoverOptions _options = new() { Enabled = true };
        private readonly FailoverAttributionAccessor _attribution = new();

        public FailoverLLMClientTests(ITestOutputHelper output) : base(output)
        {
        }

        private static LLMCommunicationException Error(HttpStatusCode status)
            => new($"error {(int)status}", status, null);

        private static ChatCompletionRequest Request() => new()
        {
            Model = "test-model",
            Messages = new List<Message> { new() { Role = MessageRole.User, Content = "hi" } },
        };

        private static ChatCompletionResponse Response(string id) => new()
        {
            Id = id,
            Object = "chat.completion",
            Created = 1,
            Model = "test-model",
            Choices = new List<Choice>(),
        };

        private FailoverCandidate Candidate(int keyId, ILLMClient client, int providerId = 1, string? baseUrl = "http://a", short accountGroup = 0)
            => new()
            {
                ProviderId = providerId,
                ProviderType = ProviderType.OpenAI,
                KeyCredentialId = keyId,
                ProviderAccountGroup = accountGroup,
                ProviderModelId = "test-model",
                BaseUrl = baseUrl,
                ClientFactory = () => client,
            };

        private static Mock<ILLMClient> ClientReturning(ChatCompletionResponse response)
        {
            var mock = new Mock<ILLMClient>();
            mock.Setup(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);
            return mock;
        }

        private static Mock<ILLMClient> ClientThrowing(Exception ex)
        {
            var mock = new Mock<ILLMClient>();
            mock.Setup(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(ex);
            mock.Setup(c => c.CreateImageAsync(It.IsAny<ImageGenerationRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(ex);
            return mock;
        }

        [Fact]
        public async Task InvalidKeyOnPrimary_FailsOverToSecondKey()
        {
            var failing = ClientThrowing(Error(HttpStatusCode.Unauthorized));
            var succeeding = ClientReturning(Response("from-key-2"));

            var sut = new FailoverLLMClient(
                new[] { Candidate(1, failing.Object), Candidate(2, succeeding.Object) },
                _options, _attribution, null);

            var response = await sut.CreateChatCompletionAsync(Request());

            Assert.Equal("from-key-2", response.Id);
            Assert.Equal(2, _attribution.AttemptCount);
            Assert.True(_attribution.FailoverOccurred);
            Assert.Equal(2, _attribution.Current!.KeyCredentialId);
            failing.Verify(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BadRequest_DoesNotFailover_RethrowsOriginal()
        {
            var original = Error(HttpStatusCode.BadRequest);
            var failing = ClientThrowing(original);
            var second = ClientReturning(Response("never"));

            var sut = new FailoverLLMClient(
                new[] { Candidate(1, failing.Object), Candidate(2, second.Object) },
                _options, _attribution, null);

            var thrown = await Assert.ThrowsAsync<LLMCommunicationException>(
                () => sut.CreateChatCompletionAsync(Request()));

            Assert.Same(original, thrown);
            Assert.Equal(1, _attribution.AttemptCount);
            second.Verify(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task AllCandidatesFail_LastExceptionRethrown()
        {
            var first = ClientThrowing(Error(HttpStatusCode.Unauthorized));
            var lastError = Error(HttpStatusCode.PaymentRequired);
            var second = ClientThrowing(lastError);

            var sut = new FailoverLLMClient(
                new[] { Candidate(1, first.Object), Candidate(2, second.Object) },
                _options, _attribution, null);

            var thrown = await Assert.ThrowsAsync<LLMCommunicationException>(
                () => sut.CreateChatCompletionAsync(Request()));

            Assert.Same(lastError, thrown);
            Assert.Equal(2, _attribution.AttemptCount);
        }

        [Fact]
        public async Task ServerError_SameEndpointSiblingKey_Aborts()
        {
            // 500 is provider-infrastructure-scoped: a sibling key on the SAME BaseUrl cannot
            // help, so key-level failover must abort rather than burn an attempt.
            var original = Error(HttpStatusCode.InternalServerError);
            var failing = ClientThrowing(original);
            var second = ClientReturning(Response("never"));

            var sut = new FailoverLLMClient(
                new[]
                {
                    Candidate(1, failing.Object, baseUrl: "http://same"),
                    Candidate(2, second.Object, baseUrl: "http://same"),
                },
                _options, _attribution, null);

            var thrown = await Assert.ThrowsAsync<LLMCommunicationException>(
                () => sut.CreateChatCompletionAsync(Request()));

            Assert.Same(original, thrown);
            second.Verify(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ServerError_DifferentEndpointSiblingKey_FailsOver()
        {
            var failing = ClientThrowing(Error(HttpStatusCode.InternalServerError));
            var second = ClientReturning(Response("other-endpoint"));

            var sut = new FailoverLLMClient(
                new[]
                {
                    Candidate(1, failing.Object, baseUrl: "http://a"),
                    Candidate(2, second.Object, baseUrl: "http://b"),
                },
                _options, _attribution, null);

            var response = await sut.CreateChatCompletionAsync(Request());

            Assert.Equal("other-endpoint", response.Id);
        }

        [Fact]
        public async Task ByokApiKey_BypassesFailover()
        {
            var failing = ClientThrowing(Error(HttpStatusCode.Unauthorized));
            var second = ClientReturning(Response("never"));

            var sut = new FailoverLLMClient(
                new[] { Candidate(1, failing.Object), Candidate(2, second.Object) },
                _options, _attribution, null);

            await Assert.ThrowsAsync<LLMCommunicationException>(
                () => sut.CreateChatCompletionAsync(Request(), apiKey: "caller-supplied"));

            Assert.Equal(1, _attribution.AttemptCount);
            second.Verify(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CallerCancellation_Aborts_WithoutFurtherAttempts()
        {
            var failing = new Mock<ILLMClient>();
            failing.Setup(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());
            var second = ClientReturning(Response("never"));

            var sut = new FailoverLLMClient(
                new[] { Candidate(1, failing.Object), Candidate(2, second.Object) },
                _options, _attribution, null);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sut.CreateChatCompletionAsync(Request()));

            second.Verify(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Images_ServerError_DoesNotFailover_DoubleGenerationRisk()
        {
            var original = Error(HttpStatusCode.InternalServerError);
            var failing = ClientThrowing(original);
            var second = new Mock<ILLMClient>();

            var sut = new FailoverLLMClient(
                new[]
                {
                    Candidate(1, failing.Object, baseUrl: "http://a"),
                    Candidate(2, second.Object, baseUrl: "http://b"),
                },
                _options, _attribution, null);

            var thrown = await Assert.ThrowsAsync<LLMCommunicationException>(
                () => sut.CreateImageAsync(new ImageGenerationRequest { Prompt = "p", Model = "m" }));

            Assert.Same(original, thrown);
            second.Verify(c => c.CreateImageAsync(It.IsAny<ImageGenerationRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Images_AuthError_DoesFailover()
        {
            var failing = ClientThrowing(Error(HttpStatusCode.Unauthorized));
            var second = new Mock<ILLMClient>();
            second.Setup(c => c.CreateImageAsync(It.IsAny<ImageGenerationRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ImageGenerationResponse { Created = 1, Data = new List<ImageData>() });

            var sut = new FailoverLLMClient(
                new[] { Candidate(1, failing.Object), Candidate(2, second.Object) },
                _options, _attribution, null);

            var response = await sut.CreateImageAsync(new ImageGenerationRequest { Prompt = "p", Model = "m" });

            Assert.NotNull(response);
            Assert.Equal(2, _attribution.Current!.KeyCredentialId);
        }

        [Fact]
        public async Task Streaming_ErrorBeforeFirstChunk_FailsOverToFreshStream()
        {
            var failing = new Mock<ILLMClient>();
            failing.Setup(c => c.StreamChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(ThrowingStream(Error(HttpStatusCode.ServiceUnavailable)));
            var second = new Mock<ILLMClient>();
            second.Setup(c => c.StreamChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(ChunkStream("a", "b", "c"));

            var sut = new FailoverLLMClient(
                new[]
                {
                    Candidate(1, failing.Object, providerId: 1, baseUrl: "http://a"),
                    Candidate(2, second.Object, providerId: 1, baseUrl: "http://b"),
                },
                _options, _attribution, null);

            var chunks = new List<string>();
            await foreach (var chunk in sut.StreamChatCompletionAsync(Request()))
            {
                chunks.Add(chunk.Id!);
            }

            Assert.Equal(new[] { "a", "b", "c" }, chunks);
            Assert.True(_attribution.FailoverOccurred);
        }

        [Fact]
        public async Task Streaming_ErrorAfterFirstChunk_Propagates_NoFailover()
        {
            var midStreamError = Error(HttpStatusCode.ServiceUnavailable);
            var failing = new Mock<ILLMClient>();
            failing.Setup(c => c.StreamChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(StreamThenThrow("first", midStreamError));
            var second = new Mock<ILLMClient>();

            var sut = new FailoverLLMClient(
                new[]
                {
                    Candidate(1, failing.Object, baseUrl: "http://a"),
                    Candidate(2, second.Object, baseUrl: "http://b"),
                },
                _options, _attribution, null);

            var chunks = new List<string>();
            var thrown = await Assert.ThrowsAsync<LLMCommunicationException>(async () =>
            {
                await foreach (var chunk in sut.StreamChatCompletionAsync(Request()))
                {
                    chunks.Add(chunk.Id!);
                }
            });

            Assert.Same(midStreamError, thrown);
            Assert.Equal(new[] { "first" }, chunks);
            second.Verify(c => c.StreamChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void OrderKeysForFailover_PrimaryFirst_ThenOtherAccountGroups()
        {
            var keys = new[]
            {
                new ProviderKeyCredential { Id = 1, IsEnabled = true, IsPrimary = false, ProviderAccountGroup = 0 },
                new ProviderKeyCredential { Id = 2, IsEnabled = true, IsPrimary = true, ProviderAccountGroup = 0 },
                new ProviderKeyCredential { Id = 3, IsEnabled = true, IsPrimary = false, ProviderAccountGroup = 1 },
                new ProviderKeyCredential { Id = 4, IsEnabled = false, IsPrimary = false, ProviderAccountGroup = 1 },
            };

            var ordered = DatabaseAwareLLMClientFactory.OrderKeysForFailover(keys, maxKeys: 3);

            // Primary (key 2) first; then key 3 (different account group than primary's group 0)
            // before key 1 (same group as primary); disabled key 4 excluded.
            Assert.Equal(new[] { 2, 3, 1 }, ordered.Select(k => k.Id));
        }

        [Fact]
        public void OrderKeysForFailover_RespectsMaxKeys()
        {
            var keys = Enumerable.Range(1, 5)
                .Select(i => new ProviderKeyCredential { Id = i, IsEnabled = true, IsPrimary = i == 1, ProviderAccountGroup = (short)i })
                .ToArray();

            var ordered = DatabaseAwareLLMClientFactory.OrderKeysForFailover(keys, maxKeys: 2);

            Assert.Equal(2, ordered.Count);
            Assert.Equal(1, ordered[0].Id);
        }

        [Fact]
        public async Task ProviderScopedError_SkipsRemainingSameProviderKeys_JumpsToNextProvider()
        {
            var failingPrimary = ClientThrowing(Error(HttpStatusCode.ServiceUnavailable));
            var siblingKeySameProvider = new Mock<ILLMClient>(); // must never be called (same endpoint)
            var fallbackProvider = ClientReturning(Response("from-provider-2"));

            var sut = new FailoverLLMClient(
                new[]
                {
                    Candidate(1, failingPrimary.Object, providerId: 1, baseUrl: "http://p1"),
                    Candidate(2, siblingKeySameProvider.Object, providerId: 1, baseUrl: "http://p1"),
                    Candidate(3, fallbackProvider.Object, providerId: 2, baseUrl: "http://p2"),
                },
                _options, _attribution, null);

            var response = await sut.CreateChatCompletionAsync(Request());

            Assert.Equal("from-provider-2", response.Id);
            Assert.Equal(2, _attribution.Current!.ProviderId);
            siblingKeySameProvider.Verify(
                c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CrossProviderFailover_AttributionCarriesFallbackMappingCost()
        {
            var failing = ClientThrowing(Error(HttpStatusCode.NotFound)); // model gone at primary
            var fallback = ClientReturning(Response("served"));

            var primary = Candidate(1, failing.Object, providerId: 1, baseUrl: "http://p1") with
            {
                MappingId = 10,
                ModelCostId = 100,
            };
            var secondary = Candidate(9, fallback.Object, providerId: 2, baseUrl: "http://p2") with
            {
                MappingId = 20,
                ModelCostId = 200,
            };

            var sut = new FailoverLLMClient(new[] { primary, secondary }, _options, _attribution, null);

            await sut.CreateChatCompletionAsync(Request());

            // Billing attribution must follow the SERVING candidate's mapping, not the primary's.
            Assert.Equal(20, _attribution.Current!.MappingId);
            Assert.Equal(200, _attribution.Current.ModelCostId);
            Assert.Equal(2, _attribution.Current.ProviderId);
        }

        [Fact]
        public void PromptCacheInjection_Idempotent_AcrossFailoverRedispatch()
        {
            // Failover re-dispatches the SAME request object through a second candidate chain,
            // which runs PromptCachingLLMClient's injection again. The mutation must be
            // idempotent or the second attempt would send a different payload.
            var request = Request();
            var config = new PromptCachingConfig
            {
                AutoInjectEnabled = true,
                InjectionPoints = new List<CacheInjectionPoint> { new() { Role = "user", Index = -1 } },
            };

            PromptCacheInjectionService.InjectCacheControl(request, config);
            var afterFirst = System.Text.Json.JsonSerializer.Serialize(request.Messages);

            PromptCacheInjectionService.InjectCacheControl(request, config);
            var afterSecond = System.Text.Json.JsonSerializer.Serialize(request.Messages);

            Assert.Equal(afterFirst, afterSecond);
        }

        private static async IAsyncEnumerable<ChatCompletionChunk> ThrowingStream(Exception ex)
        {
            await Task.Yield();
            throw ex;
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }

        private static async IAsyncEnumerable<ChatCompletionChunk> ChunkStream(params string[] ids)
        {
            foreach (var id in ids)
            {
                await Task.Yield();
                yield return new ChatCompletionChunk { Id = id, Model = "test-model" };
            }
        }

        private static async IAsyncEnumerable<ChatCompletionChunk> StreamThenThrow(string firstId, Exception ex)
        {
            await Task.Yield();
            yield return new ChatCompletionChunk { Id = firstId, Model = "test-model" };
            throw ex;
        }
    }
}
