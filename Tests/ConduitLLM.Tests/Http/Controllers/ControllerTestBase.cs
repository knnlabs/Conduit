using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    /// <summary>
    /// Base class for HTTP controller tests providing common setup and helper methods
    /// </summary>
    public abstract class ControllerTestBase : TestBase
    {
        protected ControllerTestBase(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// Creates a controller context with a default HTTP context
        /// </summary>
        protected ControllerContext CreateControllerContext()
        {
            var httpContext = new DefaultHttpContext();
            return new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        /// <summary>
        /// Creates a controller context with custom headers
        /// </summary>
        protected ControllerContext CreateControllerContext(Action<IHeaderDictionary> configureHeaders)
        {
            var context = CreateControllerContext();
            configureHeaders(context.HttpContext.Request.Headers);
            return context;
        }

        /// <summary>
        /// Creates a controller context with a specific user principal
        /// </summary>
        protected ControllerContext CreateControllerContextWithUser(string userId, string virtualKeyId = null)
        {
            var context = CreateControllerContext();
            
            // Add claims for authenticated user
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("sub", userId),
                new System.Security.Claims.Claim("user_id", userId)
            };

            if (!string.IsNullOrEmpty(virtualKeyId))
            {
                claims.Add(new System.Security.Claims.Claim("virtual_key_id", virtualKeyId));
            }

            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            
            context.HttpContext.User = principal;
            return context;
        }

        /// <summary>
        /// Creates a controller context with request body
        /// </summary>
        protected ControllerContext CreateControllerContextWithBody<T>(T body)
        {
            var context = CreateControllerContext();
            
            // Serialize body to JSON and set in request
            var json = System.Text.Json.JsonSerializer.Serialize(body);
            var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            
            context.HttpContext.Request.Body = stream;
            context.HttpContext.Request.ContentType = "application/json";
            context.HttpContext.Request.ContentLength = stream.Length;
            
            return context;
        }

        /// <summary>
        /// Asserts that the result is a successful ObjectResult with expected value
        /// </summary>
        protected void AssertOkObjectResult<T>(IActionResult result, Action<T> assertions = null)
        {
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            
            var value = Assert.IsType<T>(okResult.Value);
            assertions?.Invoke(value);
        }

        /// <summary>
        /// Asserts that the result is a BadRequest with expected error message
        /// </summary>
        protected void AssertBadRequest(IActionResult result, string expectedMessage = null)
        {
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            
            if (!string.IsNullOrEmpty(expectedMessage))
            {
                Assert.Equal(expectedMessage, badRequestResult.Value?.ToString());
            }
        }

        /// <summary>
        /// Asserts that the result is NotFound
        /// </summary>
        protected void AssertNotFound(IActionResult result)
        {
            Assert.IsType<NotFoundResult>(result);
        }

        /// <summary>
        /// Asserts that the result is Unauthorized
        /// </summary>
        protected void AssertUnauthorized(IActionResult result)
        {
            Assert.IsType<UnauthorizedResult>(result);
        }

        /// <summary>
        /// Asserts that the result is a 500 Internal Server Error with expected message
        /// </summary>
        protected void AssertInternalServerError(IActionResult result, string expectedMessage = null)
        {
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            
            if (!string.IsNullOrEmpty(expectedMessage))
            {
                Assert.Equal(expectedMessage, objectResult.Value?.ToString());
            }
        }

        /// <summary>
        /// Creates a mock logger for the specified type
        /// </summary>
        protected new Mock<ILogger<T>> CreateLogger<T>()
        {
            return base.CreateLogger<T>();
        }
    }
}