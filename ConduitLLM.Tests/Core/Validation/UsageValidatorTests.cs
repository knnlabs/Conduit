using System.Collections.Generic;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Validation;
using FluentAssertions;
using Xunit;

namespace ConduitLLM.Tests.Core.Validation;

public class UsageValidatorTests
{
    private readonly UsageValidator _validator = new();

    [Fact]
    public void Validate_ValidUsage_ShouldReturnNoErrors()
    {
        // Arrange
        var usage = new Usage
        {
            PromptTokens = 100,
            CompletionTokens = 50,
            TotalTokens = 150
        };

        // Act
        var result = _validator.Validate(usage);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidTotalTokens_ShouldReturnError()
    {
        // Arrange
        var usage = new Usage
        {
            PromptTokens = 100,
            CompletionTokens = 50,
            TotalTokens = 200 // Should be 150
        };

        // Act
        var result = _validator.Validate(usage);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("Total tokens (200) does not equal prompt (100) + completion (50) tokens");
    }

    [Fact]
    public void Validate_CachedTokensExceedPromptTokens_ShouldReturnError()
    {
        // Arrange
        var usage = new Usage
        {
            PromptTokens = 100,
            CachedInputTokens = 150
        };

        // Act
        var result = _validator.Validate(usage);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("Cached input tokens (150) exceed total prompt tokens (100)");
    }

    [Fact]
    public void Validate_CombinedCachedTokensExceedPromptTokens_ShouldReturnError()
    {
        // Arrange
        var usage = new Usage
        {
            PromptTokens = 100,
            CachedInputTokens = 60,
            CachedWriteTokens = 50
        };

        // Act
        var result = _validator.Validate(usage);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("Combined cached tokens (110) exceed total prompt tokens (100)");
    }

    [Fact]
    public void Validate_NegativeSearchUnits_ShouldReturnError()
    {
        // Arrange
        var usage = new Usage
        {
            SearchUnits = -5
        };

        // Act
        var result = _validator.Validate(usage);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Search units must be positive");
    }

    [Fact]
    public void Validate_InferenceStepsOutOfRange_ShouldReturnErrors()
    {
        // Arrange - too low
        var usageLow = new Usage { InferenceSteps = 0 };
        
        // Act
        var resultLow = _validator.Validate(usageLow);

        // Assert
        resultLow.IsValid.Should().BeFalse();
        resultLow.Errors.Should().ContainSingle()
            .Which.Should().Contain("Inference steps (0) out of valid range (1-1000)");

        // Arrange - too high
        var usageHigh = new Usage { InferenceSteps = 1001 };
        
        // Act
        var resultHigh = _validator.Validate(usageHigh);

        // Assert
        resultHigh.IsValid.Should().BeFalse();
        resultHigh.Errors.Should().ContainSingle()
            .Which.Should().Contain("Inference steps (1001) out of valid range (1-1000)");
    }

    [Fact]
    public void Validate_ValidInferenceSteps_ShouldReturnNoErrors()
    {
        // Arrange
        var usage = new Usage
        {
            InferenceSteps = 30,
            ImageCount = 1
        };

        // Act
        var result = _validator.Validate(usage);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NegativeTokenValues_ShouldReturnErrors()
    {
        // Arrange
        var usage = new Usage
        {
            PromptTokens = -10,
            CompletionTokens = -20,
            TotalTokens = -30,
            CachedInputTokens = -5,
            CachedWriteTokens = -8
        };

        // Act
        var result = _validator.Validate(usage);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(7); // 5 negative errors + 2 cached token validation errors
        result.Errors.Should().Contain("Prompt tokens cannot be negative");
        result.Errors.Should().Contain("Completion tokens cannot be negative");
        result.Errors.Should().Contain("Total tokens cannot be negative");
        result.Errors.Should().Contain("Cached input tokens cannot be negative");
        result.Errors.Should().Contain("Cache write tokens cannot be negative");
        result.Errors.Should().Contain("Cached input tokens (-5) exceed total prompt tokens (-10)");
        result.Errors.Should().Contain("Cache write tokens (-8) exceed total prompt tokens (-10)");
    }

    [Fact]
    public void Validate_InvalidMediaDurations_ShouldReturnErrors()
    {
        // Arrange
        var usage = new Usage
        {
            ImageCount = 0,
            VideoDurationSeconds = -5.5,
            AudioDurationSeconds = 0
        };

        // Act
        var result = _validator.Validate(usage);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain("Image count must be positive");
        result.Errors.Should().Contain("Video duration must be positive");
        result.Errors.Should().Contain("Audio duration must be positive");
    }

    [Fact]
    public void Validate_SearchMetadataInvalid_ShouldReturnErrors()
    {
        // Arrange
        var usage = new Usage
        {
            SearchUnits = 5,
            SearchMetadata = new SearchUsageMetadata
            {
                QueryCount = 0,
                DocumentCount = -10,
                ChunkedDocumentCount = 20
            }
        };

        // Act
        var result = _validator.Validate(usage);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Search metadata query count must be positive");
        result.Errors.Should().Contain("Search metadata document count cannot be negative");
        result.Errors.Should().Contain("Chunked document count (20) cannot exceed total document count (-10)");
    }

    [Fact]
    public void Validate_ValidSearchMetadata_ShouldReturnNoErrors()
    {
        // Arrange
        var usage = new Usage
        {
            SearchUnits = 3,
            SearchMetadata = new SearchUsageMetadata
            {
                QueryCount = 1,
                DocumentCount = 250,
                ChunkedDocumentCount = 50
            }
        };

        // Act
        var result = _validator.Validate(usage);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ComplexValidUsage_ShouldReturnNoErrors()
    {
        // Arrange
        var usage = new Usage
        {
            PromptTokens = 1000,
            CompletionTokens = 500,
            TotalTokens = 1500,
            CachedInputTokens = 300,
            CachedWriteTokens = 200,
            ImageCount = 2,
            ImageQuality = "hd",
            InferenceSteps = 50,
            AudioDurationSeconds = 120.5m,
            Metadata = new Dictionary<string, object>
            {
                ["cache_ttl"] = 3600,
                ["provider"] = "anthropic"
            }
        };

        // Act
        var result = _validator.Validate(usage);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NullableFieldsNotSet_ShouldReturnNoErrors()
    {
        // Arrange
        var usage = new Usage(); // All fields are null

        // Act
        var result = _validator.Validate(usage);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var usage = new Usage
        {
            PromptTokens = 100,
            CompletionTokens = 50,
            TotalTokens = 200, // Wrong total
            CachedInputTokens = 150, // Exceeds prompt tokens
            SearchUnits = -5, // Negative
            InferenceSteps = 2000 // Out of range
        };

        // Act
        var result = _validator.Validate(usage);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(4);
    }
}