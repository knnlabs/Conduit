using System.Text.Json.Serialization;

using ConduitLLM.Security.Models;

namespace ConduitLLM.Security.Serialization;

/// <summary>Source-generated contracts for security data shared through distributed cache.</summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(BannedIpInfo))]
[JsonSerializable(typeof(FailedAuthData))]
public partial class SecurityCacheJsonContext : JsonSerializerContext;
