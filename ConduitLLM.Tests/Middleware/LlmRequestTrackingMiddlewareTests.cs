using System.IO;
using System.Text;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Http.Middleware;

using Microsoft.AspNetCore.Http;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Middleware
{
    public class LlmRequestTrackingMiddlewareTests
    {
        [Fact]
        public async Task Middleware_TracksRequestCorrectly()
        {
            // Arrange
            var mockRequestLogService = new Mock<IRequestLogService>();
            var mockNextMiddleware = new RequestDelegate(_ => Task.CompletedTask);
            
            var middleware = new LlmRequestTrackingMiddleware(
                mockNextMiddleware,
                mockRequestLogService.Object);
                
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/chat";
            context.Request.Method = "POST";
            context.Request.Headers["X-Virtual-Key"] = "vk_test123";
            
            var requestBody = "{\"model\":\"gpt-4\",\"messages\":[{\"role\":\"user\",\"content\":\"Hello\"}]}";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
            context.Request.ContentLength = requestBody.Length;
            context.Request.ContentType = "application/json";
            
            var originalResponseBody = context.Response.Body;
            using var replacementResponseBody = new MemoryStream();
            context.Response.Body = replacementResponseBody;
            
            // Mock service to setup expected behavior
            mockRequestLogService
                .Setup(s => s.GetVirtualKeyIdFromKeyValueAsync("vk_test123"))
                .ReturnsAsync(1);
                
            mockRequestLogService
                .Setup(s => s.EstimateTokens(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((150, 30)); // Mocked input and output token counts
                
            mockRequestLogService
                .Setup(s => s.CalculateCost("gpt-4", 150, 30))
                .Returns(0.0025m);
                
            // Act
            await middleware.InvokeAsync(context);
            
            // Assert
            mockRequestLogService.Verify(
                s => s.LogRequestAsync(It.Is<LogRequestDto>(dto =>
                    dto.VirtualKeyId == 1 &&
                    dto.ModelName == "gpt-4" &&
                    dto.RequestType == "chat" &&
                    dto.InputTokens == 150 &&
                    dto.OutputTokens == 30 &&
                    dto.Cost == 0.0025m &&
                    dto.StatusCode == 200
                )),
                Times.Once);
        }
    }
}
