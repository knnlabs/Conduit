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
        #region Utility Method Tests

        [Fact]
        public async Task GetTotalSizeBytes_WithStoredFiles_ShouldReturnCorrectTotal()
        {
            // Arrange
            var content1 = new MemoryStream(Encoding.UTF8.GetBytes("file1"));
            var content2 = new MemoryStream(Encoding.UTF8.GetBytes("file2data"));
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                FileName = "test.txt",
                MediaType = MediaType.Other
            };

            // Act
            await _service.StoreAsync(content1, metadata);
            await _service.StoreAsync(content2, metadata);

            var totalSize = _service.GetTotalSizeBytes();

            // Assert
            Assert.Equal(14, totalSize); // 5 + 9 bytes
        }

        [Fact]
        public async Task GetItemCount_WithStoredFiles_ShouldReturnCorrectCount()
        {
            // Arrange
            var content1 = new MemoryStream(Encoding.UTF8.GetBytes("file1"));
            var content2 = new MemoryStream(Encoding.UTF8.GetBytes("file2"));
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                FileName = "test.txt",
                MediaType = MediaType.Other
            };

            // Act
            await _service.StoreAsync(content1, metadata);
            await _service.StoreAsync(content2, metadata);

            var itemCount = _service.GetItemCount();

            // Assert
            Assert.Equal(2, itemCount);
        }

        [Fact]
        public async Task GetItemCount_AfterDeletion_ShouldReturnCorrectCount()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("file"));
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                FileName = "test.txt",
                MediaType = MediaType.Other
            };

            // Act
            var storeResult = await _service.StoreAsync(content, metadata);
            var initialCount = _service.GetItemCount();
            await _service.DeleteAsync(storeResult.StorageKey);
            var finalCount = _service.GetItemCount();

            // Assert
            Assert.Equal(1, initialCount);
            Assert.Equal(0, finalCount);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public async Task Constructor_WithCustomBaseUrl_ShouldTrimTrailingSlash()
        {
            // Arrange
            var baseUrlWithSlash = "http://localhost:8080/";
            var service = new InMemoryMediaStorageService(_mockLogger.Object, baseUrlWithSlash);

            // Act
            var url = await service.GenerateUrlAsync("test/key");

            // Assert
            Assert.Equal("http://localhost:8080/v1/media/test/key", url);
        }

        [Fact]
        public async Task Constructor_WithDefaultBaseUrl_ShouldUseDefault()
        {
            // Arrange & Act
            var service = new InMemoryMediaStorageService(_mockLogger.Object);
            var url = await service.GenerateUrlAsync("test/key");

            // Assert
            Assert.Equal("http://localhost:5000/v1/media/test/key", url);
        }

        #endregion
    }
}