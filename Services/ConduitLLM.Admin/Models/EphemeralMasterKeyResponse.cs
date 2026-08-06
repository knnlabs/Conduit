using System.Text.Json.Serialization;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Admin.Models
{
    /// <summary>
    /// Response model for ephemeral master key generation
    /// </summary>
    public class EphemeralMasterKeyResponse : EphemeralKeyResponseBase
    {
        /// <summary>
        /// The generated ephemeral master key token
        /// </summary>
        [JsonPropertyName("ephemeralMasterKey")]
        public string EphemeralMasterKey
        {
            get => Token;
            set => Token = value;
        }
    }
}
