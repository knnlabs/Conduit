using ConduitLLM.Admin.Validation;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ConduitLLM.Tests.Admin.Validation
{
    /// <summary>
    /// Unit tests for <see cref="InvalidModelStateResponse"/> — the custom
    /// <c>InvalidModelStateResponseFactory</c> that maps [ApiController] validation failures to
    /// the Admin API's standard <c>ErrorResponseDto</c> (Tier 2a, #904).
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class InvalidModelStateResponseTests
    {
        [Fact]
        public void BuildErrorResponse_AggregatesFieldErrors_WithValidationErrorCode()
        {
            // Arrange
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Name", "The Name field is required.");
            modelState.AddModelError("Age", "The field Age must be between 1 and 120.");

            // Act
            var dto = InvalidModelStateResponse.BuildErrorResponse(modelState);

            // Assert
            dto.Code.Should().Be("validation_error");
            var message = dto.error.ToString()!;
            message.Should().Contain("Name").And.Contain("required");
            message.Should().Contain("Age");
        }

        [Fact]
        public void BuildErrorResponse_WhenNoSpecificErrors_ReturnsGenericMessage()
        {
            // Arrange
            var modelState = new ModelStateDictionary();

            // Act
            var dto = InvalidModelStateResponse.BuildErrorResponse(modelState);

            // Assert
            dto.Code.Should().Be("validation_error");
            dto.error.ToString().Should().Contain("validation");
        }
    }
}
