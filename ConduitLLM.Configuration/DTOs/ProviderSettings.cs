namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Additional provider-specific settings
    /// </summary>
    public class ProviderSettings : Dictionary<string, object>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderSettings"/> class.
        /// </summary>
        public ProviderSettings() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderSettings"/> class with the specified dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary to copy settings from.</param>
        public ProviderSettings(IDictionary<string, object> dictionary) : base(dictionary)
        {
        }
    }
}