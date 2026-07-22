using ConduitLLM.Core.Models;

namespace ConduitLLM.Admin.Models
{
    /// <summary>
    /// Represents data for an ephemeral master key stored in cache
    /// </summary>
    public class EphemeralMasterKeyData : EphemeralKeyDataBase
    {
        /// <summary>
        /// Whether the single-use key has been consumed.
        /// </summary>
        public bool IsConsumed { get; set; }

        /// <summary>
        /// Flag indicating this is a valid master key token
        /// </summary>
        public bool IsValid { get; set; } = true;
    }
}
