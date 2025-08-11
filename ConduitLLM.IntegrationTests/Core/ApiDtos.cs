using System.Text.Json.Serialization;

namespace ConduitLLM.IntegrationTests.Core;

// =====================================================
// Provider Management DTOs
// =====================================================

public class CreateProviderRequest
{
    public int ProviderType { get; set; }  // Enum value
    public string ProviderName { get; set; } = "";
    public string? BaseUrl { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class CreateProviderResponse
{
    public int Id { get; set; }
    public string ProviderName { get; set; } = "";
    public int ProviderType { get; set; }
    public bool IsEnabled { get; set; }
}

// =====================================================
// Provider Key DTOs
// =====================================================

public class CreateProviderKeyRequest
{
    public string ApiKey { get; set; } = "";
    public string KeyName { get; set; } = "";
    public string? Organization { get; set; }
    public string? BaseUrl { get; set; }
    public bool IsPrimary { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public int? ProviderAccountGroup { get; set; }
}

public class CreateProviderKeyResponse
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public string KeyName { get; set; } = "";
    public bool IsPrimary { get; set; }
    public bool IsEnabled { get; set; }
    public string ApiKey { get; set; } = "";  // Masked version
}

// =====================================================
// Model Mapping DTOs
// =====================================================

public class CreateModelMappingRequest
{
    public string ModelId { get; set; } = "";  // Model alias
    public string ProviderModelId { get; set; } = "";  // Actual model name
    public int ProviderId { get; set; }
    public int Priority { get; set; } = 0;
    public bool IsEnabled { get; set; } = true;
    public bool SupportsVision { get; set; } = false;
    public bool SupportsChat { get; set; } = true;
    public bool SupportsStreaming { get; set; } = true;
    public bool SupportsFunctionCalling { get; set; } = false;
}

public class CreateModelMappingResponse
{
    public int Id { get; set; }
    public string ModelId { get; set; } = "";
    public string ProviderModelId { get; set; } = "";
    public int ProviderId { get; set; }
}

// =====================================================
// Model Cost DTOs
// =====================================================

public class CreateModelCostRequest
{
    public string CostName { get; set; } = "";
    public int PricingModel { get; set; } = 0; // PricingModel.Standard
    public List<int> ModelProviderMappingIds { get; set; } = new();
    public string ModelType { get; set; } = "chat";
    public int Priority { get; set; } = 0;
    public string? Description { get; set; }
    public decimal InputCostPerMillionTokens { get; set; }
    public decimal OutputCostPerMillionTokens { get; set; }
}

public class CreateModelCostResponse
{
    public int Id { get; set; }
    public string CostName { get; set; } = "";
    public decimal InputCostPerMillionTokens { get; set; }
    public decimal OutputCostPerMillionTokens { get; set; }
}

// =====================================================
// Virtual Key Group DTOs
// =====================================================

public class CreateVirtualKeyGroupRequest
{
    public string GroupName { get; set; } = "";
    public string? ExternalGroupId { get; set; }
    public decimal? InitialBalance { get; set; }
}

public class CreateVirtualKeyGroupResponse
{
    public int Id { get; set; }
    public string GroupName { get; set; } = "";
    public decimal Balance { get; set; }  // Current balance (not InitialBalance)
    public decimal LifetimeCreditsAdded { get; set; }
    public decimal LifetimeSpent { get; set; }
}

// =====================================================
// Virtual Key DTOs
// =====================================================

public class CreateVirtualKeyRequest
{
    public string KeyName { get; set; } = "";
    public string? AllowedModels { get; set; }
    public int VirtualKeyGroupId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Metadata { get; set; }
    public int? RateLimitRpm { get; set; }
    public int? RateLimitRpd { get; set; }
}

public class CreateVirtualKeyResponse
{
    public KeyInfo KeyInfo { get; set; } = new();
    public string VirtualKey { get; set; } = "";
}

public class KeyInfo
{
    public int Id { get; set; }
    public string KeyName { get; set; } = "";
    public int VirtualKeyGroupId { get; set; }
}

public class GetVirtualKeyResponse
{
    public string VirtualKey { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal TotalSpend { get; set; }
    public decimal RemainingCredit { get; set; }
}

// =====================================================
// Chat Completion DTOs
// =====================================================

public class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";
    
    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("object")]
    public string Object { get; set; } = "";
    
    [JsonPropertyName("created")]
    public long Created { get; set; }
    
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";
    
    [JsonPropertyName("choices")]
    public List<ChatChoice> Choices { get; set; } = new();
    
    [JsonPropertyName("usage")]
    public ChatUsage? Usage { get; set; }
}

public class ChatChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("message")]
    public ChatMessage Message { get; set; } = new();
    
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class ChatUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }
    
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
    
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}