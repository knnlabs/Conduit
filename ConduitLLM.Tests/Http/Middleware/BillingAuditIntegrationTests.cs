using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Middleware;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Tests.Http.Middleware
{
    /// <summary>
    /// Integration tests for billing audit event capture through the UsageTrackingMiddleware
    /// </summary>
    public class BillingAuditIntegrationTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly string _databaseName;
        private readonly IBillingAuditService _billingAuditService;
        private readonly Mock<ICostCalculationService> _mockCostService;
        private readonly Mock<IBatchSpendUpdateService> _mockBatchSpendService;
        private readonly Mock<IRequestLogService> _mockRequestLogService;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<ILogger<UsageTrackingMiddleware>> _mockLogger;
        private readonly UsageTrackingMiddleware _middleware;

        public BillingAuditIntegrationTests()
        {
            var services = new ServiceCollection();
            
            // Configure in-memory database with a consistent name for this test instance
            _databaseName = $"BillingAuditIntegrationTestDb_{Guid.NewGuid()}";
            services.AddDbContext<ConduitDbContext>(options =>
                options.UseInMemoryDatabase(databaseName: _databaseName),
                ServiceLifetime.Scoped);
            
            // Register logger
            services.AddSingleton<ILogger<BillingAuditService>>(new Mock<ILogger<BillingAuditService>>().Object);
            
            // Build the service provider
            _serviceProvider = services.BuildServiceProvider();
            
            // Initialize the database
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
                context.Database.EnsureCreated();
            }
            
            // Create BillingAuditService with the service provider
            _billingAuditService = new BillingAuditService(
                _serviceProvider, 
                _serviceProvider.GetRequiredService<ILogger<BillingAuditService>>());
            
            // Start the billing audit service
            ((BillingAuditService)_billingAuditService).StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            
            // Setup mocks
            _mockCostService = new Mock<ICostCalculationService>();
            _mockBatchSpendService = new Mock<IBatchSpendUpdateService>();
            _mockRequestLogService = new Mock<IRequestLogService>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockLogger = new Mock<ILogger<UsageTrackingMiddleware>>();
            
            // Create middleware with a next delegate that writes the response
            RequestDelegate next = async (HttpContext ctx) => 
            {
                // The middleware expects the response to be written by the next delegate
                // This simulates what happens in production
                if (ctx.Items.TryGetValue("MockResponseData", out var responseData))
                {
                    var json = JsonSerializer.Serialize(responseData);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.StatusCode = 200; // Ensure success status
                    await ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length);
                    await ctx.Response.Body.FlushAsync(); // Ensure data is written
                }
            };
            _middleware = new UsageTrackingMiddleware(next, _mockLogger.Object);
        }

        [Fact]
        public async Task Middleware_ShouldLogUsageTrackedEvent_ForSuccessfulRequest()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            context.Items["VirtualKeyId"] = 123;
            context.Items["VirtualKey"] = "test-key";
            context.Items["ProviderType"] = "OpenAI";
            
            var responseData = new
            {
                id = "chatcmpl-123",
                model = "gpt-4",
                usage = new
                {
                    prompt_tokens = 100,
                    completion_tokens = 200,
                    total_tokens = 300
                }
            };
            
            // Store response data for the next delegate to write
            context.Items["MockResponseData"] = responseData;
            
            // Replace response body with a stream we can control
            var originalBody = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;
            
            _mockCostService.Setup(x => x.CalculateCostAsync("gpt-4", It.IsAny<Usage>(), default))
                .ReturnsAsync(0.015m);
            
            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);
            
            // Act - The middleware will intercept the response as it's being written
            await _middleware.InvokeAsync(
                context, 
                _mockCostService.Object, 
                _mockBatchSpendService.Object,
                _mockRequestLogService.Object, 
                _mockVirtualKeyService.Object,
                _billingAuditService);
            
            // Give the background service time to process the event
            // The LogBillingEvent method is fire-and-forget, so we need to wait for it to be queued
            await Task.Delay(500);
            
            // Force flush any pending events
            await ((BillingAuditService)_billingAuditService).StopAsync(CancellationToken.None);
            
            // Assert
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            var auditEvent = await dbContext.BillingAuditEvents
                .FirstOrDefaultAsync(e => e.EventType == BillingAuditEventType.UsageTracked);
            
            Assert.NotNull(auditEvent);
            Assert.Equal(123, auditEvent.VirtualKeyId);
            Assert.Equal("gpt-4", auditEvent.Model);
            Assert.Equal(0.015m, auditEvent.CalculatedCost);
            Assert.Equal("OpenAI", auditEvent.ProviderType);
            Assert.NotNull(auditEvent.UsageJson);
            
            // Restore original body
            context.Response.Body = originalBody;
        }

        [Fact]
        public async Task Middleware_ShouldLogZeroCostSkippedEvent_ForZeroCost()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            context.Items["VirtualKeyId"] = 456;
            context.Items["VirtualKey"] = "test-key";
            context.Items["ProviderType"] = "OpenAI";
            
            var responseData = new
            {
                id = "chatcmpl-456",
                model = "free-model",
                usage = new
                {
                    prompt_tokens = 50,
                    completion_tokens = 50,
                    total_tokens = 100
                }
            };
            
            // Store response data for the next delegate to write
            context.Items["MockResponseData"] = responseData;
            
            // Replace response body with a stream we can control
            var originalBody = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;
            
            _mockCostService.Setup(x => x.CalculateCostAsync("free-model", It.IsAny<Usage>(), default))
                .ReturnsAsync(0m); // Zero cost
            
            // Act
            await _middleware.InvokeAsync(
                context, 
                _mockCostService.Object, 
                _mockBatchSpendService.Object,
                _mockRequestLogService.Object, 
                _mockVirtualKeyService.Object,
                _billingAuditService);
            
            // Give the background service time to process the event
            await Task.Delay(100);
            
            // Force flush
            await ((BillingAuditService)_billingAuditService).StopAsync(CancellationToken.None);
            
            // Assert
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            var auditEvent = await dbContext.BillingAuditEvents
                .FirstOrDefaultAsync(e => e.EventType == BillingAuditEventType.ZeroCostSkipped);
            
            Assert.NotNull(auditEvent);
            Assert.Equal(456, auditEvent.VirtualKeyId);
            Assert.Equal("free-model", auditEvent.Model);
            Assert.Equal(0m, auditEvent.CalculatedCost);
            
            // Restore original body
            context.Response.Body = originalBody;
        }

        [Fact]
        public async Task Middleware_ShouldLogMissingUsageDataEvent_WhenNoUsageInResponse()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            context.Items["VirtualKeyId"] = 789;
            context.Items["VirtualKey"] = "test-key";
            context.Items["ProviderType"] = "CustomProvider";
            
            var responseData = new
            {
                id = "response-789",
                model = "custom-model",
                // No usage field
                choices = new[] { new { text = "response text" } }
            };
            
            // Store response data for the next delegate to write
            context.Items["MockResponseData"] = responseData;
            
            // Replace response body with a stream we can control
            var originalBody = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;
            
            // Act
            await _middleware.InvokeAsync(
                context, 
                _mockCostService.Object, 
                _mockBatchSpendService.Object,
                _mockRequestLogService.Object, 
                _mockVirtualKeyService.Object,
                _billingAuditService);
            
            // Give the background service time to process the event
            await Task.Delay(100);
            
            // Force flush
            await ((BillingAuditService)_billingAuditService).StopAsync(CancellationToken.None);
            
            // Assert
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            var auditEvent = await dbContext.BillingAuditEvents
                .FirstOrDefaultAsync(e => e.EventType == BillingAuditEventType.MissingUsageData);
            
            Assert.NotNull(auditEvent);
            Assert.Equal(789, auditEvent.VirtualKeyId);
            Assert.Equal("CustomProvider", auditEvent.ProviderType);
            
            // Restore original body
            context.Response.Body = originalBody;
        }

        [Fact]
        public async Task Middleware_ShouldLogErrorResponseSkippedEvent_For4xxErrors()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            context.Items["VirtualKeyId"] = 111;
            context.Items["VirtualKey"] = "test-key";
            context.Response.StatusCode = 400; // Bad request
            
            // Act
            await _middleware.InvokeAsync(
                context, 
                _mockCostService.Object, 
                _mockBatchSpendService.Object,
                _mockRequestLogService.Object, 
                _mockVirtualKeyService.Object,
                _billingAuditService);
            
            // Force flush
            await ((BillingAuditService)_billingAuditService).StopAsync(default);
            
            // Assert
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            var auditEvent = await dbContext.BillingAuditEvents
                .FirstOrDefaultAsync(e => e.EventType == BillingAuditEventType.ErrorResponseSkipped);
            
            Assert.NotNull(auditEvent);
            Assert.Equal(111, auditEvent.VirtualKeyId);
            Assert.Equal(400, auditEvent.HttpStatusCode);
            Assert.Contains("HTTP 400", auditEvent.FailureReason!);
        }

        [Fact]
        public async Task Middleware_ShouldLogNoVirtualKeyEvent_WhenKeyMissing()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            // No VirtualKeyId in context.Items
            
            // Act
            await _middleware.InvokeAsync(
                context, 
                _mockCostService.Object, 
                _mockBatchSpendService.Object,
                _mockRequestLogService.Object, 
                _mockVirtualKeyService.Object,
                _billingAuditService);
            
            // Force flush
            await ((BillingAuditService)_billingAuditService).StopAsync(default);
            
            // Assert
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            var auditEvent = await dbContext.BillingAuditEvents
                .FirstOrDefaultAsync(e => e.EventType == BillingAuditEventType.NoVirtualKey);
            
            Assert.NotNull(auditEvent);
            Assert.Null(auditEvent.VirtualKeyId);
            Assert.Contains("No virtual key", auditEvent.FailureReason!);
        }

        [Fact]
        public async Task Middleware_ShouldLogStreamingUsageMissingEvent_ForStreamingWithoutUsage()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            context.Items["VirtualKeyId"] = 222;
            context.Items["VirtualKey"] = "test-key";
            context.Items["IsStreamingRequest"] = true;
            context.Items["ProviderType"] = "OpenAI";
            context.Response.ContentType = "text/event-stream";
            // No StreamingUsage in context
            
            // Act
            await _middleware.InvokeAsync(
                context, 
                _mockCostService.Object, 
                _mockBatchSpendService.Object,
                _mockRequestLogService.Object, 
                _mockVirtualKeyService.Object,
                _billingAuditService);
            
            // Force flush
            await ((BillingAuditService)_billingAuditService).StopAsync(default);
            
            // Assert
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            var auditEvent = await dbContext.BillingAuditEvents
                .FirstOrDefaultAsync(e => e.EventType == BillingAuditEventType.StreamingUsageMissing);
            
            Assert.NotNull(auditEvent);
            Assert.Equal(222, auditEvent.VirtualKeyId);
            Assert.Contains("No StreamingUsage", auditEvent.FailureReason!);
        }

        [Fact]
        public async Task Middleware_ShouldLogJsonParseErrorEvent_ForInvalidJson()
        {
            // Arrange
            var context = CreateHttpContext("/v1/chat/completions");
            context.Items["VirtualKeyId"] = 333;
            context.Items["VirtualKey"] = "test-key";
            context.Items["ProviderType"] = "OpenAI";
            
            // Setup invalid JSON response
            var invalidJson = "{ invalid json }";
            var bytes = Encoding.UTF8.GetBytes(invalidJson);
            context.Response.Body = new MemoryStream(bytes);
            context.Response.StatusCode = 200;
            
            // Act
            await _middleware.InvokeAsync(
                context, 
                _mockCostService.Object, 
                _mockBatchSpendService.Object,
                _mockRequestLogService.Object, 
                _mockVirtualKeyService.Object,
                _billingAuditService);
            
            // Force flush
            await ((BillingAuditService)_billingAuditService).StopAsync(default);
            
            // Assert
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            var auditEvent = await dbContext.BillingAuditEvents
                .FirstOrDefaultAsync(e => e.EventType == BillingAuditEventType.JsonParseError);
            
            Assert.NotNull(auditEvent);
            Assert.Equal(333, auditEvent.VirtualKeyId);
            Assert.NotNull(auditEvent.FailureReason);
        }

        [Fact]
        public async Task Middleware_ShouldPreserveAllContextData_InAuditEvents()
        {
            // Arrange
            var context = CreateHttpContext("/v1/embeddings");
            context.Items["VirtualKeyId"] = 444;
            context.Items["VirtualKey"] = "test-key-444";
            context.Items["ProviderType"] = "Azure";
            context.TraceIdentifier = "trace-444";
            
            var responseData = new
            {
                model = "text-embedding-ada-002",
                usage = new
                {
                    prompt_tokens = 50,
                    total_tokens = 50
                }
            };
            
            // Store response data for the next delegate to write
            context.Items["MockResponseData"] = responseData;
            
            // Replace response body with a stream we can control
            var originalBody = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;
            
            _mockCostService.Setup(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), default))
                .ReturnsAsync(0.001m);
            
            _mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);
            
            // Act
            await _middleware.InvokeAsync(
                context, 
                _mockCostService.Object, 
                _mockBatchSpendService.Object,
                _mockRequestLogService.Object, 
                _mockVirtualKeyService.Object,
                _billingAuditService);
            
            // Give the background service time to process the event
            await Task.Delay(100);
            
            // Force flush
            await ((BillingAuditService)_billingAuditService).StopAsync(CancellationToken.None);
            
            // Assert
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
            var auditEvent = await dbContext.BillingAuditEvents
                .FirstOrDefaultAsync(e => e.VirtualKeyId == 444);
            
            Assert.NotNull(auditEvent);
            Assert.Equal("trace-444", auditEvent.RequestId);
            Assert.Equal("/v1/embeddings", auditEvent.RequestPath);
            Assert.Equal("Azure", auditEvent.ProviderType);
            Assert.Equal(200, auditEvent.HttpStatusCode);
            Assert.Equal("text-embedding-ada-002", auditEvent.Model);
            Assert.False(auditEvent.IsEstimated);
            
            // Restore original body
            context.Response.Body = originalBody;
        }

        private HttpContext CreateHttpContext(string path)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Request.Method = "POST";
            context.Response.Body = new MemoryStream();
            context.TraceIdentifier = Guid.NewGuid().ToString();
            
            // Set default status code
            context.Response.StatusCode = 200;
            
            return context;
        }

        public void Dispose()
        {
            // Stop and flush the service before disposing
            if (_billingAuditService is BillingAuditService billingService)
            {
                billingService.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            (_billingAuditService as IDisposable)?.Dispose();
            _serviceProvider?.Dispose();
        }
    }
}