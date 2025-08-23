using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Services;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class MediaLifecycleServiceTests
    {
        // Additional field for virtual key repository (not in the base class)
        private Mock<IVirtualKeyRepository> CreateMockVirtualKeyRepository()
        {
            return new Mock<IVirtualKeyRepository>();
        }

        private MediaLifecycleService CreateServiceWithVirtualKeyRepository(Mock<IVirtualKeyRepository> mockVirtualKeyRepository)
        {
            return new MediaLifecycleService(
                _mockMediaRepository.Object,
                _mockStorageService.Object,
                _mockLogger.Object,
                _mockOptions.Object,
                mockVirtualKeyRepository.Object
            );
        }

        [Fact]
        public async Task GetOverallStorageStatsAsync_WithoutGroupId_ReturnsAllStats()
        {
            // Arrange
            var mockVirtualKeyRepository = CreateMockVirtualKeyRepository();
            var service = CreateServiceWithVirtualKeyRepository(mockVirtualKeyRepository);
            
            var allMedia = new List<MediaRecord>
            {
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    VirtualKeyId = 1, 
                    MediaType = "image", 
                    SizeBytes = 1000,
                    Provider = "replicate"
                },
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    VirtualKeyId = 2, 
                    MediaType = "video", 
                    SizeBytes = 5000,
                    Provider = "openai"
                },
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    VirtualKeyId = 1, 
                    MediaType = "image", 
                    SizeBytes = 2000,
                    Provider = "replicate"
                }
            };

            var providerStats = new Dictionary<string, long>
            {
                { "replicate", 3000 },
                { "openai", 5000 }
            };

            _mockMediaRepository.Setup(x => x.GetMediaOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(allMedia);
            _mockMediaRepository.Setup(x => x.GetStorageStatsByProviderAsync())
                .ReturnsAsync(providerStats);
            _mockMediaRepository.Setup(x => x.GetOrphanedMediaAsync())
                .ReturnsAsync(new List<MediaRecord>());

            // Act
            var result = await service.GetOverallStorageStatsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(8000, result.TotalSizeBytes);
            Assert.Equal(3, result.TotalFiles);
            Assert.Equal(0, result.OrphanedFiles);
            Assert.Equal(2, result.ByProvider.Count);
            Assert.Equal(2, result.ByMediaType.Count);
            Assert.Equal(2, result.StorageByVirtualKey.Count);
            Assert.Equal(3000L, result.StorageByVirtualKey["1"]);
            Assert.Equal(5000L, result.StorageByVirtualKey["2"]);
        }

        [Fact]
        public async Task GetOverallStorageStatsAsync_WithGroupId_ReturnsFilteredStats()
        {
            // Arrange
            var mockVirtualKeyRepository = CreateMockVirtualKeyRepository();
            var service = CreateServiceWithVirtualKeyRepository(mockVirtualKeyRepository);
            
            const int groupId = 1;
            var virtualKeys = new List<VirtualKey>
            {
                new VirtualKey { Id = 1, VirtualKeyGroupId = groupId },
                new VirtualKey { Id = 3, VirtualKeyGroupId = groupId }
            };

            var mediaForKey1 = new List<MediaRecord>
            {
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    VirtualKeyId = 1, 
                    MediaType = "image", 
                    SizeBytes = 1000,
                    Provider = "replicate"
                }
            };

            var mediaForKey3 = new List<MediaRecord>
            {
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    VirtualKeyId = 3, 
                    MediaType = "video", 
                    SizeBytes = 2000,
                    Provider = "openai"
                }
            };

            mockVirtualKeyRepository.Setup(x => x.GetByVirtualKeyGroupIdAsync(groupId, default))
                .ReturnsAsync(virtualKeys);
            _mockMediaRepository.Setup(x => x.GetByVirtualKeyIdAsync(1))
                .ReturnsAsync(mediaForKey1);
            _mockMediaRepository.Setup(x => x.GetByVirtualKeyIdAsync(3))
                .ReturnsAsync(mediaForKey3);

            // Act
            var result = await service.GetOverallStorageStatsAsync(groupId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3000, result.TotalSizeBytes);
            Assert.Equal(2, result.TotalFiles);
            Assert.Equal(0, result.OrphanedFiles); // No orphaned files when filtering by group
            Assert.Equal(2, result.ByProvider.Count);
            Assert.Equal(1000L, result.ByProvider["replicate"]);
            Assert.Equal(2000L, result.ByProvider["openai"]);
            Assert.Equal(2, result.StorageByVirtualKey.Count);
        }

        [Fact]
        public async Task GetOverallStorageStatsAsync_WithGroupIdNoRepository_ThrowsException()
        {
            // Arrange
            const int groupId = 1;
            var service = new MediaLifecycleService(
                _mockMediaRepository.Object,
                _mockStorageService.Object,
                _mockLogger.Object,
                _mockOptions.Object,
                null // No virtual key repository
            );

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await service.GetOverallStorageStatsAsync(groupId)
            );
        }

        [Fact]
        public async Task GetOverallStorageStatsAsync_WithEmptyGroup_ReturnsEmptyStats()
        {
            // Arrange
            var mockVirtualKeyRepository = CreateMockVirtualKeyRepository();
            var service = CreateServiceWithVirtualKeyRepository(mockVirtualKeyRepository);
            
            const int groupId = 999;
            var virtualKeys = new List<VirtualKey>(); // Empty list

            mockVirtualKeyRepository.Setup(x => x.GetByVirtualKeyGroupIdAsync(groupId, default))
                .ReturnsAsync(virtualKeys);

            // Act
            var result = await service.GetOverallStorageStatsAsync(groupId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.TotalSizeBytes);
            Assert.Equal(0, result.TotalFiles);
            Assert.Equal(0, result.OrphanedFiles);
            Assert.Empty(result.ByProvider);
            Assert.Empty(result.ByMediaType);
            Assert.Empty(result.StorageByVirtualKey);
        }

        [Fact]
        public async Task GetOverallStorageStatsAsync_GroupsMediaByType()
        {
            // Arrange
            var mockVirtualKeyRepository = CreateMockVirtualKeyRepository();
            var service = CreateServiceWithVirtualKeyRepository(mockVirtualKeyRepository);
            
            var allMedia = new List<MediaRecord>
            {
                new MediaRecord { MediaType = "image", SizeBytes = 1000, VirtualKeyId = 1 },
                new MediaRecord { MediaType = "image", SizeBytes = 2000, VirtualKeyId = 1 },
                new MediaRecord { MediaType = "video", SizeBytes = 5000, VirtualKeyId = 2 },
                new MediaRecord { MediaType = "video", SizeBytes = 3000, VirtualKeyId = 2 }
            };

            _mockMediaRepository.Setup(x => x.GetMediaOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(allMedia);
            _mockMediaRepository.Setup(x => x.GetStorageStatsByProviderAsync())
                .ReturnsAsync(new Dictionary<string, long>());
            _mockMediaRepository.Setup(x => x.GetOrphanedMediaAsync())
                .ReturnsAsync(new List<MediaRecord>());

            // Act
            var result = await service.GetOverallStorageStatsAsync();

            // Assert
            Assert.Equal(2, result.ByMediaType["image"].FileCount);
            Assert.Equal(3000, result.ByMediaType["image"].SizeBytes);
            Assert.Equal(2, result.ByMediaType["video"].FileCount);
            Assert.Equal(8000, result.ByMediaType["video"].SizeBytes);
        }
    }
}