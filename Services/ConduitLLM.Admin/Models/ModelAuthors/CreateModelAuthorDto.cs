namespace ConduitLLM.Admin.Models.ModelAuthors
{
    /// <summary>
    /// Data transfer object for creating a new model author/organization.
    /// </summary>
    /// <remarks>
    /// Use this DTO to register a new organization before adding their model series and models.
    /// Authors represent the top level of the model hierarchy and should be created first.
    /// </remarks>
    public class CreateModelAuthorDto
    {
        /// <summary>
        /// Gets or sets the name of the author/organization.
        /// </summary>
        /// <remarks>
        /// Use the official organization name as it appears in their documentation.
        /// This name must be unique in the system.
        /// Examples: "OpenAI", "Anthropic", "Meta", "Google", "Mistral AI"
        /// </remarks>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional website URL.
        /// </summary>
        /// <remarks>
        /// The organization's official website for reference.
        /// Example: "https://openai.com", "https://anthropic.com"
        /// </remarks>
        public string? WebsiteUrl { get; set; }

        /// <summary>
        /// Gets or sets an optional description of the organization.
        /// </summary>
        /// <remarks>
        /// Brief background about the organization and their focus in AI development.
        /// Example: "Leading AI research company focused on safe and beneficial AGI"
        /// </remarks>
        public string? Description { get; set; }
    }
}