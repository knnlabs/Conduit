using System;

namespace ConduitLLM.WebUI.DTOs;

/// <summary>
/// WebUI-specific model cost class for backward compatibility with the UI
/// </summary>
public class ModelCost
{
    /// <summary>
    /// Unique identifier for the model cost entry
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Model identification pattern, which can include wildcards
    /// </summary>
    public string ModelIdPattern { get; set; } = string.Empty;

    /// <summary>
    /// Cost per input token for chat/completion requests in USD
    /// </summary>
    public decimal InputTokenCost { get; set; } = 0;

    /// <summary>
    /// Cost per output token for chat/completion requests in USD
    /// </summary>
    public decimal OutputTokenCost { get; set; } = 0;

    /// <summary>
    /// Cost per token for embedding requests in USD, if applicable
    /// </summary>
    public decimal? EmbeddingTokenCost { get; set; }

    /// <summary>
    /// Cost per image for image generation requests in USD, if applicable
    /// </summary>
    public decimal? ImageCostPerImage { get; set; }

    /// <summary>
    /// Description for this model cost entry (for backward compatibility)
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Creation timestamp of this cost record
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last update timestamp of this cost record
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Convert from a ModelCostDto to a WebUI ModelCost
    /// </summary>
    /// <param name="dto">The DTO to convert</param>
    /// <returns>A new ModelCost instance</returns>
    public static ModelCost FromDto(Configuration.DTOs.ModelCostDto dto)
    {
        return new ModelCost
        {
            Id = dto.Id,
            ModelIdPattern = dto.ModelIdPattern,
            InputTokenCost = dto.InputTokenCost,
            OutputTokenCost = dto.OutputTokenCost,
            EmbeddingTokenCost = dto.EmbeddingTokenCost,
            ImageCostPerImage = dto.ImageCostPerImage,
            Description = dto.Description,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }
    
    /// <summary>
    /// Convert to a ModelCostDto
    /// </summary>
    /// <returns>A new ModelCostDto instance</returns>
    public Configuration.DTOs.ModelCostDto ToDto()
    {
        return new Configuration.DTOs.ModelCostDto
        {
            Id = this.Id,
            ModelIdPattern = this.ModelIdPattern,
            InputTokenCost = this.InputTokenCost,
            OutputTokenCost = this.OutputTokenCost,
            EmbeddingTokenCost = this.EmbeddingTokenCost,
            ImageCostPerImage = this.ImageCostPerImage,
            Description = this.Description,
            CreatedAt = this.CreatedAt,
            UpdatedAt = this.UpdatedAt
        };
    }
    
    /// <summary>
    /// Implicit conversion operator to ModelCostDto
    /// </summary>
    /// <param name="modelCost">The ModelCost to convert</param>
    /// <returns>The converted ModelCostDto, or null if the input is null</returns>
    public static implicit operator Configuration.DTOs.ModelCostDto?(ModelCost? modelCost)
    {
        if (modelCost == null)
        {
            return null;
        }
        
        return modelCost.ToDto();
    }
}