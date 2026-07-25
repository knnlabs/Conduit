using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.VirtualKey
{
    /// <summary>
    /// Rate limit override for one model alias, applied on top of a key's overall ceilings.
    /// </summary>
    /// <remarks>
    /// Overrides narrow, never widen: a request must satisfy the key's limits, its group's, and
    /// the override for the model it names. Either field may be omitted to leave that dimension
    /// governed only by the key and group ceilings.
    /// </remarks>
    public class ModelRateLimitDto
    {
        /// <summary>Requests per minute permitted against this model alias.</summary>
        [Range(1, int.MaxValue, ErrorMessage = "Model requests per minute must be positive.")]
        public int? Rpm { get; set; }

        /// <summary>Tokens per minute permitted against this model alias.</summary>
        [Range(1, int.MaxValue, ErrorMessage = "Model tokens per minute must be positive.")]
        public int? Tpm { get; set; }
    }
}
