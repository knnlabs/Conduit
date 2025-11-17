using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Validation;

/// <summary>
/// Validates usage statistics for consistency and correctness.
/// </summary>
public class UsageValidator
{
    /// <summary>
    /// Validates a Usage object for consistency and correctness.
    /// </summary>
    /// <param name="usage">The usage object to validate.</param>
    /// <returns>A validation result containing any errors found.</returns>
    public ValidationResult Validate(Usage usage)
    {
        var errors = new List<string>();

        // Validate token counts
        if (usage.PromptTokens.HasValue && usage.CompletionTokens.HasValue && usage.TotalTokens.HasValue)
        {
            // Total tokens should equal prompt + completion
            var expectedTotal = usage.PromptTokens.Value + usage.CompletionTokens.Value;
            if (usage.TotalTokens.Value != expectedTotal)
            {
                errors.Add($"Total tokens ({usage.TotalTokens.Value}) does not equal prompt ({usage.PromptTokens.Value}) + completion ({usage.CompletionTokens.Value}) tokens");
            }
        }

        // Validate cached tokens
        if (usage.CachedInputTokens.HasValue && usage.PromptTokens.HasValue)
        {
            // Cached tokens should not exceed prompt tokens
            if (usage.CachedInputTokens.Value > usage.PromptTokens.Value)
            {
                errors.Add($"Cached input tokens ({usage.CachedInputTokens.Value}) exceed total prompt tokens ({usage.PromptTokens.Value})");
            }
        }

        if (usage.CachedWriteTokens.HasValue && usage.PromptTokens.HasValue)
        {
            // Cache write tokens should not exceed prompt tokens
            if (usage.CachedWriteTokens.Value > usage.PromptTokens.Value)
            {
                errors.Add($"Cache write tokens ({usage.CachedWriteTokens.Value}) exceed total prompt tokens ({usage.PromptTokens.Value})");
            }
        }

        if (usage.CachedInputTokens.HasValue && usage.CachedWriteTokens.HasValue && usage.PromptTokens.HasValue)
        {
            // Combined cached tokens should not exceed prompt tokens
            var totalCached = usage.CachedInputTokens.Value + usage.CachedWriteTokens.Value;
            if (totalCached > usage.PromptTokens.Value)
            {
                errors.Add($"Combined cached tokens ({totalCached}) exceed total prompt tokens ({usage.PromptTokens.Value})");
            }
        }

        // Validate search units
        if (usage.SearchUnits.HasValue && usage.SearchUnits.Value <= 0)
        {
            errors.Add("Search units must be positive");
        }

        // Validate inference steps
        if (usage.InferenceSteps.HasValue)
        {
            if (usage.InferenceSteps.Value < 1 || usage.InferenceSteps.Value > 1000)
            {
                errors.Add($"Inference steps ({usage.InferenceSteps.Value}) out of valid range (1-1000)");
            }
        }

        // Validate image count
        if (usage.ImageCount.HasValue && usage.ImageCount.Value <= 0)
        {
            errors.Add("Image count must be positive");
        }

        // Validate video duration
        if (usage.VideoDurationSeconds.HasValue && usage.VideoDurationSeconds.Value <= 0)
        {
            errors.Add("Video duration must be positive");
        }


        // Validate search metadata consistency
        if (usage.SearchMetadata != null)
        {
            if (usage.SearchMetadata.QueryCount <= 0)
            {
                errors.Add("Search metadata query count must be positive");
            }

            if (usage.SearchMetadata.DocumentCount < 0)
            {
                errors.Add("Search metadata document count cannot be negative");
            }

            if (usage.SearchMetadata.ChunkedDocumentCount < 0)
            {
                errors.Add("Search metadata chunked document count cannot be negative");
            }

            if (usage.SearchMetadata.ChunkedDocumentCount > usage.SearchMetadata.DocumentCount)
            {
                errors.Add($"Chunked document count ({usage.SearchMetadata.ChunkedDocumentCount}) cannot exceed total document count ({usage.SearchMetadata.DocumentCount})");
            }

            // Validate search units calculation if present
            if (usage.SearchUnits.HasValue)
            {
                // According to the issue: 1 search unit = 1 query + up to 100 documents
                var expectedSearchUnits = usage.SearchMetadata.QueryCount * 
                    ((usage.SearchMetadata.DocumentCount + usage.SearchMetadata.ChunkedDocumentCount + 99) / 100);
                
                // Allow some flexibility in the calculation (e.g., different providers might calculate slightly differently)
                if (Math.Abs(usage.SearchUnits.Value - expectedSearchUnits) > usage.SearchMetadata.QueryCount)
                {
                    errors.Add($"Search units ({usage.SearchUnits.Value}) doesn't match expected calculation based on metadata");
                }
            }
        }

        // Validate negative values
        if (usage.PromptTokens.HasValue && usage.PromptTokens.Value < 0)
        {
            errors.Add("Prompt tokens cannot be negative");
        }

        if (usage.CompletionTokens.HasValue && usage.CompletionTokens.Value < 0)
        {
            errors.Add("Completion tokens cannot be negative");
        }

        if (usage.TotalTokens.HasValue && usage.TotalTokens.Value < 0)
        {
            errors.Add("Total tokens cannot be negative");
        }

        if (usage.CachedInputTokens.HasValue && usage.CachedInputTokens.Value < 0)
        {
            errors.Add("Cached input tokens cannot be negative");
        }

        if (usage.CachedWriteTokens.HasValue && usage.CachedWriteTokens.Value < 0)
        {
            errors.Add("Cache write tokens cannot be negative");
        }

        return new ValidationResult(errors);
    }
}

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// List of validation errors found.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Indicates whether the validation passed (no errors).
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Creates a new validation result.
    /// </summary>
    /// <param name="errors">List of validation errors.</param>
    public ValidationResult(List<string> errors)
    {
        Errors = errors.AsReadOnly();
    }
}