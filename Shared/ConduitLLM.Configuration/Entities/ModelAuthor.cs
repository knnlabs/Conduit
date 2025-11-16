using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.Entities
{
    public class ModelAuthor
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The name of the model creator (e.g., OpenAI, Anthropic, etc.)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the model author/organization.
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// URL to the author's website or documentation.
        /// </summary>
        [MaxLength(500)]
        public string? WebsiteUrl { get; set; }

        /// <summary>
        /// Navigation property for all model series by this author.
        /// </summary>
        public virtual ICollection<ModelSeries> ModelSeries { get; set; } = new List<ModelSeries>();
    }
}