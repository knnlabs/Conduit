using System;
using System.Collections.Generic;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Services;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Services
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    public class NotificationSeverityClassifierTests : TestBase
    {
        private readonly NotificationSeverityClassifier _classifier;

        public NotificationSeverityClassifierTests(ITestOutputHelper output) : base(output)
        {
            _classifier = new NotificationSeverityClassifier();
        }

        [Theory]
        [InlineData("openai", NotificationSeverity.High)]
        [InlineData("anthropic", NotificationSeverity.High)]
        [InlineData("google", NotificationSeverity.High)]
        [InlineData("microsoft", NotificationSeverity.High)]
        [InlineData("unknown-provider", NotificationSeverity.Low)]
        public void ClassifyNewModel_ByProvider_ReturnsExpectedSeverity(string provider, NotificationSeverity expected)
        {
            // Arrange
            var model = new DiscoveredModelInfo
            {
                ModelId = "test-model",
                Capabilities = new ModelCapabilityInfo { Chat = true }
            };

            // Act
            var result = _classifier.ClassifyNewModel(provider, model);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void ClassifyNewModel_WithMultipleAdvancedCapabilities_ReturnsHigh()
        {
            // Arrange
            var model = new DiscoveredModelInfo
            {
                ModelId = "advanced-model",
                Capabilities = new ModelCapabilityInfo
                {
                    Chat = true,
                    Vision = true,
                    Embeddings = true,
                    FunctionCalling = true
                }
            };

            // Act
            var result = _classifier.ClassifyNewModel("unknown-provider", model);

            // Assert
            result.Should().Be(NotificationSeverity.High);
        }

        [Fact]
        public void ClassifyNewModel_WithSingleAdvancedCapability_ReturnsMedium()
        {
            // Arrange
            var model = new DiscoveredModelInfo
            {
                ModelId = "medium-model",
                Capabilities = new ModelCapabilityInfo
                {
                    Chat = true,
                    Vision = true
                }
            };

            // Act
            var result = _classifier.ClassifyNewModel("unknown-provider", model);

            // Assert
            result.Should().Be(NotificationSeverity.Medium);
        }

        [Theory]
        [InlineData("Vision: False → True", NotificationSeverity.High)]
        [InlineData("Embeddings: False → True", NotificationSeverity.High)]
        [InlineData("Function calling: False → True", NotificationSeverity.High)]
        [InlineData("Video generation: False → True", NotificationSeverity.High)]
        [InlineData("Chat: False → True", NotificationSeverity.Low)]
        public void ClassifyCapabilityChange_WithCapabilityAdditions_ReturnsExpectedSeverity(string change, NotificationSeverity expected)
        {
            // Arrange
            var changes = new List<string> { change };

            // Act
            var result = _classifier.ClassifyCapabilityChange("openai", "gpt-4", changes);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void ClassifyCapabilityChange_WithCapabilityRemoval_ReturnsMedium()
        {
            // Arrange
            var changes = new List<string> { "Vision: True → False" };

            // Act
            var result = _classifier.ClassifyCapabilityChange("openai", "gpt-4", changes);

            // Assert
            result.Should().Be(NotificationSeverity.Medium);
        }

        [Theory]
        [InlineData(60, NotificationSeverity.High)]
        [InlineData(50, NotificationSeverity.Medium)]
        [InlineData(25, NotificationSeverity.Medium)]
        [InlineData(10, NotificationSeverity.Low)]
        [InlineData(5, NotificationSeverity.Low)]
        [InlineData(-60, NotificationSeverity.High)]
        [InlineData(-25, NotificationSeverity.Medium)]
        [InlineData(-5, NotificationSeverity.Low)]
        public void ClassifyPriceChange_ByPercentage_ReturnsExpectedSeverity(decimal percentage, NotificationSeverity expected)
        {
            // Act
            var result = _classifier.ClassifyPriceChange("openai", "gpt-4", percentage);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("provider_offline", NotificationSeverity.Critical)]
        [InlineData("provider_error", NotificationSeverity.Critical)]
        [InlineData("authentication_failed", NotificationSeverity.Critical)]
        [InlineData("provider_online", NotificationSeverity.High)]
        [InlineData("new_provider", NotificationSeverity.High)]
        [InlineData("provider_updated", NotificationSeverity.Medium)]
        [InlineData("rate_limit_changed", NotificationSeverity.Medium)]
        [InlineData("metadata_updated", NotificationSeverity.Low)]
        public void ClassifyProviderEvent_ByEventType_ReturnsExpectedSeverity(string eventType, NotificationSeverity expected)
        {
            // Act
            var result = _classifier.ClassifyProviderEvent("openai", eventType);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void ClassifyCapabilityChange_WithMixedChanges_PrioritizesHighestSeverity()
        {
            // Arrange
            var changes = new List<string>
            {
                "Description: Updated",  // Low
                "Vision: False → True",  // High
                "Context window: 4k → 8k"  // Low
            };

            // Act
            var result = _classifier.ClassifyCapabilityChange("openai", "gpt-4", changes);

            // Assert
            result.Should().Be(NotificationSeverity.High);
        }
    }
}