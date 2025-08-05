using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Authorization;

namespace ConduitLLM.Http.Tests.Authorization
{
    /// <summary>
    /// Unit tests for RequireBalanceAttribute authorization filter
    /// </summary>
    public class RequireBalanceAttributeTests : TestBase
    {
        private readonly Mock<IVirtualKeyService> _virtualKeyServiceMock;
        private readonly Mock<ILogger<RequireBalanceAttribute>> _loggerMock;
        private readonly RequireBalanceAttribute _attribute;

        public RequireBalanceAttributeTests(ITestOutputHelper output) : base(output)
        {
            _virtualKeyServiceMock = new Mock<IVirtualKeyService>();
            _loggerMock = CreateLogger<RequireBalanceAttribute>();
            _attribute = new RequireBalanceAttribute();
        }

        [Fact]
        public async Task OnAuthorizationAsync_WithSufficientBalance_AllowsRequest()
        {
            // Arrange
            var virtualKey = CreateVirtualKeyWithBalance(100.00m);
            var context = CreateAuthorizationFilterContext("test-key-123");
            
            _virtualKeyServiceMock.Setup(s => s.ValidateVirtualKeyAsync("test-key-123", null))
                .ReturnsAsync(virtualKey);

            // Act
            await _attribute.OnAuthorizationAsync(context);

            // Assert
            Assert.Null(context.Result); // No result means authorization passed
            _virtualKeyServiceMock.Verify(s => s.ValidateVirtualKeyAsync("test-key-123", null), Times.Once);
        }

        [Fact]
        public async Task OnAuthorizationAsync_WithInsufficientBalance_Returns402PaymentRequired()
        {
            // Arrange
            var context = CreateAuthorizationFilterContext("test-key-zero-balance");
            
            // Mock service returns null for insufficient balance (this is what ValidateVirtualKeyAsync does for $0.00 balance)
            _virtualKeyServiceMock.Setup(s => s.ValidateVirtualKeyAsync("test-key-zero-balance", null))
                .ReturnsAsync((VirtualKey?)null);

            // Act
            await _attribute.OnAuthorizationAsync(context);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(StatusCodes.Status402PaymentRequired, objectResult.StatusCode);
            
            // Check response body
            var responseBody = objectResult.Value;
            Assert.NotNull(responseBody);
            
            // Use reflection to check anonymous object properties
            var errorProperty = responseBody.GetType().GetProperty("error");
            var statusCodeProperty = responseBody.GetType().GetProperty("statusCode");
            
            Assert.NotNull(errorProperty);
            Assert.NotNull(statusCodeProperty);
            Assert.Equal("Insufficient balance", errorProperty.GetValue(responseBody));
            Assert.Equal(402, statusCodeProperty.GetValue(responseBody));
            
            _virtualKeyServiceMock.Verify(s => s.ValidateVirtualKeyAsync("test-key-zero-balance", null), Times.Once);
        }

        [Fact]
        public async Task OnAuthorizationAsync_WithoutVirtualKeyClaim_Returns401Unauthorized()
        {
            // Arrange
            var context = CreateAuthorizationFilterContextWithoutClaim();

            // Act
            await _attribute.OnAuthorizationAsync(context);

            // Assert
            var objectResult = Assert.IsType<UnauthorizedObjectResult>(context.Result);
            Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
            
            // Verify service was never called
            _virtualKeyServiceMock.Verify(s => s.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task OnAuthorizationAsync_WithInvalidVirtualKey_Returns402PaymentRequired()
        {
            // Arrange
            var context = CreateAuthorizationFilterContext("invalid-key");
            
            // Mock service returns null for invalid key
            _virtualKeyServiceMock.Setup(s => s.ValidateVirtualKeyAsync("invalid-key", null))
                .ReturnsAsync((VirtualKey?)null);

            // Act
            await _attribute.OnAuthorizationAsync(context);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(StatusCodes.Status402PaymentRequired, objectResult.StatusCode);
            
            _virtualKeyServiceMock.Verify(s => s.ValidateVirtualKeyAsync("invalid-key", null), Times.Once);
        }

        [Fact]
        public async Task OnAuthorizationAsync_WithServiceException_Returns500InternalServerError()
        {
            // Arrange
            var context = CreateAuthorizationFilterContext("test-key-exception");
            
            _virtualKeyServiceMock.Setup(s => s.ValidateVirtualKeyAsync("test-key-exception", null))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            await _attribute.OnAuthorizationAsync(context);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
            
            var responseBody = objectResult.Value;
            Assert.NotNull(responseBody);
            
            var errorProperty = responseBody.GetType().GetProperty("error");
            Assert.NotNull(errorProperty);
            Assert.Equal("Authorization error", errorProperty.GetValue(responseBody));
        }

        [Fact]
        public async Task OnAuthorizationAsync_WithModelParameter_PassesModelToService()
        {
            // Arrange
            var virtualKey = CreateVirtualKeyWithBalance(50.00m);
            var context = CreateAuthorizationFilterContext("test-key-model", "gpt-4");
            
            _virtualKeyServiceMock.Setup(s => s.ValidateVirtualKeyAsync("test-key-model", "gpt-4"))
                .ReturnsAsync(virtualKey);
            
            // Also setup for null model case in case route extraction isn't working
            _virtualKeyServiceMock.Setup(s => s.ValidateVirtualKeyAsync("test-key-model", null))
                .ReturnsAsync(virtualKey);

            // Act
            await _attribute.OnAuthorizationAsync(context);

            // Assert
            Assert.Null(context.Result); // Authorization passed
            _virtualKeyServiceMock.Verify(s => s.ValidateVirtualKeyAsync("test-key-model", "gpt-4"), Times.Once);
        }

        /// <summary>
        /// Creates a virtual key entity with specified balance in its group
        /// </summary>
        private VirtualKey CreateVirtualKeyWithBalance(decimal balance)
        {
            return new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "test-hash",
                IsEnabled = true,
                VirtualKeyGroupId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates an AuthorizationFilterContext with virtual key claim
        /// </summary>
        private AuthorizationFilterContext CreateAuthorizationFilterContext(string virtualKey, string model = null)
        {
            var httpContext = new DefaultHttpContext();
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_virtualKeyServiceMock.Object);
            serviceCollection.AddSingleton(_loggerMock.Object);
            httpContext.RequestServices = serviceCollection.BuildServiceProvider();

            // Add virtual key claim
            var claims = new[]
            {
                new Claim("VirtualKey", virtualKey)
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            httpContext.User = principal;

            // Add model to route values if provided
            if (!string.IsNullOrEmpty(model))
            {
                httpContext.Request.RouteValues["model"] = model;
            }

            var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
            
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }

        /// <summary>
        /// Creates an AuthorizationFilterContext without virtual key claim
        /// </summary>
        private AuthorizationFilterContext CreateAuthorizationFilterContextWithoutClaim()
        {
            var httpContext = new DefaultHttpContext();
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_virtualKeyServiceMock.Object);
            serviceCollection.AddSingleton(_loggerMock.Object);
            httpContext.RequestServices = serviceCollection.BuildServiceProvider();

            // No claims - anonymous user
            var identity = new ClaimsIdentity();
            var principal = new ClaimsPrincipal(identity);
            httpContext.User = principal;

            var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
            
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }
    }
}