namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for provider key credentials
    /// </summary>
    public class ProviderKeyCredentialDto
    {
        /// <summary>
        /// Unique identifier for the key credential
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The provider credential ID this key belongs to
        /// </summary>
        public int ProviderId { get; set; }

        /// <summary>
        /// The provider account group (0-32)
        /// </summary>
        public short ProviderAccountGroup { get; set; }

        /// <summary>
        /// API key or other authentication token (always masked in responses)
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Base URL for the provider API (optional, overrides provider default)
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>
        /// Organization or project ID (optional, overrides provider default)
        /// </summary>
        public string? Organization { get; set; }

        /// <summary>
        /// Whether this key is the primary key for the provider
        /// </summary>
        public bool IsPrimary { get; set; }

        /// <summary>
        /// Whether this key is enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Optional human-readable name for this key
        /// </summary>
        public string? KeyName { get; set; }

        /// <summary>
        /// Date when the key was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Date when the key was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO for creating a new provider key credential
    /// </summary>
    public class CreateProviderKeyCredentialDto
    {
        /// <summary>
        /// The provider account group (0-32)
        /// </summary>
        public short ProviderAccountGroup { get; set; } = 0;

        /// <summary>
        /// API key or other authentication token
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Base URL for the provider API (optional)
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>
        /// Organization or project ID (optional)
        /// </summary>
        public string? Organization { get; set; }

        /// <summary>
        /// Whether to set this key as the primary key
        /// </summary>
        public bool IsPrimary { get; set; }

        /// <summary>
        /// Whether this key is enabled (default: true)
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Optional human-readable name for this key
        /// </summary>
        public string? KeyName { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing provider key credential
    /// </summary>
    public class UpdateProviderKeyCredentialDto
    {
        /// <summary>
        /// The ID of the key to update
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// API key or other authentication token (optional, only if changing)
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Base URL for the provider API (optional)
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>
        /// Organization or project ID (optional)
        /// </summary>
        public string? Organization { get; set; }

        /// <summary>
        /// Whether this key is enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Optional human-readable name for this key
        /// </summary>
        public string? KeyName { get; set; }
    }
}