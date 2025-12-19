using System.Text.Json;

namespace ConduitLLM.Tests.Http.Middleware.Builders
{
    /// <summary>
    /// Factory for creating common LLM provider response objects.
    /// </summary>
    public static class ResponseBuilders
    {
        /// <summary>
        /// Creates an OpenAI chat completion response builder.
        /// </summary>
        public static OpenAIResponseBuilder OpenAI() => new();

        /// <summary>
        /// Creates an Anthropic message response builder.
        /// </summary>
        public static AnthropicResponseBuilder Anthropic() => new();

        /// <summary>
        /// Creates a Groq response builder with tool usage support.
        /// </summary>
        public static GroqResponseBuilder Groq() => new();

        /// <summary>
        /// Creates an image generation response builder.
        /// </summary>
        public static ImageResponseBuilder Image() => new();

        /// <summary>
        /// Creates a video generation response builder.
        /// </summary>
        public static VideoResponseBuilder Video() => new();

        /// <summary>
        /// Creates an embeddings response builder.
        /// </summary>
        public static EmbeddingsResponseBuilder Embeddings() => new();
    }

    /// <summary>
    /// Builder for OpenAI-format chat completion responses.
    /// </summary>
    public class OpenAIResponseBuilder
    {
        private string _id = "chatcmpl-123";
        private string _model = "gpt-4";
        private int _promptTokens = 10;
        private int _completionTokens = 20;
        private string _content = "Hello! How can I help you?";
        private string _finishReason = "stop";
        private readonly List<object> _toolCalls = new();

        /// <summary>
        /// Sets the response ID.
        /// </summary>
        public OpenAIResponseBuilder WithId(string id) { _id = id; return this; }

        /// <summary>
        /// Sets the model name.
        /// </summary>
        public OpenAIResponseBuilder WithModel(string model) { _model = model; return this; }

        /// <summary>
        /// Sets the token usage.
        /// </summary>
        /// <param name="promptTokens">Number of prompt tokens.</param>
        /// <param name="completionTokens">Number of completion tokens.</param>
        public OpenAIResponseBuilder WithUsage(int promptTokens, int completionTokens)
        {
            _promptTokens = promptTokens;
            _completionTokens = completionTokens;
            return this;
        }

        /// <summary>
        /// Sets the response content.
        /// </summary>
        public OpenAIResponseBuilder WithContent(string content) { _content = content; return this; }

        /// <summary>
        /// Sets the finish reason.
        /// </summary>
        public OpenAIResponseBuilder WithFinishReason(string reason) { _finishReason = reason; return this; }

        /// <summary>
        /// Adds a tool call to the response.
        /// </summary>
        /// <param name="id">Tool call ID.</param>
        /// <param name="name">Function name.</param>
        /// <param name="arguments">Function arguments as JSON string.</param>
        public OpenAIResponseBuilder WithToolCall(string id, string name, string arguments)
        {
            _toolCalls.Add(new { id, type = "function", function = new { name, arguments } });
            return this;
        }

        /// <summary>
        /// Builds the response as an object.
        /// </summary>
        public object Build()
        {
            var message = new Dictionary<string, object>
            {
                ["role"] = "assistant",
                ["content"] = _content
            };

            if (_toolCalls.Count > 0)
                message["tool_calls"] = _toolCalls;

            return new
            {
                id = _id,
                @object = "chat.completion",
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                model = _model,
                choices = new[] { new { index = 0, message, finish_reason = _finishReason } },
                usage = new
                {
                    prompt_tokens = _promptTokens,
                    completion_tokens = _completionTokens,
                    total_tokens = _promptTokens + _completionTokens
                }
            };
        }

        /// <summary>
        /// Builds the response as a JSON string.
        /// </summary>
        public string BuildJson() => JsonSerializer.Serialize(Build());
    }

    /// <summary>
    /// Builder for Anthropic message responses with caching support.
    /// </summary>
    public class AnthropicResponseBuilder
    {
        private string _id = "msg_01XhEY9K2nPNTxWZj5vZ2VBm";
        private string _model = "claude-3-5-sonnet-20241022";
        private int _inputTokens = 100;
        private int _outputTokens = 50;
        private int _cacheCreationTokens = 0;
        private int _cacheReadTokens = 0;
        private string _content = "Hello! I'm Claude.";
        private string _stopReason = "end_turn";

        /// <summary>
        /// Sets the response ID.
        /// </summary>
        public AnthropicResponseBuilder WithId(string id) { _id = id; return this; }

        /// <summary>
        /// Sets the model name.
        /// </summary>
        public AnthropicResponseBuilder WithModel(string model) { _model = model; return this; }

        /// <summary>
        /// Sets the token usage.
        /// </summary>
        /// <param name="inputTokens">Number of input tokens.</param>
        /// <param name="outputTokens">Number of output tokens.</param>
        public AnthropicResponseBuilder WithUsage(int inputTokens, int outputTokens)
        {
            _inputTokens = inputTokens;
            _outputTokens = outputTokens;
            return this;
        }

        /// <summary>
        /// Sets the cached tokens.
        /// </summary>
        /// <param name="cacheCreation">Tokens used for cache creation.</param>
        /// <param name="cacheRead">Tokens read from cache.</param>
        public AnthropicResponseBuilder WithCachedTokens(int cacheCreation, int cacheRead)
        {
            _cacheCreationTokens = cacheCreation;
            _cacheReadTokens = cacheRead;
            return this;
        }

        /// <summary>
        /// Sets the response content.
        /// </summary>
        public AnthropicResponseBuilder WithContent(string content) { _content = content; return this; }

        /// <summary>
        /// Sets the stop reason.
        /// </summary>
        public AnthropicResponseBuilder WithStopReason(string reason) { _stopReason = reason; return this; }

        /// <summary>
        /// Builds the response as an object.
        /// </summary>
        public object Build() => new
        {
            id = _id,
            type = "message",
            role = "assistant",
            model = _model,
            content = new[] { new { type = "text", text = _content } },
            stop_reason = _stopReason,
            usage = new
            {
                input_tokens = _inputTokens,
                output_tokens = _outputTokens,
                cache_creation_input_tokens = _cacheCreationTokens,
                cache_read_input_tokens = _cacheReadTokens
            }
        };

        /// <summary>
        /// Builds the response as a JSON string.
        /// </summary>
        public string BuildJson() => JsonSerializer.Serialize(Build());
    }

    /// <summary>
    /// Builder for Groq responses with tool usage tracking (x_groq extension).
    /// </summary>
    public class GroqResponseBuilder
    {
        private string _id = "chatcmpl-groq-123";
        private string _model = "llama-3.1-70b-versatile";
        private int _promptTokens = 100;
        private int _completionTokens = 50;
        private string _content = "Here's the result...";
        private readonly Dictionary<string, int> _toolUsage = new();
        private readonly List<object> _toolCalls = new();

        /// <summary>
        /// Sets the response ID.
        /// </summary>
        public GroqResponseBuilder WithId(string id) { _id = id; return this; }

        /// <summary>
        /// Sets the model name.
        /// </summary>
        public GroqResponseBuilder WithModel(string model) { _model = model; return this; }

        /// <summary>
        /// Sets the token usage.
        /// </summary>
        /// <param name="promptTokens">Number of prompt tokens.</param>
        /// <param name="completionTokens">Number of completion tokens.</param>
        public GroqResponseBuilder WithUsage(int promptTokens, int completionTokens)
        {
            _promptTokens = promptTokens;
            _completionTokens = completionTokens;
            return this;
        }

        /// <summary>
        /// Sets the response content.
        /// </summary>
        public GroqResponseBuilder WithContent(string content) { _content = content; return this; }

        /// <summary>
        /// Adds tool usage to the x_groq extension.
        /// </summary>
        /// <param name="toolName">The tool name.</param>
        /// <param name="count">Number of times the tool was used.</param>
        public GroqResponseBuilder WithToolUsage(string toolName, int count)
        {
            _toolUsage[toolName] = count;
            return this;
        }

        /// <summary>
        /// Adds a tool call to the response message.
        /// </summary>
        public GroqResponseBuilder WithToolCall(string id, string name, string arguments)
        {
            _toolCalls.Add(new { id, type = "function", function = new { name, arguments } });
            return this;
        }

        /// <summary>
        /// Builds the response as an object.
        /// </summary>
        public object Build()
        {
            var message = new Dictionary<string, object>
            {
                ["role"] = "assistant",
                ["content"] = _content
            };

            if (_toolCalls.Count > 0)
                message["tool_calls"] = _toolCalls;

            var response = new Dictionary<string, object>
            {
                ["id"] = _id,
                ["model"] = _model,
                ["choices"] = new[] { new { index = 0, message, finish_reason = "stop" } },
                ["usage"] = new
                {
                    prompt_tokens = _promptTokens,
                    completion_tokens = _completionTokens,
                    total_tokens = _promptTokens + _completionTokens
                }
            };

            if (_toolUsage.Count > 0)
            {
                response["x_groq"] = new { usage = _toolUsage };
            }

            return response;
        }

        /// <summary>
        /// Builds the response as a JSON string.
        /// </summary>
        public string BuildJson() => JsonSerializer.Serialize(Build());
    }

    /// <summary>
    /// Builder for image generation responses.
    /// </summary>
    public class ImageResponseBuilder
    {
        private string? _model;
        private int _imageCount = 1;
        private bool _includeUsage = false;
        private readonly List<string> _urls = new();
        private string? _revisedPrompt = "A generated image";

        /// <summary>
        /// Sets the model name in the response.
        /// </summary>
        public ImageResponseBuilder WithModel(string model) { _model = model; return this; }

        /// <summary>
        /// Sets the number of images generated.
        /// </summary>
        public ImageResponseBuilder WithImages(int count)
        {
            _imageCount = count;
            return this;
        }

        /// <summary>
        /// Includes usage data in the response.
        /// </summary>
        public ImageResponseBuilder WithUsage() { _includeUsage = true; return this; }

        /// <summary>
        /// Sets the revised prompt text.
        /// </summary>
        public ImageResponseBuilder WithRevisedPrompt(string prompt) { _revisedPrompt = prompt; return this; }

        /// <summary>
        /// Adds specific image URLs to the response.
        /// </summary>
        public ImageResponseBuilder WithUrl(string url)
        {
            _urls.Add(url);
            return this;
        }

        /// <summary>
        /// Builds the response as an object.
        /// </summary>
        public object Build()
        {
            var data = _urls.Count > 0
                ? _urls.Select(url => new { url, revised_prompt = _revisedPrompt }).ToArray()
                : Enumerable.Range(0, _imageCount)
                    .Select(i => new { url = $"https://example.com/image{i}.png", revised_prompt = _revisedPrompt })
                    .ToArray();

            var response = new Dictionary<string, object>
            {
                ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["data"] = data
            };

            if (_model != null) response["model"] = _model;
            if (_includeUsage) response["usage"] = new { images = _imageCount };

            return response;
        }

        /// <summary>
        /// Builds the response as a JSON string.
        /// </summary>
        public string BuildJson() => JsonSerializer.Serialize(Build());
    }

    /// <summary>
    /// Builder for video generation responses.
    /// </summary>
    public class VideoResponseBuilder
    {
        private string? _model;
        private int _videoCount = 1;
        private int _durationSeconds = 5;
        private string _resolution = "1080p";
        private int _fps = 24;
        private readonly List<string> _urls = new();

        /// <summary>
        /// Sets the model name.
        /// </summary>
        public VideoResponseBuilder WithModel(string model) { _model = model; return this; }

        /// <summary>
        /// Sets the number of videos generated.
        /// </summary>
        public VideoResponseBuilder WithVideos(int count) { _videoCount = count; return this; }

        /// <summary>
        /// Sets the video duration in seconds.
        /// </summary>
        public VideoResponseBuilder WithDuration(int seconds) { _durationSeconds = seconds; return this; }

        /// <summary>
        /// Sets the video resolution.
        /// </summary>
        public VideoResponseBuilder WithResolution(string resolution) { _resolution = resolution; return this; }

        /// <summary>
        /// Sets the frames per second.
        /// </summary>
        public VideoResponseBuilder WithFps(int fps) { _fps = fps; return this; }

        /// <summary>
        /// Adds a specific video URL.
        /// </summary>
        public VideoResponseBuilder WithUrl(string url)
        {
            _urls.Add(url);
            return this;
        }

        /// <summary>
        /// Builds the response as an object.
        /// </summary>
        public object Build()
        {
            var data = _urls.Count > 0
                ? _urls.Select(url => new { url }).ToArray()
                : Enumerable.Range(0, _videoCount)
                    .Select(i => new { url = $"https://example.com/video{i}.mp4" })
                    .ToArray();

            var response = new Dictionary<string, object>
            {
                ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["data"] = data,
                ["usage"] = new
                {
                    duration_seconds = _durationSeconds,
                    resolution = _resolution,
                    fps = _fps
                }
            };

            if (_model != null) response["model"] = _model;

            return response;
        }

        /// <summary>
        /// Builds the response as a JSON string.
        /// </summary>
        public string BuildJson() => JsonSerializer.Serialize(Build());
    }

    /// <summary>
    /// Builder for embeddings responses.
    /// </summary>
    public class EmbeddingsResponseBuilder
    {
        private string _model = "text-embedding-ada-002";
        private int _promptTokens = 10;
        private int _totalTokens = 10;
        private int _embeddingCount = 1;
        private int _dimensions = 1536;

        /// <summary>
        /// Sets the model name.
        /// </summary>
        public EmbeddingsResponseBuilder WithModel(string model) { _model = model; return this; }

        /// <summary>
        /// Sets the token usage.
        /// </summary>
        public EmbeddingsResponseBuilder WithUsage(int promptTokens)
        {
            _promptTokens = promptTokens;
            _totalTokens = promptTokens;
            return this;
        }

        /// <summary>
        /// Sets the number of embeddings returned.
        /// </summary>
        public EmbeddingsResponseBuilder WithEmbeddingCount(int count) { _embeddingCount = count; return this; }

        /// <summary>
        /// Sets the embedding dimensions.
        /// </summary>
        public EmbeddingsResponseBuilder WithDimensions(int dims) { _dimensions = dims; return this; }

        /// <summary>
        /// Builds the response as an object.
        /// </summary>
        public object Build()
        {
            var data = Enumerable.Range(0, _embeddingCount)
                .Select(i => new
                {
                    @object = "embedding",
                    embedding = Enumerable.Range(0, _dimensions).Select(_ => 0.0).ToArray(),
                    index = i
                })
                .ToArray();

            return new
            {
                @object = "list",
                data,
                model = _model,
                usage = new
                {
                    prompt_tokens = _promptTokens,
                    total_tokens = _totalTokens
                }
            };
        }

        /// <summary>
        /// Builds the response as a JSON string.
        /// </summary>
        public string BuildJson() => JsonSerializer.Serialize(Build());
    }
}
