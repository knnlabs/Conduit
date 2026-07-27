using System.Text.Json.Serialization;

using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Configuration.Serialization;

/// <summary>
/// Source-generated metadata for shared HTTP response contracts.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DiscoveryModelsResponse))]
public partial class ConfigurationHttpJsonContext : JsonSerializerContext;
