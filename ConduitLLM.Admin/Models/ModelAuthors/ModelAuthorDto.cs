namespace ConduitLLM.Admin.Models.ModelAuthors
{
    /// <summary>
    /// Data transfer object representing an AI model author or organization.
    /// </summary>
    /// <remarks>
    /// ModelAuthors represent the organizations, companies, or research groups that create
    /// AI models. Examples include OpenAI, Anthropic, Meta, Google, Mistral AI, etc.
    /// 
    /// Authors serve as the top-level grouping for model organization:
    /// - Each author can have multiple model series
    /// - Each series contains multiple model versions
    /// 
    /// This hierarchy (Author -> Series -> Models) helps organize the growing number
    /// of AI models in a logical, manageable structure.
    /// </remarks>
    public class ModelAuthorDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the author.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the author/organization.
        /// </summary>
        /// <remarks>
        /// The official name of the organization creating the models.
        /// Examples: "OpenAI", "Anthropic", "Meta", "Google", "Mistral AI", "Stability AI"
        /// </remarks>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the website URL for the author/organization.
        /// </summary>
        /// <remarks>
        /// The official website where users can learn more about the organization
        /// and their models. Used for reference and documentation links.
        /// </remarks>
        public string? WebsiteUrl { get; set; }

        /// <summary>
        /// Gets or sets a description of the author/organization.
        /// </summary>
        /// <remarks>
        /// Brief background about the organization, their focus areas, or notable
        /// contributions to AI. Helps users understand the organization's expertise
        /// and model characteristics.
        /// </remarks>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets when the author record was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the author record was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}