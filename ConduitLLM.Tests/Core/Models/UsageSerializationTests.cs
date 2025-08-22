using System.Text.Json;

using ConduitLLM.Core.Models;

using FluentAssertions;

namespace ConduitLLM.Tests.Core.Models;

public class UsageSerializationTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Serialize_AllFieldsPopulated_ShouldSerializeCorrectly()
    {
        // Arrange
        var usage = new Usage
        {
            PromptTokens = 100,
            CompletionTokens = 50,
            TotalTokens = 150,
            CachedInputTokens = 30,
            CachedWriteTokens = 20,
            ImageCount = 2,
            ImageQuality = "hd",
            InferenceSteps = 30,
            VideoDurationSeconds = 60.5,
            VideoResolution = "1920x1080",
            AudioDurationSeconds = 120.75m,
            SearchUnits = 5,
            SearchMetadata = new SearchUsageMetadata
            {
                QueryCount = 1,
                DocumentCount = 450,
                ChunkedDocumentCount = 50
            },
            IsBatch = true,
            Metadata = new Dictionary<string, object>
            {
                ["provider"] = "openai",
                ["cache_ttl"] = 3600,
                ["custom_field"] = true
            }
        };

        // Act
        var json = JsonSerializer.Serialize(usage, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<Usage>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.PromptTokens.Should().Be(100);
        deserialized.CompletionTokens.Should().Be(50);
        deserialized.TotalTokens.Should().Be(150);
        deserialized.CachedInputTokens.Should().Be(30);
        deserialized.CachedWriteTokens.Should().Be(20);
        deserialized.ImageCount.Should().Be(2);
        deserialized.ImageQuality.Should().Be("hd");
        deserialized.InferenceSteps.Should().Be(30);
        deserialized.VideoDurationSeconds.Should().Be(60.5);
        deserialized.VideoResolution.Should().Be("1920x1080");
        deserialized.AudioDurationSeconds.Should().Be(120.75m);
        deserialized.SearchUnits.Should().Be(5);
        deserialized.IsBatch.Should().BeTrue();
        
        deserialized.SearchMetadata.Should().NotBeNull();
        deserialized.SearchMetadata!.QueryCount.Should().Be(1);
        deserialized.SearchMetadata.DocumentCount.Should().Be(450);
        deserialized.SearchMetadata.ChunkedDocumentCount.Should().Be(50);
        
        deserialized.Metadata.Should().NotBeNull();
        deserialized.Metadata!["provider"].ToString().Should().Be("openai");
        deserialized.Metadata["cache_ttl"].ToString().Should().Be("3600");
    }

    [Fact]
    public void Serialize_NullFields_ShouldOmitFromJson()
    {
        // Arrange
        var usage = new Usage
        {
            PromptTokens = 100,
            CompletionTokens = 50,
            TotalTokens = 150
        };

        // Act
        var json = JsonSerializer.Serialize(usage, _jsonOptions);

        // Assert
        json.Should().NotContain("cached_input_tokens");
        json.Should().NotContain("cached_write_tokens");
        json.Should().NotContain("image_count");
        json.Should().NotContain("image_quality");
        json.Should().NotContain("inference_steps");
        json.Should().NotContain("video_duration_seconds");
        json.Should().NotContain("video_resolution");
        json.Should().NotContain("audio_duration_seconds");
        json.Should().NotContain("search_units");
        json.Should().NotContain("search_metadata");
        json.Should().NotContain("is_batch");
        json.Should().NotContain("metadata");
    }

    [Fact]
    public void Deserialize_OldFormat_ShouldHandleBackwardCompatibility()
    {
        // Arrange - JSON from old client without new fields
        var oldJson = @"{
            ""prompt_tokens"": 100,
            ""completion_tokens"": 50,
            ""total_tokens"": 150
        }";

        // Act
        var usage = JsonSerializer.Deserialize<Usage>(oldJson, _jsonOptions);

        // Assert
        usage.Should().NotBeNull();
        usage!.PromptTokens.Should().Be(100);
        usage.CompletionTokens.Should().Be(50);
        usage.TotalTokens.Should().Be(150);
        
        // All new fields should be null
        usage.CachedInputTokens.Should().BeNull();
        usage.CachedWriteTokens.Should().BeNull();
        usage.ImageQuality.Should().BeNull();
        usage.InferenceSteps.Should().BeNull();
        usage.AudioDurationSeconds.Should().BeNull();
        usage.SearchUnits.Should().BeNull();
        usage.SearchMetadata.Should().BeNull();
        usage.Metadata.Should().BeNull();
    }

    [Fact]
    public void Deserialize_PartialNewFields_ShouldHandleCorrectly()
    {
        // Arrange - JSON with some new fields
        var json = @"{
            ""prompt_tokens"": 1000,
            ""completion_tokens"": 500,
            ""total_tokens"": 1500,
            ""cached_input_tokens"": 300,
            ""image_quality"": ""standard"",
            ""inference_steps"": 4
        }";

        // Act
        var usage = JsonSerializer.Deserialize<Usage>(json, _jsonOptions);

        // Assert
        usage.Should().NotBeNull();
        usage!.PromptTokens.Should().Be(1000);
        usage.CompletionTokens.Should().Be(500);
        usage.TotalTokens.Should().Be(1500);
        usage.CachedInputTokens.Should().Be(300);
        usage.ImageQuality.Should().Be("standard");
        usage.InferenceSteps.Should().Be(4);
        
        // Other fields should be null
        usage.CachedWriteTokens.Should().BeNull();
        usage.AudioDurationSeconds.Should().BeNull();
        usage.SearchUnits.Should().BeNull();
    }

    [Fact]
    public void Serialize_CompleteUsageObject_ShouldUseCorrectPropertyNames()
    {
        // Arrange
        var usage = new Usage
        {
            PromptTokens = 100,
            CompletionTokens = 50,
            TotalTokens = 150,
            CachedInputTokens = 30,
            CachedWriteTokens = 20,
            AudioDurationSeconds = 60.5m,
            SearchUnits = 2,
            InferenceSteps = 30,
            ImageQuality = "hd"
        };

        // Act
        var json = JsonSerializer.Serialize(usage, _jsonOptions);
        var jsonDocument = JsonDocument.Parse(json);
        var root = jsonDocument.RootElement;

        // Assert - verify property names match JSON attributes
        root.GetProperty("prompt_tokens").GetInt32().Should().Be(100);
        root.GetProperty("completion_tokens").GetInt32().Should().Be(50);
        root.GetProperty("total_tokens").GetInt32().Should().Be(150);
        root.GetProperty("cached_input_tokens").GetInt32().Should().Be(30);
        root.GetProperty("cached_write_tokens").GetInt32().Should().Be(20);
        root.GetProperty("audio_duration_seconds").GetDecimal().Should().Be(60.5m);
        root.GetProperty("search_units").GetInt32().Should().Be(2);
        root.GetProperty("inference_steps").GetInt32().Should().Be(30);
        root.GetProperty("image_quality").GetString().Should().Be("hd");
    }

    [Fact]
    public void Deserialize_EmptyUsage_ShouldHandleAllNullFields()
    {
        // Arrange
        var json = "{}";

        // Act
        var usage = JsonSerializer.Deserialize<Usage>(json, _jsonOptions);

        // Assert
        usage.Should().NotBeNull();
        usage!.PromptTokens.Should().BeNull();
        usage.CompletionTokens.Should().BeNull();
        usage.TotalTokens.Should().BeNull();
        usage.CachedInputTokens.Should().BeNull();
        usage.CachedWriteTokens.Should().BeNull();
        usage.ImageCount.Should().BeNull();
        usage.ImageQuality.Should().BeNull();
        usage.InferenceSteps.Should().BeNull();
        usage.VideoDurationSeconds.Should().BeNull();
        usage.VideoResolution.Should().BeNull();
        usage.AudioDurationSeconds.Should().BeNull();
        usage.SearchUnits.Should().BeNull();
        usage.SearchMetadata.Should().BeNull();
        usage.IsBatch.Should().BeNull();
        usage.Metadata.Should().BeNull();
    }
}