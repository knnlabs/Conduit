using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Http.Middleware;
using ConduitLLM.Core.Interfaces;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Http.Middleware
{
    public partial class UsageTrackingMiddlewareTests
    {
        private readonly Mock<ICostCalculationService> _mockCostService;
        private readonly Mock<IBatchSpendUpdateService> _mockBatchSpendService;
        private readonly Mock<IRequestLogService> _mockRequestLogService;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<ILogger<UsageTrackingMiddleware>> _mockLogger;
        private readonly UsageTrackingMiddleware _middleware;
        private RequestDelegate _next;

        public UsageTrackingMiddlewareTests()
        {
            _mockCostService = new Mock<ICostCalculationService>();
            _mockBatchSpendService = new Mock<IBatchSpendUpdateService>();
            _mockRequestLogService = new Mock<IRequestLogService>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockLogger = new Mock<ILogger<UsageTrackingMiddleware>>();
            
            // Default _next delegate - will be replaced by SetupMockResponse
            _next = (HttpContext ctx) => Task.CompletedTask;
            _middleware = new UsageTrackingMiddleware(_next, _mockLogger.Object);
        }

        private HttpContext CreateHttpContext(string path, int statusCode = 200)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Response.StatusCode = statusCode;
            context.Response.Body = new MemoryStream();
            return context;
        }

        private void SetupMockResponse(HttpContext context, object responseData)
        {
            var json = JsonSerializer.Serialize(responseData);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            // Replace the _next delegate to write the response
            _next = async (HttpContext ctx) => 
            {
                // Set response properties first
                ctx.Response.ContentType = "application/json";
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentLength = bytes.Length;
                
                // Write the response data to the response body
                await ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length);
            };
        }
    }
}