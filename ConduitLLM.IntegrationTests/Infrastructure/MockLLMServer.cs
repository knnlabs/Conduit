using System.Net;
using System.Text.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ConduitLLM.IntegrationTests.Infrastructure;

/// <summary>
/// Mock LLM server for integration testing without hitting real APIs.
/// </summary>
public class MockLLMServer : IDisposable
{
    private readonly WireMockServer _server;
    private readonly Dictionary<string, int> _requestCounts = new();
    private readonly List<(DateTime Timestamp, string Path, string Body)> _requestHistory = new();

    public string BaseUrl => _server.Url;
    public int Port => _server.Port;

    public MockLLMServer(int? port = null)
    {
        _server = WireMockServer.Start(port);
        SetupDefaultMappings();
    }

    /// <summary>
    /// Sets up an OpenAI chat completion endpoint mock.
    /// </summary>
    public void SetupChatCompletion(string responseContent = "This is a test response", int statusCode = 200, int delayMs = 0)
    {
        _server
            .Given(Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithDelay(delayMs)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    id = $"chatcmpl-{Guid.NewGuid():N}",
                    @object = "chat.completion",
                    created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    model = "gpt-3.5-turbo",
                    usage = new
                    {
                        prompt_tokens = 10,
                        completion_tokens = 20,
                        total_tokens = 30
                    },
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                role = "assistant",
                                content = responseContent
                            },
                            finish_reason = "stop",
                            index = 0
                        }
                    }
                })));
    }

    /// <summary>
    /// Sets up an OpenAI streaming chat completion endpoint mock.
    /// </summary>
    public void SetupStreamingChatCompletion(string[] responseChunks, int delayBetweenChunksMs = 10)
    {
        _server
            .Given(Request.Create()
                .WithPath("/v1/chat/completions")
                .WithParam("stream", "true")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/event-stream")
                .WithBody(req =>
                {
                    var streamData = new List<string>();
                    
                    foreach (var chunk in responseChunks)
                    {
                        var data = JsonSerializer.Serialize(new
                        {
                            id = $"chatcmpl-{Guid.NewGuid():N}",
                            @object = "chat.completion.chunk",
                            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            model = "gpt-3.5-turbo",
                            choices = new[]
                            {
                                new
                                {
                                    delta = new
                                    {
                                        content = chunk
                                    },
                                    index = 0
                                }
                            }
                        });
                        streamData.Add($"data: {data}\n\n");
                    }
                    
                    streamData.Add("data: [DONE]\n\n");
                    return string.Join("", streamData);
                }));
    }

    /// <summary>
    /// Sets up an OpenAI embeddings endpoint mock.
    /// </summary>
    public void SetupEmbeddings(float[] embeddingVector = null, int statusCode = 200)
    {
        embeddingVector ??= Enumerable.Range(0, 1536).Select(i => (float)i / 1536).ToArray();

        _server
            .Given(Request.Create()
                .WithPath("/v1/embeddings")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    @object = "list",
                    data = new[]
                    {
                        new
                        {
                            @object = "embedding",
                            embedding = embeddingVector,
                            index = 0
                        }
                    },
                    model = "text-embedding-ada-002",
                    usage = new
                    {
                        prompt_tokens = 8,
                        total_tokens = 8
                    }
                })));
    }

    /// <summary>
    /// Sets up an OpenAI image generation endpoint mock.
    /// </summary>
    public void SetupImageGeneration(string imageUrl = null, int statusCode = 200)
    {
        imageUrl ??= $"{BaseUrl}/generated-image.png";

        _server
            .Given(Request.Create()
                .WithPath("/v1/images/generations")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    data = new[]
                    {
                        new
                        {
                            url = imageUrl,
                            revised_prompt = "A test image prompt"
                        }
                    }
                })));

        // Also mock the image URL
        _server
            .Given(Request.Create()
                .WithPath("/generated-image.png")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "image/png")
                .WithBody(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="))); // 1x1 red pixel
    }

    /// <summary>
    /// Sets up an error response for a specific endpoint.
    /// </summary>
    public void SetupError(string path, int statusCode, string errorMessage, string errorCode = "internal_error")
    {
        _server
            .Given(Request.Create()
                .WithPath(path)
                .UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    error = new
                    {
                        message = errorMessage,
                        type = "error",
                        code = errorCode
                    }
                })));
    }

    /// <summary>
    /// Sets up a rate limit response.
    /// </summary>
    public void SetupRateLimit(string path)
    {
        _server
            .Given(Request.Create()
                .WithPath(path)
                .UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Retry-After", "60")
                .WithBody(JsonSerializer.Serialize(new
                {
                    error = new
                    {
                        message = "Rate limit exceeded",
                        type = "rate_limit_error",
                        code = "rate_limit_exceeded"
                    }
                })));
    }

    /// <summary>
    /// Gets the number of requests made to a specific path.
    /// </summary>
    public int GetRequestCount(string path)
    {
        return _server.LogEntries
            .Count(entry => entry.RequestMessage.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all requests made to the server.
    /// </summary>
    public IReadOnlyList<(DateTime Timestamp, string Path, string Body)> GetRequestHistory()
    {
        return _server.LogEntries
            .Select(entry => (
                entry.RequestMessage.DateTime,
                entry.RequestMessage.Path,
                entry.RequestMessage.Body ?? string.Empty
            ))
            .ToList();
    }

    /// <summary>
    /// Verifies that a request was made with specific content.
    /// </summary>
    public bool VerifyRequest(string path, Func<string, bool> bodyPredicate)
    {
        return _server.LogEntries
            .Any(entry => 
                entry.RequestMessage.Path.Equals(path, StringComparison.OrdinalIgnoreCase) &&
                bodyPredicate(entry.RequestMessage.Body ?? string.Empty));
    }

    /// <summary>
    /// Resets all request history and mappings.
    /// </summary>
    public void Reset()
    {
        _server.Reset();
        SetupDefaultMappings();
        _requestCounts.Clear();
        _requestHistory.Clear();
    }

    public void Dispose()
    {
        _server?.Stop();
        _server?.Dispose();
    }

    private void SetupDefaultMappings()
    {
        // Setup a catch-all for unmapped requests
        _server
            .Given(Request.Create()
                .UsingAnyMethod())
            .AtPriority(int.MaxValue)
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("Mock endpoint not configured"));
    }
}