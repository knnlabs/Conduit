using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Middleware;
using ConduitLLM.Http.Services;
using ConduitLLM.Core.Models;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Tests.Http.Middleware
{
    public class UsageTrackingMiddlewareToolUsageTests : IDisposable
    {
        private readonly ConduitDbContext _context;
        private readonly Mock<ILogger<UsageTrackingMiddleware>> _loggerMock;
        private readonly Mock<ILogger<ToolCostCalculationService>> _toolLoggerMock;
        private readonly Mock<ICostCalculationService> _costCalculationServiceMock;
        private readonly Mock<IBatchSpendUpdateService> _batchSpendServiceMock;
        private readonly Mock<IRequestLogService> _requestLogServiceMock;
        private readonly Mock<IVirtualKeyService> _virtualKeyServiceMock;
        private readonly Mock<IBillingAuditService> _billingAuditServiceMock;
        private readonly ToolCostCalculationService _toolCostCalculationService;
        private readonly UsageTrackingMiddleware _middleware;
        private readonly List<BillingAuditEvent> _capturedBillingEvents;

        public UsageTrackingMiddlewareToolUsageTests()
        {
            var options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;

            _context = new ConduitDbContext(options);
            _loggerMock = new Mock<ILogger<UsageTrackingMiddleware>>();
            _toolLoggerMock = new Mock<ILogger<ToolCostCalculationService>>();
            _costCalculationServiceMock = new Mock<ICostCalculationService>();
            _batchSpendServiceMock = new Mock<IBatchSpendUpdateService>();
            _requestLogServiceMock = new Mock<IRequestLogService>();
            _virtualKeyServiceMock = new Mock<IVirtualKeyService>();
            _billingAuditServiceMock = new Mock<IBillingAuditService>();
            _capturedBillingEvents = new List<BillingAuditEvent>();

            _toolCostCalculationService = new ToolCostCalculationService(_context, _toolLoggerMock.Object);

            // Capture billing events
            _billingAuditServiceMock
                .Setup(x => x.LogBillingEvent(It.IsAny<BillingAuditEvent>()))
                .Callback<BillingAuditEvent>(evt => _capturedBillingEvents.Add(evt));

            _middleware = new UsageTrackingMiddleware(
                next: (innerHttpContext) => Task.CompletedTask,
                _loggerMock.Object);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        [Fact]
        public async Task ProcessResponseAsync_WithToolUsage_PersistsToolDataToBillingAudit()
        {
            // Arrange
            await SetupToolConfiguration();
            var context = CreateHttpContext();
            var responseBody = CreateResponseWithToolUsage();

            // Setup mocks
            _costCalculationServiceMock
                .Setup(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), default))
                .ReturnsAsync(0.10m); // Base token cost

            // Act
            await _middleware.InvokeAsync(
                context,
                _costCalculationServiceMock.Object,
                _batchSpendServiceMock.Object,
                _requestLogServiceMock.Object,
                _virtualKeyServiceMock.Object,
                _billingAuditServiceMock.Object,
                _toolCostCalculationService);

            // Assert
            Assert.Single(_capturedBillingEvents);
            var billingEvent = _capturedBillingEvents[0];
            
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
            await SetupMultipleToolConfiguration();
            var context = CreateHttpContext();
            var responseBody = CreateResponseWithMultipleTools();

            _costCalculationServiceMock
                .Setup(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), default))
                .ReturnsAsync(0.15m);

            // Act
            await _middleware.InvokeAsync(
                context,
                _costCalculationServiceMock.Object,
                _batchSpendServiceMock.Object,
                _requestLogServiceMock.Object,
                _virtualKeyServiceMock.Object,
                _billingAuditServiceMock.Object,
                _toolCostCalculationService);

            // Assert
            Assert.Single(_capturedBillingEvents);
            var billingEvent = _capturedBillingEvents[0];
            
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
            var context = CreateHttpContext();
            var responseBody = CreateResponseWithToolUsage();

            _costCalculationServiceMock
                .Setup(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), default))
                .ReturnsAsync(0m); // Zero base cost to trigger zero cost path

            // Act
            await _middleware.InvokeAsync(
                context,
                _costCalculationServiceMock.Object,
                _batchSpendServiceMock.Object,
                _requestLogServiceMock.Object,
                _virtualKeyServiceMock.Object,
                _billingAuditServiceMock.Object,
                _toolCostCalculationService);

            // Assert
            Assert.Single(_capturedBillingEvents);
            var billingEvent = _capturedBillingEvents[0];
            
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
            var context = CreateHttpContext();
            var responseBody = CreateResponseWithoutToolUsage();

            _costCalculationServiceMock
                .Setup(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), default))
                .ReturnsAsync(0.10m);

            // Act
            await _middleware.InvokeAsync(
                context,
                _costCalculationServiceMock.Object,
                _batchSpendServiceMock.Object,
                _requestLogServiceMock.Object,
                _virtualKeyServiceMock.Object,
                _billingAuditServiceMock.Object,
                _toolCostCalculationService);

            // Assert
            Assert.Single(_capturedBillingEvents);
            var billingEvent = _capturedBillingEvents[0];
            
            Assert.Equal(BillingAuditEventType.UsageTracked, billingEvent.EventType); // Regular usage, not tool usage
            Assert.Null(billingEvent.ToolUsageJson);
            Assert.Null(billingEvent.ToolUsageCost);
            Assert.Equal(0.10m, billingEvent.CalculatedCost);
        }

        [Fact]
        public async Task TrackStreamingUsageAsync_WithToolUsage_PersistsToolData()
        {
            // Arrange
            await SetupToolConfiguration();
            var context = CreateStreamingHttpContext();
            
            // Add streaming tool usage to context
            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = "code_interpreter", Count = 2 }
                }
            };
            context.Items["StreamingToolUsage"] = toolUsage;

            _costCalculationServiceMock
                .Setup(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), default))
                .ReturnsAsync(0.08m);

            // Create middleware with streaming response
            var middleware = new UsageTrackingMiddleware(
                next: async (innerHttpContext) => 
                {
                    innerHttpContext.Response.ContentType = "text/event-stream";
                    await Task.CompletedTask;
                },
                _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(
                context,
                _costCalculationServiceMock.Object,
                _batchSpendServiceMock.Object,
                _requestLogServiceMock.Object,
                _virtualKeyServiceMock.Object,
                _billingAuditServiceMock.Object,
                _toolCostCalculationService);

            // Assert
            Assert.Single(_capturedBillingEvents);
            var billingEvent = _capturedBillingEvents[0];
            
            Assert.Equal(BillingAuditEventType.ToolUsageTracked, billingEvent.EventType);
            Assert.NotNull(billingEvent.ToolUsageJson);
            Assert.Contains("code_interpreter", billingEvent.ToolUsageJson);
            Assert.Equal(0.06m, billingEvent.ToolUsageCost); // 2 * 0.03
            Assert.Equal(0.14m, billingEvent.CalculatedCost); // 0.08 + 0.06
        }

        private async Task SetupToolConfiguration()
        {
            _context.ProviderTools.Add(new ProviderTool
            {
                Provider = ProviderType.Groq,
                ToolName = "code_interpreter",
                CostPerUnit = 0.03m,
                BillingUnit = "requests",
                IsActive = true
            });
            await _context.SaveChangesAsync();
        }

        private async Task SetupMultipleToolConfiguration()
        {
            _context.ProviderTools.AddRange(
                new ProviderTool
                {
                    Provider = ProviderType.Groq,
                    ToolName = "code_interpreter",
                    CostPerUnit = 0.03m,
                    BillingUnit = "requests",
                    IsActive = true
                },
                new ProviderTool
                {
                    Provider = ProviderType.Groq,
                    ToolName = "browser_search",
                    CostPerUnit = 0.04m,
                    BillingUnit = "requests",
                    IsActive = true
                }
            );
            await _context.SaveChangesAsync();
        }

        private HttpContext CreateHttpContext()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/v1/chat/completions";
            context.Request.Method = "POST";
            context.Response.StatusCode = 200;
            context.Items["VirtualKeyId"] = 123;
            context.Items["ProviderType"] = "Groq";
            context.TraceIdentifier = "test-request-id";
            
            // Setup response body
            var responseBody = CreateResponseWithToolUsage();
            context.Response.Body = new MemoryStream();
            var writer = new StreamWriter(context.Response.Body);
            writer.Write(responseBody);
            writer.Flush();
            context.Response.Body.Position = 0;
            
            return context;
        }

        private HttpContext CreateStreamingHttpContext()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/v1/chat/completions";
            context.Request.Method = "POST";
            context.Response.StatusCode = 200;
            context.Items["VirtualKeyId"] = 123;
            context.Items["ProviderType"] = "Groq";
            context.Items["StreamingUsage"] = new Usage 
            { 
                PromptTokens = 100, 
                CompletionTokens = 50 
            };
            context.Items["StreamingModel"] = "llama-3.1-70b-versatile";
            context.TraceIdentifier = "test-stream-request-id";
            
            return context;
        }

        private string CreateResponseWithToolUsage()
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
                                        name = "code_interpreter",
                                        arguments = "{\"code\": \"print('hello')\"}"
                                    }
                                }
                            }
                        }
                    }
                },
                // Groq-specific tool usage tracking
                x_groq = new
                {
                    usage = new
                    {
                        code_interpreter = 3
                    }
                }
            };

            return JsonSerializer.Serialize(response);
        }

        private string CreateResponseWithMultipleTools()
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
                x_groq = new
                {
                    usage = new
                    {
                        code_interpreter = 2,
                        browser_search = 2
                    }
                }
            };

            return JsonSerializer.Serialize(response);
        }

        private string CreateResponseWithoutToolUsage()
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
    }
}