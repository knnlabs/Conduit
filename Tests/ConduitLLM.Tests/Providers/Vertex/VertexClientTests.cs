using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Vertex;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Providers.Vertex;

[Trait("Category", "Unit")]
[Trait("Component", "Providers")]
public class VertexClientTests : IDisposable
{
    public VertexClientTests()
    {
        GoogleServiceAccountTokenProvider.ResetCacheForTests();
    }

    public void Dispose()
    {
        GoogleServiceAccountTokenProvider.ResetCacheForTests();
    }

    [Fact]
    public async Task Chat_Should_Exchange_The_Service_Account_And_Map_A_Gemini_Model()
    {
        string? requestBody = null;
        AuthenticationHeaderValue? authorization = null;
        var handler = new CallbackHandler(async request =>
        {
            if (request.RequestUri!.AbsoluteUri == GoogleServiceAccountTokenProvider.TokenEndpoint)
            {
                return TokenResponse("vertex-access-token");
            }

            request.RequestUri.AbsoluteUri.Should().EndWith(
                "/v1beta1/projects/test-project/locations/us-central1"
                + "/endpoints/openapi/chat/completions");
            requestBody = await request.Content!.ReadAsStringAsync();
            authorization = request.Headers.Authorization;
            return JsonResponse(
                """
                {
                  "id": "chatcmpl-vertex",
                  "object": "chat.completion",
                  "created": 1,
                  "model": "google/gemini-2.5-flash",
                  "choices": [{
                    "index": 0,
                    "message": { "role": "assistant", "content": "hello" },
                    "finish_reason": "stop"
                  }],
                  "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 }
                }
                """);
        });
        var client = CreateClient(handler, "gemini-2.5-flash");

        var response = await client.CreateChatCompletionAsync(new ChatCompletionRequest
        {
            Model = "alias",
            Messages = new List<Message>
            {
                new() { Role = MessageRole.User, Content = "hello" }
            }
        });

        response.Choices.Should().ContainSingle();
        authorization.Should().NotBeNull();
        authorization!.Scheme.Should().Be("Bearer");
        authorization.Parameter.Should().Be("vertex-access-token");
        using var body = JsonDocument.Parse(requestBody!);
        body.RootElement.GetProperty("model").GetString()
            .Should().Be("google/gemini-2.5-flash");
    }

    [Fact]
    public async Task Streaming_Chat_Should_Use_The_OAuth_Token_And_Forward_Sse_Chunks()
    {
        AuthenticationHeaderValue? authorization = null;
        var handler = new CallbackHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri == GoogleServiceAccountTokenProvider.TokenEndpoint)
            {
                return Task.FromResult(TokenResponse("stream-token"));
            }

            authorization = request.Headers.Authorization;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    data: {"id":"chatcmpl-vertex","object":"chat.completion.chunk","created":1,"model":"google/gemini-2.5-flash","choices":[{"index":0,"delta":{"role":"assistant","content":"hi"},"finish_reason":null}]}

                    data: [DONE]

                    """,
                    Encoding.UTF8,
                    "text/event-stream")
            };
            return Task.FromResult(response);
        });
        var client = CreateClient(handler, "google/gemini-2.5-flash");

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in client.StreamChatCompletionAsync(new ChatCompletionRequest
        {
            Model = "alias",
            Messages = new List<Message>
            {
                new() { Role = MessageRole.User, Content = "hello" }
            }
        }))
        {
            chunks.Add(chunk);
        }

        authorization!.Parameter.Should().Be("stream-token");
        chunks.Should().ContainSingle();
        chunks[0].Choices[0].Delta.Content.Should().Be("hi");
    }

    [Fact]
    public async Task GetModels_Should_Validate_Project_And_Map_Publisher_Models()
    {
        var requestedPaths = new List<string>();
        var handler = new CallbackHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri == GoogleServiceAccountTokenProvider.TokenEndpoint)
            {
                return Task.FromResult(TokenResponse("models-token"));
            }

            request.Headers.Authorization!.Parameter.Should().Be("models-token");
            requestedPaths.Add(request.RequestUri.AbsolutePath);
            if (request.RequestUri.AbsolutePath.StartsWith("/v1/projects/", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse("""{"models":[]}"""));
            }

            return Task.FromResult(JsonResponse(
                """
                {
                  "publisherModels": [{
                    "name": "publishers/google/models/gemini-2.5-flash",
                    "displayName": "Gemini 2.5 Flash"
                  }]
                }
                """));
        });
        var client = CreateClient(handler, "gemini-2.5-flash");

        var models = await client.GetModelsAsync();

        requestedPaths.Should().Contain(
            "/v1/projects/test-project/locations/us-central1/models");
        requestedPaths.Should().Contain("/v1beta1/publishers/google/models");
        models.Should().ContainSingle();
        models[0].Id.Should().Be("google/gemini-2.5-flash");
    }

    [Fact]
    public async Task GetModels_Should_Surface_An_Actionable_Project_Or_Location_Error()
    {
        var handler = new CallbackHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri == GoogleServiceAccountTokenProvider.TokenEndpoint)
            {
                return Task.FromResult(TokenResponse("models-token"));
            }

            return Task.FromResult(JsonResponse(
                """
                {
                  "error": {
                    "code": 404,
                    "message": "Project test-project or location us-central1 was not found.",
                    "status": "NOT_FOUND"
                  }
                }
                """,
                HttpStatusCode.NotFound));
        });
        var client = CreateClient(handler, "gemini-2.5-flash");

        var action = () => client.GetModelsAsync();

        var exception = await action.Should().ThrowAsync<LLMCommunicationException>();
        exception.WithMessage("*Project test-project or location us-central1 was not found*");
        exception.WithMessage("*Verify project_id, location*");
    }

    private static VertexClient CreateClient(
        HttpMessageHandler handler,
        string modelId)
    {
        using var rsa = RSA.Create(2048);
        var serviceAccountJson =
            GoogleServiceAccountTokenProviderTests.CreateServiceAccountJson(rsa);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler, disposeHandler: false));

        var provider = new Provider
        {
            Id = 1,
            ProviderType = ProviderType.Vertex,
            ProviderName = "vertex-test",
            Settings = new Dictionary<string, string>
            {
                ["project_id"] = "test-project",
                ["location"] = "us-central1"
            }
        };
        var keyCredential = new ProviderKeyCredential
        {
            Id = 77,
            ProviderId = 1,
            ApiKey = string.Empty,
            SecretSettings = new Dictionary<string, string>
            {
                ["service_account_json"] = serviceAccountJson
            },
            IsPrimary = true,
            IsEnabled = true
        };

        return new VertexClient(
            provider,
            keyCredential,
            modelId,
            NullLogger<VertexClient>.Instance,
            factory.Object);
    }

    private static HttpResponseMessage TokenResponse(string accessToken) =>
        JsonResponse(
            $$"""{"access_token":"{{accessToken}}","expires_in":3600,"token_type":"Bearer"}""");

    private static HttpResponseMessage JsonResponse(
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            callback(request);
    }
}
