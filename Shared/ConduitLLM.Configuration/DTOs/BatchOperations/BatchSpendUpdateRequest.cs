using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.BatchOperations
{
    /// <summary>
    /// Request to update spend for multiple virtual keys
    /// </summary>
    public class BatchSpendUpdateRequest
    {
        /// <summary>
        /// List of spend updates to process
        /// </summary>
        [Required]
        public List<SpendUpdateDto> Updates { get; set; } = new();
    }
}