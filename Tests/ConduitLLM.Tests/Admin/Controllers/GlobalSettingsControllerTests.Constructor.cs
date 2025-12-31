using ConduitLLM.Admin.Controllers;

namespace ConduitLLM.Tests.Admin.Controllers
{
    public partial class GlobalSettingsControllerTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new GlobalSettingsController(null!, _mockCacheService.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullCacheService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new GlobalSettingsController(_mockService.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new GlobalSettingsController(_mockService.Object, _mockCacheService.Object, null!));
        }

        #endregion
    }
}