using System.Net;
using System.Text;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Groq;

using Microsoft.Extensions.Logging.Abstractions;

namespace ConduitLLM.Tests.Providers;

[Trait("Category", "Unit")]
[Trait("Component", "Providers")]
public sealed class GroqStreamingAccountingTests
{
    [Fact]
    public async Task StreamChatCompletionAsync_PreservesHostedToolUsageForAccounting()
    {
        const string responseBody = """
            data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"llama","choices":[],"x_groq":{"usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15,"code_interpreter":2,"code_interpreter_duration_seconds":1.25,"browser_search":1}}}

            data: [DONE]

            """;
        var factory = new StubHttpClientFactory(responseBody);
        var provider = new Provider
        {
            Id = 1,
            ProviderType = ProviderType.Groq,
            ProviderName = "Groq"
        };
        var credential = new ProviderKeyCredential
        {
            Id = 1,
            ProviderId = 1,
            ApiKey = "test-key"
        };
        var client = new GroqClient(
            provider,
            credential,
            "llama",
            NullLogger<GroqClient>.Instance,
            factory);
        var request = new ChatCompletionRequest
        {
            Model = "alias",
            Messages = [new Message { Role = "user", Content = "hello" }]
        };

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in client.StreamChatCompletionAsync(request))
        {
            chunks.Add(chunk);
        }

        var usage = Assert.Single(chunks).ProviderToolUsage;
        Assert.NotNull(usage);
        Assert.Collection(
            usage.Tools,
            tool =>
            {
                Assert.Equal("code_interpreter", tool.ToolName);
                Assert.Equal(2, tool.Count);
                Assert.Equal(1.25m, tool.DurationSeconds);
            },
            tool =>
            {
                Assert.Equal("browser_search", tool.ToolName);
                Assert.Equal(1, tool.Count);
                Assert.Null(tool.DurationSeconds);
            });
    }

    private sealed class StubHttpClientFactory(string responseBody) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(responseBody))
        {
            BaseAddress = new Uri("https://api.groq.com/openai/v1/")
        };
    }

    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "text/event-stream")
            };
            return Task.FromResult(response);
        }
    }
}
