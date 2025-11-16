using ConduitLLM.Configuration.Entities;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminVirtualKeyServiceTests
    {

        [Fact]
        public async Task ListVirtualKeysAsync_WithoutGroupId_ReturnsAllKeys()
        {
            // Arrange
            var allKeys = new List<VirtualKey>
            {
                new VirtualKey { Id = 1, KeyName = "Key1", VirtualKeyGroupId = 1 },
                new VirtualKey { Id = 2, KeyName = "Key2", VirtualKeyGroupId = 2 },
                new VirtualKey { Id = 3, KeyName = "Key3", VirtualKeyGroupId = 1 }
            };

            _mockVirtualKeyRepository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(allKeys);

            // Act
            var result = await _service.ListVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            _mockVirtualKeyRepository.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockVirtualKeyRepository.Verify(x => x.GetByVirtualKeyGroupIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ListVirtualKeysAsync_WithGroupId_ReturnsFilteredKeys()
        {
            // Arrange
            const int groupId = 1;
            var groupKeys = new List<VirtualKey>
            {
                new VirtualKey { Id = 1, KeyName = "Key1", VirtualKeyGroupId = groupId },
                new VirtualKey { Id = 3, KeyName = "Key3", VirtualKeyGroupId = groupId }
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByVirtualKeyGroupIdAsync(groupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(groupKeys);

            // Act
            var result = await _service.ListVirtualKeysAsync(groupId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, dto => Assert.Equal(groupId, dto.VirtualKeyGroupId));
            _mockVirtualKeyRepository.Verify(x => x.GetByVirtualKeyGroupIdAsync(groupId, It.IsAny<CancellationToken>()), Times.Once);
            _mockVirtualKeyRepository.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ListVirtualKeysAsync_WithInvalidGroupId_ReturnsEmptyList()
        {
            // Arrange
            const int groupId = 999;
            var emptyList = new List<VirtualKey>();

            _mockVirtualKeyRepository.Setup(x => x.GetByVirtualKeyGroupIdAsync(groupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(emptyList);

            // Act
            var result = await _service.ListVirtualKeysAsync(groupId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _mockVirtualKeyRepository.Verify(x => x.GetByVirtualKeyGroupIdAsync(groupId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ListVirtualKeysAsync_LogsCorrectMessage_ForGroupFilter()
        {
            // Arrange
            const int groupId = 1;
            var groupKeys = new List<VirtualKey>
            {
                new VirtualKey { Id = 1, KeyName = "Key1", VirtualKeyGroupId = groupId }
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByVirtualKeyGroupIdAsync(groupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(groupKeys);

            // Act
            await _service.ListVirtualKeysAsync(groupId);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Listing virtual keys for group {groupId}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ListVirtualKeysAsync_HandlesNullGroupId()
        {
            // Arrange
            int? groupId = null;
            var allKeys = new List<VirtualKey>
            {
                new VirtualKey { Id = 1, KeyName = "Key1", VirtualKeyGroupId = 1 },
                new VirtualKey { Id = 2, KeyName = "Key2", VirtualKeyGroupId = 2 }
            };

            _mockVirtualKeyRepository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(allKeys);

            // Act
            var result = await _service.ListVirtualKeysAsync(groupId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            _mockVirtualKeyRepository.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockVirtualKeyRepository.Verify(x => x.GetByVirtualKeyGroupIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}