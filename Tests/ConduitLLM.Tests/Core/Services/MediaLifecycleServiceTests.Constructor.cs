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
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MediaLifecycleService(
                _mockMediaRepository.Object,
                null));
        }

        #endregion
    }
}
