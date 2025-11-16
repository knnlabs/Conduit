using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Unit tests for the GlobalSettingsController class - Setup and infrastructure.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public partial class GlobalSettingsControllerTests
    {
        private readonly Mock<IAdminGlobalSettingService> _mockService;
        private readonly Mock<ILogger<GlobalSettingsController>> _mockLogger;
        private readonly GlobalSettingsController _controller;
        private readonly ITestOutputHelper _output;

        public GlobalSettingsControllerTests(ITestOutputHelper output)
        {
            _output = output;
            _mockService = new Mock<IAdminGlobalSettingService>();
            _mockLogger = new Mock<ILogger<GlobalSettingsController>>();
            _controller = new GlobalSettingsController(_mockService.Object, _mockLogger.Object);
        }

        /// <summary>
        /// Helper method to assert that an error response contains the expected error message.
        /// Controllers return anonymous objects like { error = "message" } which need reflection to access.
        /// </summary>
        private static void AssertErrorResponse(object responseValue, string expectedError)
        {
            responseValue.Should().NotBeNull();
            var valueType = responseValue.GetType();
            var errorProperty = valueType.GetProperty("error");
            errorProperty.Should().NotBeNull("Response should have an 'error' property");
            var errorValue = errorProperty?.GetValue(responseValue);
            errorValue.Should().Be(expectedError);
        }
    }
}