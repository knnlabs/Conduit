using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities;

/// <summary>
/// Represents a log of API requests made using a virtual key
/// </summary>
public class RequestLog
{
    /// <summary>
    /// Unique identifier for the request log
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// ID of the virtual key used for the request
    /// </summary>
    public int VirtualKeyId { get; set; }

    /// <summary>
    /// Foreign key relationship to the virtual key
    /// </summary>
    [ForeignKey("VirtualKeyId")]
    public virtual VirtualKey? VirtualKey { get; set; }

    /// <summary>
    /// Name of the model used for the request
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Type of the request (chat, completion, embedding, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string RequestType { get; set; } = string.Empty;

    /// <summary>
    /// Number of input tokens in the request
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// Number of output tokens in the response
    /// </summary>
    public int OutputTokens { get; set; }

    /// <summary>
    /// Cost of the request
    /// </summary>
    [Column(TypeName = "decimal(10, 6)")]
    public decimal Cost { get; set; }

    /// <summary>
    /// Response time in milliseconds
    /// </summary>
    public double ResponseTimeMs { get; set; }

    /// <summary>
    /// Timestamp of the request
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional identifier of the user making the request
    /// </summary>
    [MaxLength(100)]
    public string? UserId { get; set; }

    /// <summary>
    /// Optional IP address of the client making the request
    /// </summary>
    [MaxLength(50)]
    public string? ClientIp { get; set; }

    /// <summary>
    /// Optional request path
    /// </summary>
    [MaxLength(256)]
    public string? RequestPath { get; set; }

    /// <summary>
    /// Optional status code of the response
    /// </summary>
    public int? StatusCode { get; set; }
}
