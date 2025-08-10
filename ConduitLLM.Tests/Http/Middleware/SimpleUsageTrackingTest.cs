using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Http.Middleware;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Services;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Tests.Http.Middleware
{
    public class SimpleUsageTrackingTest
    {
        [Fact]
        public async Task Test_Middleware_Gets_Called()
        {
            // Arrange
            var wasCalled = false;
            RequestDelegate next = (HttpContext ctx) => 
            {
                wasCalled = true;
                return Task.CompletedTask;
            };
            
            var mockLogger = new Mock<ILogger<UsageTrackingMiddleware>>();
            var middleware = new UsageTrackingMiddleware(next, mockLogger.Object);
            
            var context = new DefaultHttpContext();
            context.Request.Path = "/v1/chat/completions";
            context.Response.StatusCode = 200;
            context.Response.Body = new MemoryStream();
            context.Items["VirtualKeyId"] = 123;
            context.Items["VirtualKey"] = "test-key";
            
            var mockCostService = new Mock<ICostCalculationService>();
            var mockBatchSpendService = new Mock<IBatchSpendUpdateService>();
            var mockRequestLogService = new Mock<IRequestLogService>();
            var mockVirtualKeyService = new Mock<IVirtualKeyService>();
            
            // Act
            await middleware.InvokeAsync(context, mockCostService.Object, mockBatchSpendService.Object, 
                mockRequestLogService.Object, mockVirtualKeyService.Object);
            
            // Assert
            Assert.True(wasCalled, "The next delegate should have been called");
        }
        
        [Fact]
        public async Task Test_Response_Body_Interception()
        {
            // Arrange
            var responseData = new
            {
                id = "test",
                model = "gpt-4",
                usage = new
                {
                    prompt_tokens = 10,
                    completion_tokens = 20,
                    total_tokens = 30
                }
            };
            
            var json = JsonSerializer.Serialize(responseData);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            RequestDelegate next = async (HttpContext ctx) => 
            {
                // Write response
                await ctx.Response.Body.WriteAsync(bytes);
                ctx.Response.ContentType = "application/json";
                ctx.Response.StatusCode = 200;
            };
            
            var mockLogger = new Mock<ILogger<UsageTrackingMiddleware>>();
            var middleware = new UsageTrackingMiddleware(next, mockLogger.Object);
            
            var context = new DefaultHttpContext();
            context.Request.Path = "/v1/chat/completions";
            context.Response.Body = new MemoryStream();
            context.Items["VirtualKeyId"] = 123;
            context.Items["VirtualKey"] = "test-key";
            
            var mockCostService = new Mock<ICostCalculationService>();
            mockCostService.Setup(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), default))
                .ReturnsAsync(0.001m);
                
            var mockBatchSpendService = new Mock<IBatchSpendUpdateService>();
            mockBatchSpendService.SetupGet(x => x.IsHealthy).Returns(true);
            
            var mockRequestLogService = new Mock<IRequestLogService>();
            var mockVirtualKeyService = new Mock<IVirtualKeyService>();
            
            // Act
            await middleware.InvokeAsync(context, mockCostService.Object, mockBatchSpendService.Object, 
                mockRequestLogService.Object, mockVirtualKeyService.Object);
            
            // Assert
            mockCostService.Verify(x => x.CalculateCostAsync("gpt-4", It.IsAny<Usage>(), default), Times.Once);
        }
    }
}