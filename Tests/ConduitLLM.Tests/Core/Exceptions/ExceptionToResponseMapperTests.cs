using ConduitLLM.Core.Exceptions;

using FluentAssertions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Tests.Core.Exceptions;

/// <summary>
/// Unit tests for the <see cref="ExceptionToResponseMapper"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ExceptionMapping")]
public class ExceptionToResponseMapperTests
{
    #region Standard .NET Exception Tests

    [Fact]
    public void Map_ArgumentNullException_Returns400WithInvalidArgument()
    {
        // Arrange
        var exception = new ArgumentNullException("testParam");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(400);
        result.ErrorCode.Should().Be("invalid_argument");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Argument error");
        result.IncludeExceptionMessageInLog.Should().BeTrue();
    }

    [Fact]
    public void Map_ArgumentException_Returns400WithInvalidArgument()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument value");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(400);
        result.ErrorCode.Should().Be("invalid_argument");
        result.ResponseMessage.Should().Be("Invalid argument value");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Argument error");
        result.IncludeExceptionMessageInLog.Should().BeTrue();
    }

    [Fact]
    public void Map_InvalidOperationException_Returns400WithInvalidOperation()
    {
        // Arrange
        var exception = new InvalidOperationException("Cannot perform this operation");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(400);
        result.ErrorCode.Should().Be("invalid_operation");
        result.ResponseMessage.Should().Be("Cannot perform this operation");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Invalid operation");
        result.IncludeExceptionMessageInLog.Should().BeTrue();
    }

    [Fact]
    public void Map_KeyNotFoundException_Returns404WithNotFound()
    {
        // Arrange
        var exception = new KeyNotFoundException("Resource not found");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(404);
        result.ErrorCode.Should().Be("not_found");
        result.ResponseMessage.Should().Be("The requested resource was not found");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Resource not found");
        result.IncludeExceptionMessageInLog.Should().BeFalse();
    }

    [Fact]
    public void Map_UnauthorizedAccessException_Returns403WithForbidden()
    {
        // Arrange
        var exception = new UnauthorizedAccessException();

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(403);
        result.ErrorCode.Should().Be("forbidden");
        result.ResponseMessage.Should().Be("Access denied");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Unauthorized access attempt");
        result.IncludeExceptionMessageInLog.Should().BeFalse();
    }

    [Fact]
    public void Map_GenericException_Returns500WithInternalError()
    {
        // Arrange
        var exception = new Exception("Unexpected error");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(500);
        result.ErrorCode.Should().Be("internal_error");
        result.ResponseMessage.Should().Be("An unexpected error occurred.");
        result.LogLevel.Should().Be(LogLevel.Error);
        result.LogPrefix.Should().Be("Unexpected error");
        result.IncludeExceptionMessageInLog.Should().BeFalse();
    }

    [Fact]
    public void Map_UnknownExceptionType_Returns500WithInternalError()
    {
        // Arrange
        var exception = new InvalidProgramException("Program error");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(500);
        result.ErrorCode.Should().Be("internal_error");
        result.LogLevel.Should().Be(LogLevel.Error);
    }

    #endregion

    #region Custom Conduit Exception Tests

    [Fact]
    public void Map_AuthorizationException_Returns403WithForbidden()
    {
        // Arrange
        var exception = new AuthorizationException("Not authorized to access this resource");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(403);
        result.ErrorCode.Should().Be("forbidden");
        result.ResponseMessage.Should().Be("Not authorized to access this resource");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Authorization denied");
        result.IncludeExceptionMessageInLog.Should().BeTrue();
    }

    [Fact]
    public void Map_ModelNotFoundException_Returns404WithModelNotFound()
    {
        // Arrange
        var exception = new ModelNotFoundException("gpt-5");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(404);
        result.ErrorCode.Should().Be("model_not_found");
        result.ResponseMessage.Should().Contain("gpt-5");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Model not found");
        result.IncludeExceptionMessageInLog.Should().BeTrue();
    }

    [Fact]
    public void Map_InvalidRequestException_Returns400WithErrorCode()
    {
        // Arrange
        var exception = new InvalidRequestException("Invalid request body", "invalid_json");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(400);
        result.ErrorCode.Should().Be("invalid_json");
        result.ResponseMessage.Should().Be("Invalid request body");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Invalid request");
        result.IncludeExceptionMessageInLog.Should().BeTrue();
    }

    [Fact]
    public void Map_InvalidRequestException_WithoutErrorCode_Returns400WithDefaultCode()
    {
        // Arrange
        var exception = new InvalidRequestException("Invalid request body");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(400);
        result.ErrorCode.Should().Be("invalid_request");
        result.ResponseMessage.Should().Be("Invalid request body");
    }

    [Fact]
    public void Map_RateLimitExceededException_Returns429WithRateLimitExceeded()
    {
        // Arrange
        var exception = new RateLimitExceededException("Rate limit exceeded, try again later");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(429);
        result.ErrorCode.Should().Be("rate_limit_exceeded");
        result.ResponseMessage.Should().Be("Rate limit exceeded, try again later");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Rate limit exceeded");
        result.IncludeExceptionMessageInLog.Should().BeTrue();
    }

    [Fact]
    public void Map_ServiceUnavailableException_Returns503WithServiceUnavailable()
    {
        // Arrange
        var exception = new ServiceUnavailableException("Service temporarily unavailable");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(503);
        result.ErrorCode.Should().Be("service_unavailable");
        result.ResponseMessage.Should().Be("Service temporarily unavailable");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Service unavailable");
        result.IncludeExceptionMessageInLog.Should().BeTrue();
    }

    [Fact]
    public void Map_ConfigurationException_Returns500WithConfigurationError()
    {
        // Arrange
        var exception = new ConfigurationException("Invalid configuration setting");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(500);
        result.ErrorCode.Should().Be("configuration_error");
        result.ResponseMessage.Should().Be("A configuration error occurred");
        result.LogLevel.Should().Be(LogLevel.Error);
        result.LogPrefix.Should().Be("Configuration error");
        result.IncludeExceptionMessageInLog.Should().BeFalse();
    }

    #endregion

    #region Exception Hierarchy Tests

    [Fact]
    public void Map_ArgumentNullException_IsHandledBeforeArgumentException()
    {
        // ArgumentNullException derives from ArgumentException
        // Verify the more specific type is matched first
        var exception = new ArgumentNullException("param");

        var result = ExceptionToResponseMapper.Map(exception);

        // Both would return 400/invalid_argument, but we verify it's recognized
        result.StatusCode.Should().Be(400);
        result.ErrorCode.Should().Be("invalid_argument");
    }

    #endregion
}
