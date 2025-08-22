using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Represents a cached provider credential with all associated keys
    /// </summary>
    public class CachedProvider
    {
        /// <summary>
        /// The provider credential entity
        /// </summary>
        public Provider Provider { get; set; } = null!;

        /// <summary>
        /// All key credentials associated with this provider
        /// </summary>
        public List<ProviderKeyCredential> Keys { get; set; } = new();

        /// <summary>
        /// Gets the primary key credential
        /// </summary>
        public ProviderKeyCredential? PrimaryKey => Keys?.FirstOrDefault(k => k.IsPrimary && k.IsEnabled);

        /// <summary>
        /// Gets all enabled keys for this provider
        /// </summary>
        public IEnumerable<ProviderKeyCredential> EnabledKeys => Keys?.Where(k => k.IsEnabled) ?? Enumerable.Empty<ProviderKeyCredential>();

        /// <summary>
        /// Checks if the provider has any enabled keys
        /// </summary>
        public bool HasEnabledKeys => EnabledKeys.Count() > 0;

        /// <summary>
        /// Gets the effective API key (primary key or fallback to legacy)
        /// </summary>
        public string? GetEffectiveApiKey()
        {
            // First try to get primary key
            var primaryKey = PrimaryKey?.ApiKey;
            if (!string.IsNullOrEmpty(primaryKey))
                return primaryKey;

            // No key available
            return null;
        }

        /// <summary>
        /// Cache timestamp for tracking
        /// </summary>
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    }
}