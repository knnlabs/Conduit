// TODO: Update tests for new Model architecture where capabilities come from Model entity
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Unit tests for the ModelProviderMappingController class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public partial class ModelProviderMappingControllerTests
    {
        private readonly Mock<IAdminModelProviderMappingService> _mockService;
        private readonly Mock<IProviderService> _mockCredentialService;
        private readonly Mock<ILogger<ModelProviderMappingController>> _mockLogger;
        private readonly ModelProviderMappingController _controller;
        private readonly ITestOutputHelper _output;

        public ModelProviderMappingControllerTests(ITestOutputHelper output)
        {
            _output = output;
            _mockService = new Mock<IAdminModelProviderMappingService>();
            _mockCredentialService = new Mock<IProviderService>();
            _mockLogger = new Mock<ILogger<ModelProviderMappingController>>();
            _controller = new ModelProviderMappingController(_mockService.Object, _mockCredentialService.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelProviderMappingController(null!, _mockCredentialService.Object, _mockLogger.Object));
        }


        [Fact]
        public void Constructor_WithNullCredentialService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelProviderMappingController(_mockService.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelProviderMappingController(_mockService.Object, _mockCredentialService.Object, null!));
        }

        #endregion

    }
}