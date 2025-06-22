using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for DTOs to provide backward compatibility
    /// </summary>
    public static class DtoExtensions
    {
        #region VirtualKeyDto Extensions

        /// <summary>
        /// Gets the display name for a virtual key (alias for KeyName)
        /// </summary>
        public static string GetName(this VirtualKeyDto key)
            => key.KeyName;

        /// <summary>
        /// Gets whether the key is active (alias for IsEnabled)
        /// </summary>
        public static bool GetIsActive(this VirtualKeyDto key)
            => key.IsEnabled;

        /// <summary>
        /// Gets the usage limit (alias for MaxBudget)
        /// </summary>
        public static decimal? GetUsageLimit(this VirtualKeyDto key)
            => key.MaxBudget;

        /// <summary>
        /// Gets the rate limit (alias for RateLimitRpm)
        /// </summary>
        public static int? GetRateLimit(this VirtualKeyDto key)
            => key.RateLimitRpm;

        #endregion

        #region ProviderCredentialDto Extensions

        /// <summary>
        /// Gets the base URL (alias for ApiBase)
        /// </summary>
        public static string? GetBaseUrl(this ProviderCredentialDto credential)
            => credential.ApiBase;

        #endregion

        #region ModelProviderMappingDto Extensions

        /// <summary>
        /// Gets the model alias (alias for ModelId)
        /// </summary>
        public static string GetModelAlias(this ModelProviderMappingDto mapping)
            => mapping.ModelId;

        /// <summary>
        /// Gets the provider model name (alias for ProviderModelId)
        /// </summary>
        public static string GetProviderModelName(this ModelProviderMappingDto mapping)
            => mapping.ProviderModelId;

        /// <summary>
        /// Gets the max context tokens (alias for MaxContextLength)
        /// </summary>
        public static int? GetMaxContextTokens(this ModelProviderMappingDto mapping)
            => mapping.MaxContextLength;

        /// <summary>
        /// Converts ProviderId string to ProviderCredentialId int if possible
        /// </summary>
        public static int GetProviderCredentialId(this ModelProviderMappingDto mapping)
            => int.TryParse(mapping.ProviderId, out int id) ? id : 0;

        #endregion

        #region RequestLogDto Extensions

        /// <summary>
        /// Gets the model name (in WebUI convention, ModelName is primary)
        /// </summary>
        public static string GetModelName(this ConduitLLM.Configuration.DTOs.RequestLogDto log)
            => log.ModelName ?? log.ModelId ?? string.Empty;

        /// <summary>
        /// Gets the model ID (in Configuration convention, ModelId is primary)
        /// </summary>
        public static string GetModelId(this ConduitLLM.Configuration.DTOs.RequestLogDto log)
            => log.ModelId ?? log.ModelName ?? string.Empty;

        #endregion

        #region DetailedCostDataDto Extensions

        /// <summary>
        /// Extension property for request count (WebUI specific)
        /// </summary>
        private static readonly Dictionary<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto, int> _requestCounts = new();

        /// <summary>
        /// Sets the request count for a DetailedCostDataDto
        /// </summary>
        public static void SetRequestCount(this ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto dto, int count)
        {
            _requestCounts[dto] = count;
        }

        /// <summary>
        /// Gets the request count for a DetailedCostDataDto
        /// </summary>
        public static int GetRequestCount(this ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto dto)
        {
            return _requestCounts.TryGetValue(dto, out int count) ? count : 0;
        }

        #endregion

        #region VirtualKeyCostDataDto Extensions

        /// <summary>
        /// Extension properties for VirtualKeyCostDataDto (WebUI specific)
        /// </summary>
        private static readonly Dictionary<ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto, VirtualKeyCostExtendedData> _extendedData = new();

        private class VirtualKeyCostExtendedData
        {
            public int InputTokens { get; set; }
            public int OutputTokens { get; set; }
            public int TotalTokens { get; set; }
            public DateTime FirstRequestAt { get; set; }
            public DateTime LastRequestAt { get; set; }
            public Dictionary<string, decimal> CostsByProvider { get; set; } = new();
            public Dictionary<string, int> TokensByModel { get; set; } = new();
        }

        /// <summary>
        /// Sets extended data for a VirtualKeyCostDataDto
        /// </summary>
        public static void SetExtendedData(this ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto dto,
            int inputTokens = 0,
            int outputTokens = 0,
            int totalTokens = 0,
            DateTime? firstRequestAt = null,
            DateTime? lastRequestAt = null,
            Dictionary<string, decimal>? costsByProvider = null,
            Dictionary<string, int>? tokensByModel = null)
        {
            var data = new VirtualKeyCostExtendedData
            {
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = totalTokens,
                FirstRequestAt = firstRequestAt ?? DateTime.MinValue,
                LastRequestAt = lastRequestAt ?? DateTime.MinValue,
                CostsByProvider = costsByProvider ?? new Dictionary<string, decimal>(),
                TokensByModel = tokensByModel ?? new Dictionary<string, int>()
            };
            _extendedData[dto] = data;
        }

        /// <summary>
        /// Gets input tokens for a VirtualKeyCostDataDto
        /// </summary>
        public static int GetInputTokens(this ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto dto)
            => _extendedData.TryGetValue(dto, out var data) ? data.InputTokens : 0;

        /// <summary>
        /// Gets output tokens for a VirtualKeyCostDataDto
        /// </summary>
        public static int GetOutputTokens(this ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto dto)
            => _extendedData.TryGetValue(dto, out var data) ? data.OutputTokens : 0;

        /// <summary>
        /// Gets total tokens for a VirtualKeyCostDataDto
        /// </summary>
        public static int GetTotalTokens(this ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto dto)
            => _extendedData.TryGetValue(dto, out var data) ? data.TotalTokens : 0;

        /// <summary>
        /// Gets first request time for a VirtualKeyCostDataDto
        /// </summary>
        public static DateTime GetFirstRequestAt(this ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto dto)
            => _extendedData.TryGetValue(dto, out var data) ? data.FirstRequestAt : DateTime.MinValue;

        /// <summary>
        /// Gets last request time for a VirtualKeyCostDataDto
        /// </summary>
        public static DateTime GetLastRequestAt(this ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto dto)
            => _extendedData.TryGetValue(dto, out var data) ? data.LastRequestAt : DateTime.MinValue;

        /// <summary>
        /// Gets costs by provider for a VirtualKeyCostDataDto
        /// </summary>
        public static Dictionary<string, decimal> GetCostsByProvider(this ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto dto)
            => _extendedData.TryGetValue(dto, out var data) ? data.CostsByProvider : new Dictionary<string, decimal>();

        /// <summary>
        /// Gets tokens by model for a VirtualKeyCostDataDto
        /// </summary>
        public static Dictionary<string, int> GetTokensByModel(this ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto dto)
            => _extendedData.TryGetValue(dto, out var data) ? data.TokensByModel : new Dictionary<string, int>();

        #endregion
    }
}