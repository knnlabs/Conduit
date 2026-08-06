using System.Collections.Generic;

namespace ConduitLLM.Admin.DTOs
{
    /// <summary>
    /// DTO describing a provider type that supports tools
    /// </summary>
    public class ToolProviderDto
    {
        /// <summary>
        /// Numeric value of the provider type
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Display name of the provider
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the provider's tool support
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for the result of a bulk provider tool import
    /// </summary>
    public class ProviderToolImportResultDto
    {
        /// <summary>
        /// Number of tools successfully imported
        /// </summary>
        public int Imported { get; set; }

        /// <summary>
        /// Number of tools skipped because they already exist
        /// </summary>
        public int Skipped { get; set; }

        /// <summary>
        /// Total number of tools in the import request
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// Error messages for tools that could not be imported, or null if there were none
        /// </summary>
        public List<string>? Errors { get; set; }
    }
}
