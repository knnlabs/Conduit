using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class InMemoryMediaStorageServiceTests
    {
        #region GetStreamAsync Tests

        [Fact]
        public async Task GetStreamAsync_WithExistingKey_ShouldReturnStream()
        {
            // Arrange
            var testData = "test data";
            var content = new MemoryStream(Encoding.UTF8.GetBytes(testData));
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                FileName = "test.txt",
                MediaType = MediaType.Other
            };

            var storeResult = await _service.StoreAsync(content, metadata);

            // Act
            var stream = await _service.GetStreamAsync(storeResult.StorageKey);

            // Assert
            Assert.NotNull(stream);
            using var reader = new StreamReader(stream);
            var retrievedData = await reader.ReadToEndAsync();
            Assert.Equal(testData, retrievedData);
        }

        [Fact]
        public async Task GetStreamAsync_WithNonExistentKey_ShouldReturnNull()
        {
            // Act
            var stream = await _service.GetStreamAsync("non-existent-key");

            // Assert
            Assert.Null(stream);
        }

        [Fact]
        public async Task GetStreamAsync_WithNullKey_ShouldReturnNull()
        {
            // Act
            var stream = await _service.GetStreamAsync(null);

            // Assert
            Assert.Null(stream);
        }

        [Fact]
        public async Task GetStreamAsync_WithEmptyKey_ShouldReturnNull()
        {
            // Act
            var stream = await _service.GetStreamAsync("");

            // Assert
            Assert.Null(stream);
        }

        #endregion

        #region GetInfoAsync Tests

        [Fact]
        public async Task GetInfoAsync_WithExistingKey_ShouldReturnMediaInfo()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test data"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                CustomMetadata = new Dictionary<string, string> { ["test"] = "value" }
            };

            var storeResult = await _service.StoreAsync(content, metadata);

            // Act
            var info = await _service.GetInfoAsync(storeResult.StorageKey);

            // Assert
            Assert.NotNull(info);
            Assert.Equal(storeResult.StorageKey, info.StorageKey);
            Assert.Equal("image/jpeg", info.ContentType);
            Assert.Equal("test.jpg", info.FileName);
            Assert.Equal(MediaType.Image, info.MediaType);
            Assert.Equal(9, info.SizeBytes); // "test data" length
            Assert.NotNull(info.ExpiresAt);
            Assert.Contains("test", info.CustomMetadata.Keys);
            Assert.Equal("value", info.CustomMetadata["test"]);
        }

        [Fact]
        public async Task GetInfoAsync_WithNonExistentKey_ShouldReturnNull()
        {
            // Act
            var info = await _service.GetInfoAsync("non-existent-key");

            // Assert
            Assert.Null(info);
        }

        [Fact]
        public async Task GetInfoAsync_ShouldReturnCopyOfMetadata()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image,
                CustomMetadata = new Dictionary<string, string> { ["test"] = "value" }
            };

            var storeResult = await _service.StoreAsync(content, metadata);

            // Act
            var info1 = await _service.GetInfoAsync(storeResult.StorageKey);
            var info2 = await _service.GetInfoAsync(storeResult.StorageKey);

            // Assert
            Assert.NotSame(info1, info2); // Different instances
            Assert.NotSame(info1.CustomMetadata, info2.CustomMetadata); // Different dictionaries
            Assert.Equal(info1.StorageKey, info2.StorageKey);
        }

        #endregion
    }
}