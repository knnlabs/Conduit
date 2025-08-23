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
            Assert.Equal($"{TestBaseUrl}/v1/media/{storageKey}", url);
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
            Assert.Equal($"{TestBaseUrl}/v1/media/{storageKey}", url);
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