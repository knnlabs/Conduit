using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using BenchmarkDotNet.Attributes;

using ConduitLLM.Core.Models;
using ConduitLLM.Core.Serialization;

namespace ConduitLLM.Benchmarks;

/// <summary>
/// Compares reflection metadata lookup with the generated metadata used by the Gateway
/// chat response path.
/// </summary>
/// <remarks>
/// Run with:
/// <c>dotnet run -c Release --project Tests/ConduitLLM.Benchmarks -- --filter *JsonSourceGeneration*</c>.
/// </remarks>
[MemoryDiagnoser]
public class JsonSourceGenerationBenchmarks
{
    private static readonly JsonSerializerOptions ReflectionOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private ChatCompletionResponse _response = null!;

    [GlobalSetup]
    public void Setup()
    {
        _response = new ChatCompletionResponse
        {
            Id = "chatcmpl-benchmark",
            Object = "chat.completion",
            Created = 1_722_083_696,
            Model = "gpt-benchmark",
            Choices =
            [
                new Choice
                {
                    Index = 0,
                    FinishReason = "stop",
                    Message = new Message
                    {
                        Role = "assistant",
                        Content = "A representative short completion."
                    }
                }
            ],
            Usage = new Usage
            {
                PromptTokens = 24,
                CompletionTokens = 6,
                TotalTokens = 30
            }
        };
    }

    [Benchmark(Baseline = true)]
    public string ReflectionMetadata() =>
        JsonSerializer.Serialize(_response, ReflectionOptions);

    [Benchmark]
    public string SourceGeneratedMetadata() =>
        JsonSerializer.Serialize(_response, CoreHttpJsonContext.Default.ChatCompletionResponse);
}
