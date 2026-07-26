namespace ConduitLLM.Admin.Models.ModelAuthors
{
    /// <summary>
    /// Data transfer object for updating an existing model author/organization.
    /// </summary>
    /// <remarks>
    /// Supports JSON Merge Patch: omitted properties remain unchanged and null clears nullable
    /// properties. The required name cannot be cleared.
    /// Changes affect the display of the author across all their series and models.
    /// </remarks>
    public class UpdateModelAuthorDto
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public int Id { get; set; }
        /// <summary>
        /// Gets or sets the ID of the author to update.
        /// </summary>

        /// <summary>
        /// Gets or sets the new name for the author.
        /// </summary>
        /// <remarks>
        /// Rename with caution as it affects all references to this author.
        /// The new name must be unique in the system.
        /// Omit this member to keep the existing name.
        /// </remarks>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the new website URL.
        /// </summary>
        /// <remarks>
        /// Update if the organization changes their website.
        /// Omit this member to keep the existing URL; send null to clear it.
        /// </remarks>
        public string? WebsiteUrl { get; set; }

        /// <summary>
        /// Gets or sets the new description.
        /// </summary>
        /// <remarks>
        /// Update to reflect new information about the organization.
        /// Omit this member to keep the existing description; send null to clear it.
        /// </remarks>
        public string? Description { get; set; }
    }
}
