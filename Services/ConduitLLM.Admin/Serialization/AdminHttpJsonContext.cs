using System.Text.Json.Serialization;

using ConduitLLM.Admin.DTOs;
using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Admin.Serialization;

/// <summary>
/// Source-generated metadata for representative Admin error, list, and operational contracts.
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AdminProblemDetails))]
[JsonSerializable(typeof(PagedResult<MetricKeyCountDto>))]
[JsonSerializable(typeof(BatchSpendingStatusResponse))]
[JsonSerializable(typeof(BatchSpendingInformationResponse))]
[JsonSerializable(typeof(DiscoveryModelsResponse))]
public partial class AdminHttpJsonContext : JsonSerializerContext;
