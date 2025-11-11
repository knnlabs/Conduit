namespace ConduitLLM.Admin.Models.ModelAuthors
{
    /// <summary>
    /// Data transfer object for updating an existing model author/organization.
    /// </summary>
    /// <remarks>
    /// Supports partial updates - only non-null properties will be modified.
    /// Changes affect the display of the author across all their series and models.
    /// </remarks>
    public class UpdateModelAuthorDto
    {
        /// <summary>
        /// Gets or sets the ID of the author to update.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the new name for the author.
        /// </summary>
        /// <remarks>
        /// Rename with caution as it affects all references to this author.
        /// The new name must be unique in the system.
        /// Leave null to keep existing name.
        /// </remarks>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the new website URL.
        /// </summary>
        /// <remarks>
        /// Update if the organization changes their website.
        /// Leave null to keep existing URL.
        /// </remarks>
        public string? WebsiteUrl { get; set; }

        /// <summary>
        /// Gets or sets the new description.
        /// </summary>
        /// <remarks>
        /// Update to reflect new information about the organization.
        /// Leave null to keep existing description.
        /// </remarks>
        public string? Description { get; set; }
    }
}