using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.Components.Shared
{
    /// <summary>
    /// Shared logic for model dropdown components to avoid code duplication.
    /// </summary>
    public static class ModelDropdownLogic
    {
        /// <summary>
        /// Represents model information for dropdown display.
        /// </summary>
        public class ModelInfo
        {
            public string ModelId { get; set; } = "";
            public string Provider { get; set; } = "";
            public ModelCostDto? Cost { get; set; }
        }

        /// <summary>
        /// Converts a list of model IDs to ModelInfo objects with cost information.
        /// </summary>
        public static List<ModelInfo> ConvertToModelInfos(List<string> models, string providerName, List<ModelCostDto>? modelCosts)
        {
            return models.Select(modelId => new ModelInfo
            {
                ModelId = modelId,
                Provider = providerName,
                Cost = GetModelCost(modelId, providerName, modelCosts)
            }).ToList();
        }

        /// <summary>
        /// Gets the cost information for a specific model.
        /// </summary>
        public static ModelCostDto? GetModelCost(string modelId, string providerName, List<ModelCostDto>? modelCosts)
        {
            return modelCosts?.FirstOrDefault(c => 
                c.ModelIdPattern.Equals(modelId, StringComparison.OrdinalIgnoreCase) || 
                c.ModelIdPattern.Equals($"{providerName}/{modelId}", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Generates a description string for the model including provider and cost information.
        /// </summary>
        public static string GetModelDescription(ModelInfo model)
        {
            var parts = new List<string>();
            
            if (!string.IsNullOrEmpty(model.Provider))
            {
                parts.Add($"Provider: {model.Provider}");
            }
            
            if (model.Cost != null)
            {
                var costParts = new List<string>();
                
                // Convert to cost per million tokens for better readability
                var inputCostPerMillion = model.Cost.InputTokenCost * 1_000_000;
                var outputCostPerMillion = model.Cost.OutputTokenCost * 1_000_000;
                
                if (inputCostPerMillion > 0)
                {
                    costParts.Add($"Input: ${inputCostPerMillion:F2}/M");
                }
                
                if (outputCostPerMillion > 0)
                {
                    costParts.Add($"Output: ${outputCostPerMillion:F2}/M");
                }
                
                if (costParts.Any())
                {
                    parts.Add(string.Join(", ", costParts));
                }
                
                if (model.Cost.InputTokenCost == 0 && model.Cost.OutputTokenCost == 0)
                {
                    parts.Add("FREE");
                }
            }
            
            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Searches for models based on the search term, including model ID, provider, cost, and "FREE" keyword.
        /// </summary>
        public static bool SearchModels(ModelInfo model, string searchTerm)
        {
            var searchTermLower = searchTerm.ToLowerInvariant();
            
            // Search by model ID
            if (model.ModelId.ToLowerInvariant().Contains(searchTermLower))
                return true;
                
            // Search by provider name
            if (!string.IsNullOrEmpty(model.Provider) && 
                model.Provider.ToLowerInvariant().Contains(searchTermLower))
                return true;
                
            // Search for "free" models
            if (searchTermLower.Contains("free") && model.Cost != null &&
                model.Cost.InputTokenCost == 0 && model.Cost.OutputTokenCost == 0)
                return true;
                
            // Search by cost values
            if (model.Cost != null)
            {
                var costString = $"{model.Cost.InputTokenCost} {model.Cost.OutputTokenCost}";
                if (costString.ToLowerInvariant().Contains(searchTermLower))
                    return true;
            }
            
            return false;
        }
    }
}