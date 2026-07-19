using System;
using System.Collections.Generic;
using System.Text.Json;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Pricing;
using ConduitLLM.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class PricingRulesEvaluatorTests
    {
        private readonly Mock<IPricingAuditService> _auditServiceMock;
        private readonly Mock<ILogger<PricingRulesEvaluator>> _loggerMock;
        private readonly PricingRulesEvaluator _evaluator;

        public PricingRulesEvaluatorTests()
        {
            _auditServiceMock = new Mock<IPricingAuditService>();
            _loggerMock = new Mock<ILogger<PricingRulesEvaluator>>();
            _evaluator = new PricingRulesEvaluator(_loggerMock.Object, _auditServiceMock.Object);
        }

        #region Basic Evaluation Tests

        [Fact]
        public void Evaluate_NoRules_UsesDefaultRate()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "per_second",
                DefaultRate = 0.025m,
                UnitField = "VideoDurationSeconds",
                Rules = new List<PricingRule>()
            };

            var parameters = new Dictionary<string, object> { ["resolution"] = "1080p" };
            var usage = new Usage { VideoDurationSeconds = 10 };

            // Act
            var result = _evaluator.Evaluate(config, parameters, usage);

            // Assert
            result.UsedDefaultRate.Should().BeTrue();
            result.Rate.Should().Be(0.025m);
            result.Quantity.Should().Be(10m);
            result.Cost.Should().Be(0.25m); // 10 * 0.025
            result.MatchedRule.Should().BeNull();
        }

        [Fact]
        public void Evaluate_MatchingRule_UsesRuleRate()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "per_second",
                DefaultRate = 0.025m,
                UnitField = "VideoDurationSeconds",
                Rules = new List<PricingRule>
                {
                    new PricingRule
                    {
                        Priority = 1,
                        Description = "1080p pricing",
                        Conditions = new Dictionary<string, object> { ["resolution"] = "1080p" },
                        Rate = 0.06m
                    }
                }
            };

            var parameters = new Dictionary<string, object> { ["resolution"] = "1080p" };
            var usage = new Usage { VideoDurationSeconds = 10 };

            // Act
            var result = _evaluator.Evaluate(config, parameters, usage);

            // Assert
            result.UsedDefaultRate.Should().BeFalse();
            result.Rate.Should().Be(0.06m);
            result.Quantity.Should().Be(10m);
            result.Cost.Should().Be(0.60m); // 10 * 0.06
            result.MatchedRule.Should().NotBeNull();
            result.MatchedRule!.Description.Should().Be("1080p pricing");
        }

        [Fact]
        public void Evaluate_NoMatchingRule_UsesDefaultRate()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "per_second",
                DefaultRate = 0.015m,
                UnitField = "VideoDurationSeconds",
                Rules = new List<PricingRule>
                {
                    new PricingRule
                    {
                        Priority = 1,
                        Description = "1080p pricing",
                        Conditions = new Dictionary<string, object> { ["resolution"] = "1080p" },
                        Rate = 0.06m
                    }
                }
            };

            var parameters = new Dictionary<string, object> { ["resolution"] = "720p" };
            var usage = new Usage { VideoDurationSeconds = 10 };

            // Act
            var result = _evaluator.Evaluate(config, parameters, usage);

            // Assert
            result.UsedDefaultRate.Should().BeTrue();
            result.Rate.Should().Be(0.015m);
            result.Cost.Should().Be(0.15m); // 10 * 0.015
        }

        [Fact]
        public void Evaluate_NoMatchingRuleAndZeroDefault_Throws()
        {
            var config = new PricingRulesConfig
            {
                PricingType = "per_second",
                DefaultRate = 0,
                UnitField = "VideoDurationSeconds",
                Rules = new List<PricingRule>
                {
                    new()
                    {
                        Conditions = new Dictionary<string, object> { ["resolution"] = "1080p" },
                        Rate = 0.06m
                    }
                }
            };

            var act = () => _evaluator.Evaluate(
                config,
                new Dictionary<string, object> { ["resolution"] = "720p" },
                new Usage { VideoDurationSeconds = 10 });

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*no positive default rate*");
        }

        [Fact]
        public void Evaluate_MissingRequiredQuantity_Throws()
        {
            var config = new PricingRulesConfig
            {
                PricingType = "per_second",
                DefaultRate = 0.025m,
                UnitField = "VideoDurationSeconds"
            };

            var act = () => _evaluator.Evaluate(config, new Dictionary<string, object>(), new Usage());

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*positive quantity*");
        }

        #endregion

        #region Priority Tests

        [Fact]
        public void Evaluate_MultipleMatchingRules_UsesHighestPriority()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "per_second",
                DefaultRate = 0.01m,
                UnitField = "VideoDurationSeconds",
                Rules = new List<PricingRule>
                {
                    new PricingRule
                    {
                        Priority = 1,
                        Description = "Low priority rule",
                        Conditions = new Dictionary<string, object> { ["resolution"] = "1080p" },
                        Rate = 0.05m
                    },
                    new PricingRule
                    {
                        Priority = 10,
                        Description = "High priority rule",
                        Conditions = new Dictionary<string, object> { ["resolution"] = "1080p" },
                        Rate = 0.10m
                    }
                }
            };

            var parameters = new Dictionary<string, object> { ["resolution"] = "1080p" };
            var usage = new Usage { VideoDurationSeconds = 5 };

            // Act
            var result = _evaluator.Evaluate(config, parameters, usage);

            // Assert
            result.MatchedRule.Should().NotBeNull();
            result.MatchedRule!.Description.Should().Be("High priority rule");
            result.Rate.Should().Be(0.10m);
            result.Cost.Should().Be(0.50m);
        }

        #endregion

        #region Multi-Condition Tests

        [Fact]
        public void Evaluate_MultipleConditions_AllMustMatch()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "per_second",
                DefaultRate = 0.025m,
                UnitField = "VideoDurationSeconds",
                Rules = new List<PricingRule>
                {
                    new PricingRule
                    {
                        Priority = 1,
                        Description = "1080p with audio",
                        Conditions = new Dictionary<string, object>
                        {
                            ["resolution"] = "1080p",
                            ["with_audio"] = true
                        },
                        Rate = 0.15m
                    }
                }
            };

            var usage = new Usage { VideoDurationSeconds = 10 };

            // Act - Only resolution matches
            var parametersPartial = new Dictionary<string, object>
            {
                ["resolution"] = "1080p",
                ["with_audio"] = false
            };
            var resultPartial = _evaluator.Evaluate(config, parametersPartial, usage);

            // Assert - Should use default rate
            resultPartial.UsedDefaultRate.Should().BeTrue();
            resultPartial.Rate.Should().Be(0.025m);

            // Act - Both conditions match
            var parametersFull = new Dictionary<string, object>
            {
                ["resolution"] = "1080p",
                ["with_audio"] = true
            };
            var resultFull = _evaluator.Evaluate(config, parametersFull, usage);

            // Assert - Should use rule rate
            resultFull.UsedDefaultRate.Should().BeFalse();
            resultFull.Rate.Should().Be(0.15m);
            resultFull.Cost.Should().Be(1.50m);
        }

        #endregion

        #region Pricing Type Tests

        [Fact]
        public void Evaluate_PerUnit_UsesImageCount()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "per_unit",
                DefaultRate = 0.05m,
                UnitField = "ImageCount",
                Rules = new List<PricingRule>()
            };

            var parameters = new Dictionary<string, object>();
            var usage = new Usage { ImageCount = 5 };

            // Act
            var result = _evaluator.Evaluate(config, parameters, usage);

            // Assert
            result.Quantity.Should().Be(5m);
            result.Cost.Should().Be(0.25m); // 5 * 0.05
        }

        [Fact]
        public void Evaluate_PerStep_UsesInferenceSteps()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "per_step",
                DefaultRate = 0.00013m,
                UnitField = "InferenceSteps",
                Rules = new List<PricingRule>()
            };

            var parameters = new Dictionary<string, object>();
            var usage = new Usage { InferenceSteps = 30 };

            // Act
            var result = _evaluator.Evaluate(config, parameters, usage);

            // Assert
            result.Quantity.Should().Be(30m);
            result.Cost.Should().Be(0.0039m); // 30 * 0.00013
        }

        #endregion

        #region Case Insensitivity Tests

        [Fact]
        public void Evaluate_Conditions_AreCaseInsensitive()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "per_second",
                DefaultRate = 0.01m,
                UnitField = "VideoDurationSeconds",
                Rules = new List<PricingRule>
                {
                    new PricingRule
                    {
                        Priority = 1,
                        Description = "HD resolution",
                        Conditions = new Dictionary<string, object> { ["resolution"] = "1080P" },
                        Rate = 0.06m
                    }
                }
            };

            var parameters = new Dictionary<string, object> { ["resolution"] = "1080p" };
            var usage = new Usage { VideoDurationSeconds = 10 };

            // Act
            var result = _evaluator.Evaluate(config, parameters, usage);

            // Assert
            result.UsedDefaultRate.Should().BeFalse();
            result.Rate.Should().Be(0.06m);
        }

        #endregion
    }

    public class PricingRulesValidatorTests
    {
        private readonly Mock<ILogger<PricingRulesValidator>> _loggerMock;
        private readonly PricingRulesValidator _validator;

        public PricingRulesValidatorTests()
        {
            _loggerMock = new Mock<ILogger<PricingRulesValidator>>();
            _validator = new PricingRulesValidator(_loggerMock.Object);
        }

        #region Valid Configuration Tests

        [Fact]
        public void Validate_ValidConfig_ReturnsSuccess()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "per_second",
                DefaultRate = 0.025m,
                UnitField = "VideoDurationSeconds",
                Rules = new List<PricingRule>
                {
                    new PricingRule
                    {
                        Priority = 1,
                        Description = "1080p video",
                        Conditions = new Dictionary<string, object> { ["resolution"] = "1080p" },
                        Rate = 0.06m
                    }
                }
            };

            // Act
            var result = _validator.Validate(config);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        #endregion

        #region Invalid Configuration Tests

        [Fact]
        public void Validate_MissingPricingType_ReturnsError()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "",
                DefaultRate = 0.025m,
                UnitField = "VideoDurationSeconds"
            };

            // Act
            var result = _validator.Validate(config);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Field == "pricingType");
        }

        [Fact]
        public void Validate_InvalidPricingType_ReturnsError()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "invalid_type",
                DefaultRate = 0.025m,
                UnitField = "VideoDurationSeconds"
            };

            // Act
            var result = _validator.Validate(config);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Field == "pricingType");
        }

        [Fact]
        public void Validate_NegativeDefaultRate_ReturnsError()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "per_second",
                DefaultRate = -0.01m,
                UnitField = "VideoDurationSeconds"
            };

            // Act
            var result = _validator.Validate(config);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Field == "defaultRate");
        }

        [Fact]
        public void Validate_MissingUnitField_StillValid()
        {
            // Arrange - UnitField is optional, defaults based on pricingType
            var config = new PricingRulesConfig
            {
                PricingType = "per_second",
                DefaultRate = 0.025m,
                UnitField = ""
            };

            // Act
            var result = _validator.Validate(config);

            // Assert - Should be valid since UnitField defaults based on pricingType
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_RuleWithNegativeRate_ReturnsError()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "per_second",
                DefaultRate = 0.025m,
                UnitField = "VideoDurationSeconds",
                Rules = new List<PricingRule>
                {
                    new PricingRule
                    {
                        Priority = 1,
                        Conditions = new Dictionary<string, object> { ["resolution"] = "1080p" },
                        Rate = -0.05m
                    }
                }
            };

            // Act
            var result = _validator.Validate(config);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.RuleIndex == 0);
        }

        [Fact]
        public void Validate_RuleWithEmptyConditions_ReturnsWarning()
        {
            // Arrange
            var config = new PricingRulesConfig
            {
                PricingType = "per_second",
                DefaultRate = 0.025m,
                UnitField = "VideoDurationSeconds",
                Rules = new List<PricingRule>
                {
                    new PricingRule
                    {
                        Priority = 1,
                        Description = "Catch-all rule",
                        Conditions = new Dictionary<string, object>(),
                        Rate = 0.05m
                    }
                }
            };

            // Act
            var result = _validator.Validate(config);

            // Assert
            // Should be valid but with a warning about empty conditions
            result.IsValid.Should().BeTrue();
            result.Warnings.Should().Contain(w => w.Contains("no conditions"));
        }

        #endregion

        #region JSON Validation Tests

        [Fact]
        public void ValidateJson_ValidJson_ReturnsSuccess()
        {
            // Arrange
            var json = @"{
                ""pricingType"": ""per_second"",
                ""defaultRate"": 0.025,
                ""unitField"": ""VideoDurationSeconds"",
                ""rules"": [
                    {
                        ""priority"": 1,
                        ""description"": ""1080p video"",
                        ""conditions"": { ""resolution"": ""1080p"" },
                        ""rate"": 0.06
                    }
                ]
            }";

            // Act
            var result = _validator.ValidateJson(json);

            // Assert
            result.IsValid.Should().BeTrue();
            result.ParsedConfig.Should().NotBeNull();
        }

        [Fact]
        public void ValidateJson_InvalidJson_ReturnsError()
        {
            // Arrange
            var json = "{ invalid json }";

            // Act
            var result = _validator.ValidateJson(json);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Field == "json");
        }

        #endregion
    }
}
