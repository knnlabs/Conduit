using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminAudioUsageServiceTests
    {
        #region Cleanup Tests

        [Fact]
        public async Task CleanupOldLogsAsync_ShouldDeleteOldLogs()
        {
            // Arrange
            var retentionDays = 30;
            var expectedCutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var deletedCount = 100;

            _mockRepository.Setup(x => x.DeleteOldLogsAsync(It.Is<DateTime>(d => 
                d.Date == expectedCutoffDate.Date)))
                .ReturnsAsync(deletedCount);

            // Act
            var result = await _service.CleanupOldLogsAsync(retentionDays);

            // Assert
            result.Should().Be(deletedCount);
            _mockRepository.Verify(x => x.DeleteOldLogsAsync(It.IsAny<DateTime>()), Times.Once);
        }

        #endregion
    }
}