using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class MediaLifecycleServiceTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullMediaRepository_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MediaLifecycleService(
                null,
                _mockStorageService.Object,
                _mockLogger.Object,
                _mockOptions.Object));
        }

        [Fact]
        public void Constructor_WithNullStorageService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MediaLifecycleService(
                _mockMediaRepository.Object,
                null,
                _mockLogger.Object,
                _mockOptions.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MediaLifecycleService(
                _mockMediaRepository.Object,
                _mockStorageService.Object,
                null,
                _mockOptions.Object));
        }

        [Fact]
        public void Constructor_WithNullOptions_ShouldUseDefaultOptions()
        {
            // Act
            var service = new MediaLifecycleService(
                _mockMediaRepository.Object,
                _mockStorageService.Object,
                _mockLogger.Object,
                null);

            // Assert - Should not throw and use default configuration
            Assert.NotNull(service);
        }

        #endregion
    }
}