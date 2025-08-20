using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Admin.Models.ModelSeries
{
    /// <summary>
    /// Data transfer object for updating an existing model series.
    /// </summary>
    /// <remarks>
    /// Supports partial updates to model series properties. Only properties that are
    /// provided (non-null) will be updated, allowing for targeted modifications without
    /// affecting other properties.
    /// 
    /// Common update scenarios:
    /// - Updating the description to reflect new capabilities
    /// - Changing UI parameters to add new configuration options
    /// - Correcting the tokenizer type if initially misconfigured
    /// - Renaming a series (use with caution)
    /// 
    /// Note that changes to fundamental properties like tokenizer type should be done
    /// carefully as they affect all models in the series and may impact token counting
    /// and cost calculations.
    /// </remarks>
    public class UpdateModelSeriesDto
    {
        /// <summary>
        /// Gets or sets the ID of the series to update.
        /// </summary>
        /// <remarks>
        /// This must match the ID in the request URL for validation.
        /// Identifies which series record will be modified.
        /// </remarks>
        /// <value>The unique identifier of the series to update.</value>
        public int Id { get; set; }
        
        /// <summary>
        /// Gets or sets the new name for the series.
        /// </summary>
        /// <remarks>
        /// Renaming a series should be done carefully as it may affect:
        /// - UI displays and groupings
        /// - Documentation references
        /// - User expectations and recognition
        /// 
        /// Only provide this if you intend to rename the series.
        /// The new name should maintain clarity and follow naming conventions.
        /// Leave null to keep the existing name.
        /// </remarks>
        /// <value>The new series name, or null to keep existing.</value>
        public string? Name { get; set; }
        
        /// <summary>
        /// Gets or sets the new description for the series.
        /// </summary>
        /// <remarks>
        /// Update the description to reflect:
        /// - New capabilities added to models in the series
        /// - Changed positioning or use cases
        /// - Additional context or clarifications
        /// 
        /// This is commonly updated as model families evolve and gain new features.
        /// Leave null to keep the existing description.
        /// </remarks>
        /// <value>The new description, or null to keep existing.</value>
        public string? Description { get; set; }
        
        /// <summary>
        /// Gets or sets the new tokenizer type for the series.
        /// </summary>
        /// <remarks>
        /// Changing the tokenizer type is a significant operation that affects:
        /// - Token counting for all models in the series
        /// - Cost calculations based on token usage
        /// - Context window measurements
        /// 
        /// This should only be changed if:
        /// - The tokenizer was initially misconfigured
        /// - The model family has switched to a new tokenization scheme
        /// - You're correcting an error in the original configuration
        /// 
        /// Ensure all models in the series actually use the new tokenizer type.
        /// Leave null to keep the existing tokenizer.
        /// </remarks>
        /// <value>The new tokenizer type, or null to keep existing.</value>
        public TokenizerType? TokenizerType { get; set; }
        
        /// <summary>
        /// Gets or sets the new UI parameters configuration.
        /// </summary>
        /// <remarks>
        /// Update the parameters JSON to:
        /// - Add new configuration options for recent model features
        /// - Adjust parameter ranges based on model improvements
        /// - Fix incorrect default values or constraints
        /// - Add new UI hints or customizations
        /// 
        /// The JSON should be valid and follow the established schema for UI parameters.
        /// Common updates include adjusting temperature ranges, adding new sampling
        /// parameters, or updating token limits.
        /// 
        /// Example of adding a new parameter:
        /// {
        ///   "temperature": {"min": 0, "max": 2, "default": 0.7},
        ///   "top_k": {"min": 1, "max": 100, "default": 40},  // New parameter
        ///   "max_tokens": {"min": 1, "max": 8192, "default": 1000}  // Updated limit
        /// }
        /// 
        /// Leave null to keep the existing parameters.
        /// </remarks>
        /// <value>The new parameters JSON string, or null to keep existing.</value>
        public string? Parameters { get; set; }
    }
}