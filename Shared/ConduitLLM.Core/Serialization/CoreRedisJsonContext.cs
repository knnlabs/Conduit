using System.Text.Json.Serialization;

using ConduitLLM.Core.Services;

namespace ConduitLLM.Core.Serialization;

/// <summary>
/// Source-generated metadata for persisted Core Redis reliability payloads.
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RedisWebhookCircuitBreaker.CircuitState))]
internal partial class CoreRedisJsonContext : JsonSerializerContext;
