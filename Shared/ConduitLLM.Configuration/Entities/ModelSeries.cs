using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

using ConduitLLM.Configuration.Entities.Interfaces;

namespace ConduitLLM.Configuration.Entities
{
    public class ModelSeries : IEntity<int>
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
        /// <remarks>
        /// JsonIgnore is applied to prevent circular reference during serialization.
        /// The cycle is: ModelSeries → Author → ModelSeries
        /// Must not be initialized to a fresh instance: graph-traversing operations
        /// (DbSet.Add/Update) would treat the phantom Author (Id = 0, Name = "") as a new
        /// entity and insert a blank ModelAuthor row, colliding with
        /// IX_ModelAuthor_Name_Unique on later saves (issue #1192).
        /// Null until loaded via Include.
        /// </remarks>
        [ForeignKey("AuthorId")]
        [JsonIgnore]
        public ModelAuthor Author { get; set; } = null!;

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
        /// Navigation property for models in this series.
        /// </summary>
        /// <remarks>
        /// JsonIgnore is applied to prevent circular reference during serialization.
        /// The cycle is: ModelSeries → Models → Model → Series → ModelSeries
        /// </remarks>
        [JsonIgnore]
        public virtual ICollection<Model> Models { get; set; } = new List<Model>();
    }
}