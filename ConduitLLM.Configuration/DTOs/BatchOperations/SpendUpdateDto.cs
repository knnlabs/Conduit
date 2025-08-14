using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ConduitLLM.Configuration;

namespace ConduitLLM.Configuration.DTOs.BatchOperations
{
    /// <summary>
    /// Individual spend update item
    /// </summary>
    public class SpendUpdateDto
    {
        /// <summary>
        /// Virtual key to update
        /// </summary>
        [Required]
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Amount to add to spend
        /// </summary>
        [Required]
        [Range(0.0001, 1000000)]
        public decimal Amount { get; set; }

        /// <summary>
        /// Model used
        /// </summary>
        [Required]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Provider type used
        /// </summary>
        [Required]
        public ProviderType ProviderType { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }
}