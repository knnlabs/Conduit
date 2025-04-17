using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities;

public class ModelCost
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)] // Adjust length as needed
    public string ModelIdPattern { get; set; } = string.Empty; // e.g., "openai/gpt-4o", "anthropic.claude-3*", "*-embedding-*"

    [Column(TypeName = "decimal(18, 10)")] // High precision for costs
    public decimal InputTokenCost { get; set; } = 0; // Cost per input token (chat/completion)

    [Column(TypeName = "decimal(18, 10)")]
    public decimal OutputTokenCost { get; set; } = 0; // Cost per output token (chat/completion)

    [Column(TypeName = "decimal(18, 10)")]
    public decimal? EmbeddingTokenCost { get; set; } // Cost per token (embeddings) - nullable if not applicable

    [Column(TypeName = "decimal(18, 4)")] // Cost per image
    public decimal? ImageCostPerImage { get; set; } // Cost per generated image - nullable if not applicable

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Consider adding an index on ModelIdPattern in the DbContext configuration for faster lookups
}
