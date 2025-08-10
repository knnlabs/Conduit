using System;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace ConduitLLM.Admin.Tests.Controllers
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
    }
}