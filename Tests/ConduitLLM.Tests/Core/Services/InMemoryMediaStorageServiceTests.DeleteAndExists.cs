using System.Text;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class InMemoryMediaStorageServiceTests
    {
        #region DeleteAsync Tests

        [Fact]
        public async Task DeleteAsync_WithExistingKey_ShouldDeleteAndReturnTrue()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test data"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image
            };

            var storeResult = await _service.StoreAsync(content, metadata);

            // Act
            var deleted = await _service.DeleteAsync(storeResult.StorageKey);

            // Assert
            Assert.True(deleted);
            
            // Verify file is actually deleted
            var stream = await _service.GetStreamAsync(storeResult.StorageKey);
            Assert.Null(stream);
        }

        [Fact]
        public async Task DeleteAsync_WithNonExistentKey_ShouldReturnFalse()
        {
            // Act
            var deleted = await _service.DeleteAsync("non-existent-key");

            // Assert
            Assert.False(deleted);
        }

        [Fact]
        public async Task DeleteAsync_WithNullKey_ShouldReturnFalse()
        {
            // Act
            var deleted = await _service.DeleteAsync(null);

            // Assert
            Assert.False(deleted);
        }

        [Fact]
        public async Task DeleteManyAsync_ReportsOutcomeForEachKey()
        {
            var first = await _service.StoreAsync(
                new MemoryStream(Encoding.UTF8.GetBytes("first")),
                new MediaMetadata
                {
                    ContentType = "image/jpeg",
                    FileName = "first.jpg",
                    MediaType = MediaType.Image
                });
            var second = await _service.StoreAsync(
                new MemoryStream(Encoding.UTF8.GetBytes("second")),
                new MediaMetadata
                {
                    ContentType = "image/jpeg",
                    FileName = "second.jpg",
                    MediaType = MediaType.Image
                });

            var result = await _service.DeleteManyAsync(
                [first.StorageKey, second.StorageKey, "missing"]);

            Assert.Equal(3, result.Items.Count);
            Assert.True(result.Items.Single(item =>
                item.StorageKey == first.StorageKey).Deleted);
            Assert.True(result.Items.Single(item =>
                item.StorageKey == second.StorageKey).Deleted);
            Assert.Equal(
                "not_found",
                result.Items.Single(item => item.StorageKey == "missing").ErrorCode);
        }

        #endregion

        #region GenerateUrlAsync Tests

        [Fact]
        public async Task GenerateUrlAsync_WithValidKey_ShouldReturnCorrectUrl()
        {
            // Arrange
            var storageKey = "image/test-hash.jpg";

            // Act
            var url = await _service.GenerateUrlAsync(storageKey);

            // Assert
            Assert.Equal($"{TestBaseUrl}/v1/conduit/media/{storageKey}", url);
        }

        [Fact]
        public async Task GenerateUrlAsync_WithExpiration_ShouldReturnUrl()
        {
            // Arrange
            var storageKey = "video/test-hash.mp4";
            var expiration = TimeSpan.FromHours(1);

            // Act
            var url = await _service.GenerateUrlAsync(storageKey, expiration);

            // Assert
            Assert.Equal($"{TestBaseUrl}/v1/conduit/media/{storageKey}", url);
            // Note: In-memory storage doesn't use expiration, but method should still work
        }

        #endregion

        #region ExistsAsync Tests

        [Fact]
        public async Task ExistsAsync_WithExistingKey_ShouldReturnTrue()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test data"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image
            };

            var storeResult = await _service.StoreAsync(content, metadata);

            // Act
            var exists = await _service.ExistsAsync(storeResult.StorageKey);

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task ExistsAsync_WithNonExistentKey_ShouldReturnFalse()
        {
            // Act
            var exists = await _service.ExistsAsync("non-existent-key");

            // Assert
            Assert.False(exists);
        }

        [Fact]
        public async Task ExistsAsync_WithDeletedKey_ShouldReturnFalse()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test data"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image
            };

            var storeResult = await _service.StoreAsync(content, metadata);
            await _service.DeleteAsync(storeResult.StorageKey);

            // Act
            var exists = await _service.ExistsAsync(storeResult.StorageKey);

            // Assert
            Assert.False(exists);
        }

        #endregion
    }
}
