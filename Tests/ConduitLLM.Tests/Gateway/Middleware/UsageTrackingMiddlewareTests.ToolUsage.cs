using System.Text.Json;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Tests.Http.Middleware.Builders;
using ConduitLLM.Tests.Http.Middleware.Assertions;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http.Middleware
{
    /// <summary>
    /// Tests for UsageTrackingMiddleware tool usage tracking.
    /// These tests use a real in-memory database for tool cost configuration.
    /// </summary>
    public partial class UsageTrackingMiddlewareTests
    {
        [Fact]
        public async Task ProcessResponseAsync_WithToolUsage_PersistsToolDataToBillingAudit()
        {
            // Arrange
            await Fixture.AddToolConfigurationAsync(ProviderType.Groq, "code_interpreter", 0.03m);

            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(123)
                .AsGroq()
                .WithTraceId("test-request-id")
                .WithTestResponseBody(CreateGroqResponseWithToolUsage("code_interpreter", 3))
                .Build();

            Fixture.SetupDefaultCost(0.10m); // Base token cost

            // Act
            await Invoker
                .WithTestResponseBodyDelegate()
                .InvokeWithRealToolServiceAsync(context);

            // Assert
            var billingEvent = UsageTrackingAssertions.VerifySingleBillingEvent(Fixture.CapturedBillingEvents);
            Assert.Equal(BillingAuditEventType.ToolUsageTracked, billingEvent.EventType);
            Assert.NotNull(billingEvent.ToolUsageJson);
            Assert.Contains("code_interpreter", billingEvent.ToolUsageJson);
            Assert.Equal(0.09m, billingEvent.ToolUsageCost); // 3 * 0.03
            Assert.Equal(0.19m, billingEvent.CalculatedCost); // 0.10 (tokens) + 0.09 (tools)
        }

        [Fact]
        public async Task ProcessResponseAsync_WithMultipleTools_CalculatesCombinedCost()
        {
            // Arrange
            await Fixture.AddToolConfigurationAsync(ProviderType.Groq, "code_interpreter", 0.03m);
            await Fixture.AddToolConfigurationAsync(ProviderType.Groq, "browser_search", 0.04m);

            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(123)
                .AsGroq()
                .WithTestResponseBody(CreateGroqResponseWithMultipleTools())
                .Build();

            Fixture.SetupDefaultCost(0.15m);

            // Act
            await Invoker
                .WithTestResponseBodyDelegate()
                .InvokeWithRealToolServiceAsync(context);

            // Assert
            var billingEvent = UsageTrackingAssertions.VerifySingleBillingEvent(Fixture.CapturedBillingEvents);
            Assert.Equal(BillingAuditEventType.ToolUsageTracked, billingEvent.EventType);
            Assert.NotNull(billingEvent.ToolUsageJson);
            Assert.Contains("code_interpreter", billingEvent.ToolUsageJson);
            Assert.Contains("browser_search", billingEvent.ToolUsageJson);
            Assert.Equal(0.14m, billingEvent.ToolUsageCost); // (2 * 0.03) + (2 * 0.04)
            Assert.Equal(0.29m, billingEvent.CalculatedCost); // 0.15 + 0.14
        }

        [Fact]
        public async Task ProcessResponseAsync_WithMissingToolConfig_LogsWarningEvent()
        {
            // Arrange - No tool configuration in database
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(123)
                .AsGroq()
                .WithTestResponseBody(CreateGroqResponseWithToolUsage("code_interpreter", 3))
                .Build();

            Fixture.SetupDefaultCost(0m); // Zero base cost to trigger zero cost path

            // Act
            await Invoker
                .WithTestResponseBodyDelegate()
                .InvokeWithRealToolServiceAsync(context);

            // Assert
            var billingEvent = UsageTrackingAssertions.VerifySingleBillingEvent(Fixture.CapturedBillingEvents);
            Assert.Equal(BillingAuditEventType.ToolUsageMissingCostConfig, billingEvent.EventType);
            Assert.NotNull(billingEvent.ToolUsageJson);
            Assert.Contains("code_interpreter", billingEvent.ToolUsageJson);
            Assert.Equal(0m, billingEvent.ToolUsageCost);
            Assert.Contains("Tool usage detected but no cost configuration found", billingEvent.FailureReason);
        }

        [Fact]
        public async Task ProcessResponseAsync_WithoutToolUsage_DoesNotSetToolFields()
        {
            // Arrange
            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(123)
                .AsGroq()
                .WithTestResponseBody(CreateGroqResponseWithoutToolUsage())
                .Build();

            Fixture.SetupDefaultCost(0.10m);

            // Act
            await Invoker
                .WithTestResponseBodyDelegate()
                .InvokeWithRealToolServiceAsync(context);

            // Assert
            var billingEvent = UsageTrackingAssertions.VerifySingleBillingEvent(Fixture.CapturedBillingEvents);
            Assert.Equal(BillingAuditEventType.UsageTracked, billingEvent.EventType); // Regular usage, not tool usage
            Assert.Null(billingEvent.ToolUsageJson);
            Assert.Null(billingEvent.ToolUsageCost);
            Assert.Equal(0.10m, billingEvent.CalculatedCost);
        }

        [Fact]
        public async Task TrackStreamingUsageAsync_WithToolUsage_PersistsToolData()
        {
            // Arrange
            await Fixture.AddToolConfigurationAsync(ProviderType.Groq, "code_interpreter", 0.03m);

            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = "code_interpreter", Count = 2 }
                }
            };

            var streamingUsage = new Usage
            {
                PromptTokens = 100,
                CompletionTokens = 50
            };

            var context = new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(123)
                .AsGroq()
                .AsStreaming(streamingUsage, "llama-3.1-70b-versatile")
                .WithStreamingToolUsage(toolUsage)
                .WithTraceId("test-stream-request-id")
                .Build();

            Fixture.SetupDefaultCost(0.08m);

            // Act
            await Invoker
                .AsStreamingResponse()
                .InvokeWithRealToolServiceAsync(context);

            // Assert
            var billingEvent = UsageTrackingAssertions.VerifySingleBillingEvent(Fixture.CapturedBillingEvents);
            Assert.Equal(BillingAuditEventType.ToolUsageTracked, billingEvent.EventType);
            Assert.NotNull(billingEvent.ToolUsageJson);
            Assert.Contains("code_interpreter", billingEvent.ToolUsageJson);
            Assert.Equal(0.06m, billingEvent.ToolUsageCost); // 2 * 0.03
            Assert.Equal(0.14m, billingEvent.CalculatedCost); // 0.08 + 0.06
        }

        #region Helper Methods for Tool Usage Tests

        private static string CreateGroqResponseWithToolUsage(string toolName, int count)
        {
            var response = new
            {
                id = "chatcmpl-123",
                model = "llama-3.1-70b-versatile",
                usage = new
                {
                    prompt_tokens = 100,
                    completion_tokens = 50,
                    total_tokens = 150
                },
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = "Here's the result from the code interpreter...",
                            tool_calls = new[]
                            {
                                new
                                {
                                    id = "call_123",
                                    type = "function",
                                    function = new
                                    {
                                        name = toolName,
                                        arguments = "{\"code\": \"print('hello')\"}"
                                    }
                                }
                            }
                        }
                    }
                },
                x_groq = new Dictionary<string, object>
                {
                    ["usage"] = new Dictionary<string, int> { [toolName] = count }
                }
            };

            return JsonSerializer.Serialize(response);
        }

        private static string CreateGroqResponseWithMultipleTools()
        {
            var response = new
            {
                id = "chatcmpl-456",
                model = "llama-3.1-70b-versatile",
                usage = new
                {
                    prompt_tokens = 150,
                    completion_tokens = 75,
                    total_tokens = 225
                },
                x_groq = new Dictionary<string, object>
                {
                    ["usage"] = new Dictionary<string, int>
                    {
                        ["code_interpreter"] = 2,
                        ["browser_search"] = 2
                    }
                }
            };

            return JsonSerializer.Serialize(response);
        }

        private static string CreateGroqResponseWithoutToolUsage()
        {
            var response = new
            {
                id = "chatcmpl-789",
                model = "llama-3.1-70b-versatile",
                usage = new
                {
                    prompt_tokens = 100,
                    completion_tokens = 50,
                    total_tokens = 150
                },
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = "Regular response without tools"
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(response);
        }

        #endregion
    }
}
