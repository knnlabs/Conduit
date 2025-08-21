using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    public class ModelSeries
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key for the model author.
        /// </summary>
        public int AuthorId { get; set; }

        /// <summary>
        /// The author of the model series (e.g., OpenAI, Anthropic, etc.)
        /// </summary>
        [ForeignKey("AuthorId")]
        public ModelAuthor Author { get; set; } = new ModelAuthor();

        /// <summary>
        /// The name of the model series (e.g., GPT-4 Series, Claude Series, etc.)
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// A brief description of the model series
        /// </summary>
        public string? Description { get; set; } = string.Empty;

        /// <summary>
        /// The tokenizer type used by this model series
        /// </summary>
        public TokenizerType TokenizerType { get; set; }

        /// <summary>
        /// JSON string containing parameter definitions for UI generation.
        /// Example structure:
        /// {
        ///   "temperature": {
        ///     "type": "slider",
        ///     "min": 0,
        ///     "max": 2,
        ///     "step": 0.1,
        ///     "default": 1,
        ///     "label": "Temperature"
        ///   },
        ///   "resolution": {
        ///     "type": "select",
        ///     "options": [
        ///       {"value": "720p", "label": "720p (1280x720)"},
        ///       {"value": "1080p", "label": "1080p (1920x1080)"}
        ///     ],
        ///     "default": "720p",
        ///     "label": "Resolution"
        ///   }
        /// }
        /// </summary>
        public string Parameters { get; set; } = "{}";

        /// <summary>
        /// JSON array of API parameters supported by models in this series.
        /// These are passed through to the provider API without modification.
        /// Example: ["reasoning_effort", "min_p", "top_k"]
        /// Used during bulk mapping to auto-populate supported parameters.
        /// </summary>
        public string? ApiParameters { get; set; }

        /// <summary>
        /// Navigation property for models in this series.
        /// </summary>
        public virtual ICollection<Model> Models { get; set; } = new List<Model>();
    }
}