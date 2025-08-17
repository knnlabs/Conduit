using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Maps various model identifiers used by different providers to a canonical Model entity.
    /// This allows us to recognize that "gpt-4-0125-preview", "gpt-4-turbo-preview", and "gpt-4-1106-preview"
    /// all refer to the same underlying model.
    /// </summary>
    public class ModelIdentifier
    {
        /// <summary>
        /// Unique identifier for this model identifier mapping.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the canonical Model entity.
        /// </summary>
        public int ModelId { get; set; }

        /// <summary>
        /// The identifier string used by a provider or in API calls.
        /// Examples: "gpt-4-0125-preview", "claude-3-opus-20240229", "llama-3-70b-instruct"
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// The provider or source that uses this identifier.
        /// Examples: "openai", "azure", "anthropic", "openrouter", "deepinfra"
        /// Null indicates a universal identifier.
        /// </summary>
        [MaxLength(100)]
        public string? Provider { get; set; }

        /// <summary>
        /// Indicates whether this is the primary/canonical identifier for the model.
        /// </summary>
        public bool IsPrimary { get; set; } = false;

        /// <summary>
        /// Navigation property to the associated Model.
        /// </summary>
        [ForeignKey("ModelId")]
        public virtual Model Model { get; set; } = null!;

        /// <summary>
        /// Additional metadata about this identifier (e.g., deprecated status, release date).
        /// Stored as JSON.
        /// </summary>
        public string? Metadata { get; set; }
    }
}