using System.Net;

using ConduitLLM.Core.Exceptions;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
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
    public void Map_ArgumentNullException_Returns400WithMissingParameter()
    {
        // Arrange
        var exception = new ArgumentNullException("testParam");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(400);
        result.ErrorCode.Should().Be("missing_parameter");
        result.ResponseMessage.Should().Be("Required parameter is missing");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Argument error");
        result.IncludeExceptionMessageInLog.Should().BeFalse();
        result.OpenAIErrorType.Should().Be("invalid_request_error");
        result.Param.Should().Be("testParam");
    }

    [Fact]
    public void Map_ArgumentException_Returns400WithInvalidParameter()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument value", "myParam");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(400);
        result.ErrorCode.Should().Be("invalid_parameter");
        result.ResponseMessage.Should().Be("Invalid parameter value");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Argument error");
        result.IncludeExceptionMessageInLog.Should().BeFalse();
        result.OpenAIErrorType.Should().Be("invalid_request_error");
        result.Param.Should().Be("myParam");
    }

    [Fact]
    public void Map_DuplicateProviderKeyException_Returns409()
    {
        // Derives from InvalidOperationException; must not fall into the generic 400 arm (#1261).
        var provider = new ConduitLLM.Configuration.Entities.Provider
        {
            Id = 1,
            ProviderType = ConduitLLM.Configuration.ProviderType.OpenAI,
            ProviderName = "openai"
        };
        var exception = new ConduitLLM.Configuration.Exceptions.DuplicateProviderKeyException(provider, 1);

        var result = ExceptionToResponseMapper.Map(exception);

        result.StatusCode.Should().Be(409);
        result.ErrorCode.Should().Be("duplicate_provider_key");
        result.IncludeExceptionMessageInLog.Should().BeTrue();
    }

    [Fact]
    public void Map_IdempotencyConflictException_Returns409()
    {
        var exception = new ConduitLLM.Configuration.Exceptions.IdempotencyConflictException(
            "A conflicting request with the same idempotency key is in progress");

        var result = ExceptionToResponseMapper.Map(exception);

        result.StatusCode.Should().Be(409);
        result.ErrorCode.Should().Be("conflict");
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
        result.ResponseMessage.Should().Be("The requested operation is not valid");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Invalid operation");
        result.IncludeExceptionMessageInLog.Should().BeFalse();
        result.OpenAIErrorType.Should().Be("invalid_request_error");
        result.Param.Should().BeNull();
    }

    [Fact]
    public void Map_DependencyResolutionException_Returns500WithServerError()
    {
        var exception = new InvalidOperationException(
            "Unable to resolve service for type 'IRequiredService' while attempting to activate 'ChatController'.");

        var result = ExceptionToResponseMapper.Map(exception);

        result.StatusCode.Should().Be(500);
        result.ErrorCode.Should().Be("dependency_resolution_error");
        result.ResponseMessage.Should().Be("A server dependency could not be resolved");
        result.LogLevel.Should().Be(LogLevel.Error);
        result.LogPrefix.Should().Be("Dependency resolution error");
        result.IncludeExceptionMessageInLog.Should().BeFalse();
        result.OpenAIErrorType.Should().Be("server_error");
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
        result.OpenAIErrorType.Should().Be("invalid_request_error");
    }

    [Fact]
    public void Map_UnauthorizedAccessException_Returns401WithUnauthorized()
    {
        // Arrange
        var exception = new UnauthorizedAccessException();

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(401);
        result.ErrorCode.Should().Be("unauthorized");
        result.ResponseMessage.Should().Be("Authentication required");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Unauthorized access attempt");
        result.IncludeExceptionMessageInLog.Should().BeFalse();
        result.OpenAIErrorType.Should().Be("invalid_request_error");
    }

    [Fact]
    public void Map_TimeoutException_Returns408WithTimeout()
    {
        // Arrange
        var exception = new TimeoutException("Operation timed out");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(408);
        result.ErrorCode.Should().Be("timeout");
        result.ResponseMessage.Should().Be("Request timed out");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.OpenAIErrorType.Should().Be("timeout_error");
        result.IncludeExceptionMessageInLog.Should().BeFalse();
    }

    [Fact]
    public void Map_NotImplementedException_Returns501WithNotImplemented()
    {
        // Arrange
        var exception = new NotImplementedException("Not yet available");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(501);
        result.ErrorCode.Should().Be("not_implemented");
        result.ResponseMessage.Should().Be("Feature not implemented");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.OpenAIErrorType.Should().Be("server_error");
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
        result.ResponseMessage.Should().Be("An unexpected error occurred");
        result.LogLevel.Should().Be(LogLevel.Error);
        result.LogPrefix.Should().Be("Unexpected error");
        result.IncludeExceptionMessageInLog.Should().BeFalse();
        result.OpenAIErrorType.Should().Be("server_error");
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
        result.OpenAIErrorType.Should().Be("server_error");
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
        result.OpenAIErrorType.Should().Be("invalid_request_error");
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
        result.OpenAIErrorType.Should().Be("invalid_request_error");
        result.Param.Should().Be("model");
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
        result.OpenAIErrorType.Should().Be("invalid_request_error");
    }

    [Fact]
    public void Map_InvalidRequestException_WithParam_ReturnsParam()
    {
        // Arrange
        var exception = new InvalidRequestException("Bad model value", "invalid_param", "model");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.Param.Should().Be("model");
        result.ErrorCode.Should().Be("invalid_param");
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
    public void Map_RequestTimeoutException_Returns408WithRequestTimeout()
    {
        // Arrange
        var exception = new RequestTimeoutException("Request timed out after 30s");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(408);
        result.ErrorCode.Should().Be("request_timeout");
        result.ResponseMessage.Should().Be("Request timed out after 30s");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.IncludeExceptionMessageInLog.Should().BeTrue();
        result.OpenAIErrorType.Should().Be("timeout_error");
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
        result.OpenAIErrorType.Should().Be("rate_limit_error");
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
        result.OpenAIErrorType.Should().Be("service_unavailable");
    }

    [Fact]
    public void Map_LLMCommunicationException_WithStatusCode_ReturnsProviderStatus()
    {
        // Arrange
        var exception = new LLMCommunicationException("Provider returned error",
            HttpStatusCode.BadGateway, "Bad gateway response");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(502);
        result.ErrorCode.Should().Be("provider_bad_gateway");
        result.ResponseMessage.Should().Be("Provider returned error");
        result.IncludeExceptionMessageInLog.Should().BeTrue();
        result.OpenAIErrorType.Should().Be("server_error");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void Map_LLMCommunicationException_WithProviderAuthFailure_Returns502(HttpStatusCode upstreamStatus)
    {
        // A provider credential rejection must not surface as a client 401/403 — an
        // OpenAI-compatible SDK would read that as "your gateway key is invalid" (#1257).
        var exception = new LLMCommunicationException("Provider rejected credentials",
            upstreamStatus, "auth failure");

        var result = ExceptionToResponseMapper.Map(exception);

        result.StatusCode.Should().Be(502);
        result.ErrorCode.Should().Be("provider_authentication_error");
        result.OpenAIErrorType.Should().Be("server_error");
    }

    [Fact]
    public void Map_LLMCommunicationException_WithProviderTimeout_Returns503()
    {
        var exception = new LLMCommunicationException("Provider timed out",
            HttpStatusCode.RequestTimeout, "timeout");

        var result = ExceptionToResponseMapper.Map(exception);

        result.StatusCode.Should().Be(503);
        result.ErrorCode.Should().Be("provider_timeout");
        result.OpenAIErrorType.Should().Be("server_error");
    }

    [Fact]
    public void Map_LLMCommunicationException_WithClientErrorStatus_ReturnsInvalidRequestType()
    {
        // Arrange
        var exception = new LLMCommunicationException("Bad request to provider",
            HttpStatusCode.BadRequest, null);

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(400);
        result.OpenAIErrorType.Should().Be("invalid_request_error");
        result.LogLevel.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public void Map_LLMCommunicationException_WithoutStatusCode_Returns502()
    {
        // Arrange
        var exception = new LLMCommunicationException("Unknown provider error");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert: unknown provider failure is an upstream fault, not a Conduit fault.
        result.StatusCode.Should().Be(502);
        result.ErrorCode.Should().Be("provider_communication_error");
        result.OpenAIErrorType.Should().Be("server_error");
        result.LogLevel.Should().Be(LogLevel.Error);
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
        result.OpenAIErrorType.Should().Be("server_error");
    }

    [Fact]
    public void Map_ValidationException_Returns400WithValidationError()
    {
        // Arrange
        var exception = new ValidationException("messages collection cannot be null or empty");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(400);
        result.ErrorCode.Should().Be("validation_error");
        result.ResponseMessage.Should().Be("messages collection cannot be null or empty");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Validation error");
        result.IncludeExceptionMessageInLog.Should().BeTrue();
        result.OpenAIErrorType.Should().Be("invalid_request_error");
    }

    [Fact]
    public void Map_UnsupportedProviderException_Returns400WithUnsupportedProvider()
    {
        // Arrange
        var exception = new UnsupportedProviderException("acme-ai");

        // Act
        var result = ExceptionToResponseMapper.Map(exception);

        // Assert
        result.StatusCode.Should().Be(400);
        result.ErrorCode.Should().Be("unsupported_provider");
        result.ResponseMessage.Should().Contain("acme-ai");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Unsupported provider");
        result.IncludeExceptionMessageInLog.Should().BeTrue();
        result.OpenAIErrorType.Should().Be("invalid_request_error");
        result.Param.Should().Be("provider");
    }

    #endregion

    #region Framework Binding Exception Tests

    [Fact]
    public void Map_BadHttpRequestException_Returns400WithRedactedMessage()
    {
        // Minimal-API model binding raises this when the body is malformed or missing a
        // required member. The framework message names internal types, so it is not echoed.
        var exception = new BadHttpRequestException(
            "JSON deserialization for type 'ConduitLLM.Core.Models.ChatCompletionRequest' was missing required properties, including: messages.");

        var result = ExceptionToResponseMapper.Map(exception);

        result.StatusCode.Should().Be(400);
        result.ErrorCode.Should().Be("invalid_request_body");
        result.ResponseMessage.Should().Be("The request body could not be read");
        result.ResponseMessage.Should().NotContain("ConduitLLM.Core.Models");
        result.LogLevel.Should().Be(LogLevel.Warning);
        result.LogPrefix.Should().Be("Malformed request");
        result.IncludeExceptionMessageInLog.Should().BeFalse();
        result.OpenAIErrorType.Should().Be("invalid_request_error");
    }

    [Fact]
    public void Map_BadHttpRequestException_PreservesNon400StatusCode()
    {
        // A request body over the configured size limit surfaces as 413, not 400.
        var exception = new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);

        var result = ExceptionToResponseMapper.Map(exception);

        result.StatusCode.Should().Be(413);
        result.ErrorCode.Should().Be("invalid_request_body");
        result.OpenAIErrorType.Should().Be("invalid_request_error");
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

        result.StatusCode.Should().Be(400);
        result.ErrorCode.Should().Be("missing_parameter");
        result.Param.Should().Be("param");
    }

    #endregion
}
