using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Functions.Entities;

/// <summary>
/// Maps function configurations to their cost configurations.
/// Allows different functions to share cost configs or have function-specific pricing.
/// </summary>
[Table("FunctionCostMappings")]
public class FunctionCostMapping
{
    /// <summary>
    /// Unique identifier for this mapping
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the function configuration
    /// </summary>
    [Required]
    public int FunctionConfigurationId { get; set; }

    /// <summary>
    /// Navigation property to the function configuration
    /// </summary>
    [ForeignKey(nameof(FunctionConfigurationId))]
    public FunctionConfiguration? FunctionConfiguration { get; set; }

    /// <summary>
    /// Foreign key to the function cost configuration
    /// </summary>
    [Required]
    public int FunctionCostId { get; set; }

    /// <summary>
    /// Navigation property to the cost configuration
    /// </summary>
    [ForeignKey(nameof(FunctionCostId))]
    public FunctionCost? FunctionCost { get; set; }

    /// <summary>
    /// Whether this mapping is currently active
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this mapping was created
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
