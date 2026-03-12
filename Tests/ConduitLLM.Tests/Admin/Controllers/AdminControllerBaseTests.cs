using ConduitLLM.Admin.Controllers;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Tests.TestHelpers;

using FluentAssertions;

using MassTransit;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Unit tests for the AdminControllerBase class.
    /// Tests exception handling and standardized error responses.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class AdminControllerBaseTests
    {
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<TestableAdminController>> _mockLogger;
        private readonly TestableAdminController _controller;
        private readonly ITestOutputHelper _output;

        public AdminControllerBaseTests(ITestOutputHelper output)
        {
            _output = output;
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<TestableAdminController>>();
            _controller = new TestableAdminController(_mockPublishEndpoint.Object, _mockLogger.Object);

            // Setup controller context for testing
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        #region ExecuteAsync<T> Tests

        [Fact]
        public async Task ExecuteAsync_OnSuccess_ReturnsSuccessResult()
        {
            // Arrange
            var expectedResult = new TestDto { Id = 1, Name = "Test" };
            Func<Task<TestDto>> operation = () => Task.FromResult(expectedResult);
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            var result = await _controller.TestExecuteAsync(operation, successAction, "TestOperation");

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = (OkObjectResult)result;
            okResult.Value.Should().Be(expectedResult);
        }

        [Fact]
        public async Task ExecuteAsync_OnArgumentException_Returns400BadRequest()
        {
            // Arrange
            var exceptionMessage = "Invalid argument provided";
            Func<Task<TestDto>> operation = () => throw new ArgumentException(exceptionMessage);
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            var result = await _controller.TestExecuteAsync(operation, successAction, "TestOperation");

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequest = (BadRequestObjectResult)result;
            badRequest.Value.Should().BeOfType<ErrorResponseDto>();
            var error = (ErrorResponseDto)badRequest.Value!;
            error.error.Should().Be("Invalid parameter value");
            error.Code.Should().Be("invalid_parameter");
        }

        [Fact]
        public async Task ExecuteAsync_OnArgumentNullException_Returns400BadRequest()
        {
            // Arrange
            var paramName = "testParam";
            Func<Task<TestDto>> operation = () => throw new ArgumentNullException(paramName);
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            var result = await _controller.TestExecuteAsync(operation, successAction, "TestOperation");

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequest = (BadRequestObjectResult)result;
            badRequest.Value.Should().BeOfType<ErrorResponseDto>();
            var error = (ErrorResponseDto)badRequest.Value!;
            error.Code.Should().Be("missing_parameter");
        }

        [Fact]
        public async Task ExecuteAsync_OnInvalidOperationException_Returns400BadRequest()
        {
            // Arrange
            var exceptionMessage = "Invalid operation attempted";
            Func<Task<TestDto>> operation = () => throw new InvalidOperationException(exceptionMessage);
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            var result = await _controller.TestExecuteAsync(operation, successAction, "TestOperation");

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequest = (BadRequestObjectResult)result;
            badRequest.Value.Should().BeOfType<ErrorResponseDto>();
            var error = (ErrorResponseDto)badRequest.Value!;
            error.error.Should().Be("The requested operation is not valid");
            error.Code.Should().Be("invalid_operation");
        }

        [Fact]
        public async Task ExecuteAsync_OnKeyNotFoundException_Returns404NotFound()
        {
            // Arrange
            Func<Task<TestDto>> operation = () => throw new KeyNotFoundException("Resource not found");
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            var result = await _controller.TestExecuteAsync(operation, successAction, "TestOperation");

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFound = (NotFoundObjectResult)result;
            notFound.Value.Should().BeOfType<ErrorResponseDto>();
            var error = (ErrorResponseDto)notFound.Value!;
            error.Code.Should().Be("not_found");
        }

        [Fact]
        public async Task ExecuteAsync_OnUnauthorizedAccessException_Returns401Unauthorized()
        {
            // Arrange
            Func<Task<TestDto>> operation = () => throw new UnauthorizedAccessException();
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            var result = await _controller.TestExecuteAsync(operation, successAction, "TestOperation");

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = (ObjectResult)result;
            objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
            objectResult.Value.Should().BeOfType<ErrorResponseDto>();
            var error = (ErrorResponseDto)objectResult.Value!;
            error.Code.Should().Be("unauthorized");
        }

        [Fact]
        public async Task ExecuteAsync_OnGenericException_Returns500InternalServerError()
        {
            // Arrange
            Func<Task<TestDto>> operation = () => throw new InvalidProgramException("Unexpected error");
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            var result = await _controller.TestExecuteAsync(operation, successAction, "TestOperation");

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = (ObjectResult)result;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            objectResult.Value.Should().BeOfType<ErrorResponseDto>();
            var error = (ErrorResponseDto)objectResult.Value!;
            error.Code.Should().Be("internal_error");
        }

        #endregion

        #region ExecuteAsync (void) Tests

        [Fact]
        public async Task ExecuteAsync_VoidOperation_OnSuccess_ReturnsSuccessResult()
        {
            // Arrange
            var executed = false;
            Func<Task> operation = () => { executed = true; return Task.CompletedTask; };
            var successResult = new NoContentResult();

            // Act
            var result = await _controller.TestExecuteAsync(operation, successResult, "VoidOperation");

            // Assert
            executed.Should().BeTrue();
            result.Should().Be(successResult);
        }

        [Fact]
        public async Task ExecuteAsync_VoidOperation_OnException_ReturnsAppropriateError()
        {
            // Arrange
            Func<Task> operation = () => throw new ArgumentException("Invalid");
            var successResult = new NoContentResult();

            // Act
            var result = await _controller.TestExecuteAsync(operation, successResult, "VoidOperation");

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region ExecuteWithNotFoundAsync Tests

        [Fact]
        public async Task ExecuteWithNotFoundAsync_WhenEntityExists_ReturnsSuccessResult()
        {
            // Arrange
            var expectedResult = new TestDto { Id = 1, Name = "Found Entity" };
            Func<Task<TestDto?>> operation = () => Task.FromResult<TestDto?>(expectedResult);
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            var result = await _controller.TestExecuteWithNotFoundAsync(
                operation, successAction, "TestEntity", 1, "GetById");

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = (OkObjectResult)result;
            okResult.Value.Should().Be(expectedResult);
        }

        [Fact]
        public async Task ExecuteWithNotFoundAsync_WhenEntityNull_Returns404NotFound()
        {
            // Arrange
            Func<Task<TestDto?>> operation = () => Task.FromResult<TestDto?>(null);
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            var result = await _controller.TestExecuteWithNotFoundAsync(
                operation, successAction, "TestEntity", 42, "GetById");

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFound = (NotFoundObjectResult)result;
            notFound.Value.Should().BeOfType<ErrorResponseDto>();
            var error = (ErrorResponseDto)notFound.Value!;
            error.error.ToString().Should().Contain("TestEntity");
            error.error.ToString().Should().Contain("42");
            error.Code.Should().Be("not_found");
        }

        [Fact]
        public async Task ExecuteWithNotFoundAsync_WhenEntityNull_LogsWarning()
        {
            // Arrange
            Func<Task<TestDto?>> operation = () => Task.FromResult<TestDto?>(null);
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            await _controller.TestExecuteWithNotFoundAsync(
                operation, successAction, "Provider", 123, "GetProviderById");

            // Assert
            _mockLogger.VerifyLog(LogLevel.Warning, "not found", Times.Once());
        }

        [Fact]
        public async Task ExecuteWithNotFoundAsync_WithAsyncSuccessAction_ExecutesCorrectly()
        {
            // Arrange
            var expectedResult = new TestDto { Id = 1, Name = "Entity" };
            Func<Task<TestDto?>> operation = () => Task.FromResult<TestDto?>(expectedResult);
            Func<TestDto, Task<IActionResult>> asyncSuccessAction = async dto =>
            {
                await Task.Delay(1); // Simulate async work
                return new OkObjectResult(new { dto.Id, Processed = true });
            };

            // Act
            var result = await _controller.TestExecuteWithNotFoundAsyncAction(
                operation, asyncSuccessAction, "TestEntity", 1, "GetAndProcess");

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task ExecuteWithNotFoundAsync_OnException_ReturnsAppropriateError()
        {
            // Arrange
            Func<Task<TestDto?>> operation = () => throw new InvalidOperationException("Database error");
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            var result = await _controller.TestExecuteWithNotFoundAsync(
                operation, successAction, "TestEntity", 1, "GetById");

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequest = (BadRequestObjectResult)result;
            var error = (ErrorResponseDto)badRequest.Value!;
            error.error.Should().Be("The requested operation is not valid");
            error.Code.Should().Be("invalid_operation");
        }

        #endregion

        #region Exception Logging Tests

        [Fact]
        public async Task ExecuteAsync_OnArgumentException_LogsWarning()
        {
            // Arrange
            Func<Task<TestDto>> operation = () => throw new ArgumentException("Bad arg");
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            await _controller.TestExecuteAsync(operation, successAction, "TestOperation");

            // Assert
            _mockLogger.VerifyLog(LogLevel.Warning, "Argument error", Times.Once());
        }

        [Fact]
        public async Task ExecuteAsync_OnInvalidOperationException_LogsWarning()
        {
            // Arrange
            Func<Task<TestDto>> operation = () => throw new InvalidOperationException("Invalid op");
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            await _controller.TestExecuteAsync(operation, successAction, "TestOperation");

            // Assert
            _mockLogger.VerifyLog(LogLevel.Warning, "Invalid operation", Times.Once());
        }

        [Fact]
        public async Task ExecuteAsync_OnKeyNotFoundException_LogsWarning()
        {
            // Arrange
            Func<Task<TestDto>> operation = () => throw new KeyNotFoundException();
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            await _controller.TestExecuteAsync(operation, successAction, "TestOperation");

            // Assert
            _mockLogger.VerifyLog(LogLevel.Warning, "not found", Times.Once());
        }

        [Fact]
        public async Task ExecuteAsync_OnUnauthorizedAccessException_LogsWarning()
        {
            // Arrange
            Func<Task<TestDto>> operation = () => throw new UnauthorizedAccessException();
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            await _controller.TestExecuteAsync(operation, successAction, "TestOperation");

            // Assert
            _mockLogger.VerifyLog(LogLevel.Warning, "Unauthorized", Times.Once());
        }

        [Fact]
        public async Task ExecuteAsync_OnGenericException_LogsError()
        {
            // Arrange
            Func<Task<TestDto>> operation = () => throw new Exception("Unexpected");
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);

            // Act
            await _controller.TestExecuteAsync(operation, successAction, "TestOperation");

            // Assert
            _mockLogger.VerifyLog(LogLevel.Error, "Unexpected error", Times.Once());
        }

        [Fact]
        public async Task ExecuteAsync_WithContextData_IncludesContextInLog()
        {
            // Arrange
            Func<Task<TestDto>> operation = () => throw new ArgumentException("Error");
            Func<TestDto, IActionResult> successAction = dto => new OkObjectResult(dto);
            var contextData = new { EntityId = 42, EntityType = "Provider" };

            // Act
            await _controller.TestExecuteAsync(operation, successAction, "GetProvider", contextData);

            // Assert
            _mockLogger.VerifyLog(LogLevel.Warning, "GetProvider", Times.Once());
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new TestableAdminController(_mockPublishEndpoint.Object, null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Fact]
        public void Constructor_WithNullPublishEndpoint_DoesNotThrow()
        {
            // Act & Assert
            var act = () => new TestableAdminController(null, _mockLogger.Object);
            act.Should().NotThrow();
        }

        #endregion
    }

    #region Test Helpers

    /// <summary>
    /// Test DTO for verifying operation results.
    /// </summary>
    public class TestDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Concrete implementation of AdminControllerBase for testing.
    /// Exposes protected methods as public for testing purposes.
    /// </summary>
    public class TestableAdminController : AdminControllerBase
    {
        public TestableAdminController(IPublishEndpoint? publishEndpoint, ILogger<TestableAdminController> logger)
            : base(publishEndpoint, logger)
        {
        }

        /// <summary>
        /// Exposes ExecuteAsync for testing.
        /// </summary>
        public Task<IActionResult> TestExecuteAsync<T>(
            Func<Task<T>> operation,
            Func<T, IActionResult> successAction,
            string operationName,
            object? contextData = null)
        {
            return ExecuteAsync(operation, successAction, operationName, contextData);
        }

        /// <summary>
        /// Exposes ExecuteAsync (void) for testing.
        /// </summary>
        public Task<IActionResult> TestExecuteAsync(
            Func<Task> operation,
            IActionResult successResult,
            string operationName,
            object? contextData = null)
        {
            return ExecuteAsync(operation, successResult, operationName, contextData);
        }

        /// <summary>
        /// Exposes ExecuteWithNotFoundAsync for testing (sync success action).
        /// </summary>
        public Task<IActionResult> TestExecuteWithNotFoundAsync<T>(
            Func<Task<T?>> operation,
            Func<T, IActionResult> successAction,
            string entityType,
            object? entityId,
            string operationName) where T : class
        {
            return ExecuteWithNotFoundAsync(operation, successAction, entityType, entityId, operationName);
        }

        /// <summary>
        /// Exposes ExecuteWithNotFoundAsync for testing (async success action).
        /// </summary>
        public Task<IActionResult> TestExecuteWithNotFoundAsyncAction<T>(
            Func<Task<T?>> operation,
            Func<T, Task<IActionResult>> successAction,
            string entityType,
            object? entityId,
            string operationName) where T : class
        {
            return ExecuteWithNotFoundAsync(operation, successAction, entityType, entityId, operationName);
        }
    }

    #endregion
}
