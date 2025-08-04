using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Providers.Common.Models;
using ConduitLLM.Providers.Helpers;

namespace ConduitLLM.Providers.Providers.OpenAI
{
    /// <summary>
    /// OpenAIClient partial class containing model listing functionality.
    /// </summary>
    public partial class OpenAIClient
    {
        /// <summary>
        /// Maps the Azure OpenAI response format to the standard models list.
        /// </summary>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            if (_isAzure)
            {
                // Azure has a different response format for listing models
                return await ExecuteApiRequestAsync(async () =>
                {
                    using var client = CreateHttpClient(apiKey);
                    var endpoint = GetModelsEndpoint();

                    var response = await ConduitLLM.Core.Utilities.HttpClientHelper.GetJsonAsync<AzureOpenAIModels.ListDeploymentsResponse>(
                        client,
                        endpoint,
                        new Dictionary<string, string>(),
                        DefaultJsonOptions,
                        Logger,
                        cancellationToken);

                    return response.Data
                        .Select(m =>
                        {
                            var model = ExtendedModelInfo.Create(m.DeploymentId, ProviderName, m.DeploymentId)
                                .WithName(m.Model ?? m.DeploymentId)
                                .WithCapabilities(new ConduitLLM.Providers.Common.Models.ModelCapabilities
                                {
                                    Chat = true,
                                    TextGeneration = true
                                });

                            // Can't add custom properties directly, but they'll be ignored anyway
                            return model;
                        })
                        .ToList();
                }, "GetModels", cancellationToken);
            }

            // Use the base implementation for standard OpenAI
            return await base.GetModelsAsync(apiKey, cancellationToken);
        }
    }

    // Azure-specific model response structures
    namespace AzureOpenAIModels
    {
        public class ListDeploymentsResponse
        {
            [JsonPropertyName("data")]
            public List<DeploymentInfo> Data { get; set; } = new();
        }

        public class DeploymentInfo
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("deploymentId")]
            public string DeploymentId { get; set; } = string.Empty;

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("provisioningState")]
            public string ProvisioningState { get; set; } = string.Empty;
        }
    }
}