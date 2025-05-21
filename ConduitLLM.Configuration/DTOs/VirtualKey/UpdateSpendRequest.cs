using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.VirtualKey
{
    /// <summary>
    /// Request to update the spend amount for a virtual key
    /// </summary>
    public class UpdateSpendRequest
    {
        /// <summary>
        /// The cost amount to add to the current spend
        /// </summary>
        [Required]
        public decimal Cost { get; set; }
    }
}