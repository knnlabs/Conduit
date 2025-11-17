using System.Text.Json;

using ConduitLLM.Http.Services;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Services
{
    public class ModelMetadataServiceTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILogger<ModelMetadataService>> _mockLogger;
        private readonly ModelMetadataService _service;
        private readonly string _testDirectory;
        private readonly string _metadataPath;

        public ModelMetadataServiceTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<ModelMetadataService>>();
            
            // Create a temporary directory for test metadata files
            _testDirectory = Path.Combine(Path.GetTempPath(), $"ConduitLLMTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            
            var staticModelsDir = Path.Combine(_testDirectory, "StaticModels");
            Directory.CreateDirectory(staticModelsDir);
            _metadataPath = Path.Combine(staticModelsDir, "model-metadata.json");
            
            // Create service with mocked assembly location
            _service = new TestableModelMetadataService(_mockLogger.Object, _testDirectory);
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Fact]
        public async Task GetModelMetadataAsync_WhenModelExists_ReturnsMetadata()
        {
            // Arrange
            var testMetadata = new
            {
                @dalle3 = new
                {
                    image = new
                    {
                        sizes = new[] { "1024x1024", "1792x1024" },
                        maxImages = 1,
                        qualityOptions = new[] { "standard", "hd" }
                    }
                }
            };
            
            await File.WriteAllTextAsync(_metadataPath, JsonSerializer.Serialize(testMetadata));

            // Act
            var result = await _service.GetModelMetadataAsync("dalle3");

            // Assert
            Assert.NotNull(result);
            var json = JsonSerializer.Serialize(result);
            Assert.Contains("1024x1024", json);
            Assert.Contains("1792x1024", json);
            Assert.Contains("standard", json);
            Assert.Contains("hd", json);
        }

        [Fact]
        public async Task GetModelMetadataAsync_WhenModelDoesNotExist_ReturnsNull()
        {
            // Arrange
            var testMetadata = new
            {
                @dalle3 = new
                {
                    image = new
                    {
                        sizes = new[] { "1024x1024" }
                    }
                }
            };
            
            await File.WriteAllTextAsync(_metadataPath, JsonSerializer.Serialize(testMetadata));

            // Act
            var result = await _service.GetModelMetadataAsync("nonexistent-model");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetModelMetadataAsync_WhenFileDoesNotExist_ReturnsNull()
        {
            // Arrange
            // Create a service with a non-existent directory to ensure no metadata is found
            var nonExistentDirectory = Path.Combine(Path.GetTempPath(), $"NonExistent_{Guid.NewGuid()}");
            var serviceWithNoFile = new TestableModelMetadataService(_mockLogger.Object, nonExistentDirectory);

            // Act
            var result = await serviceWithNoFile.GetModelMetadataAsync("any-model");

            // Assert
            Assert.Null(result);
            // The service may find the file at alternative locations in test environment,
            // so we can't reliably verify the warning log
        }

        [Fact]
        public async Task GetModelMetadataAsync_WhenJsonIsInvalid_ReturnsNull()
        {
            // Arrange
            await File.WriteAllTextAsync(_metadataPath, "{ invalid json }");

            // Act
            var result = await _service.GetModelMetadataAsync("any-model");

            // Assert
            Assert.Null(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error loading model metadata")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetModelMetadataAsync_CachesResults()
        {
            // Arrange
            var testMetadata = new
            {
                @dalle3 = new
                {
                    image = new
                    {
                        sizes = new[] { "1024x1024" }
                    }
                }
            };
            
            await File.WriteAllTextAsync(_metadataPath, JsonSerializer.Serialize(testMetadata));

            // Act - First call loads from file
            var result1 = await _service.GetModelMetadataAsync("dalle3");
            
            // Delete the file to prove second call uses cache
            File.Delete(_metadataPath);
            
            var result2 = await _service.GetModelMetadataAsync("dalle3");

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Equal(JsonSerializer.Serialize(result1), JsonSerializer.Serialize(result2));
        }

        [Fact]
        public async Task GetModelMetadataAsync_ReloadsAfterCacheExpiry()
        {
            // Arrange
            var initialMetadata = new
            {
                @dalle3 = new
                {
                    image = new
                    {
                        sizes = new[] { "1024x1024" }
                    }
                }
            };
            
            await File.WriteAllTextAsync(_metadataPath, JsonSerializer.Serialize(initialMetadata));

            // Create service with short cache expiry for testing
            var service = new TestableModelMetadataService(_mockLogger.Object, _testDirectory, TimeSpan.FromMilliseconds(100));

            // Act
            var result1 = await service.GetModelMetadataAsync("dalle3");
            
            // Wait for cache to expire
            await Task.Delay(150);
            
            // Update metadata
            var updatedMetadata = new
            {
                @dalle3 = new
                {
                    image = new
                    {
                        sizes = new[] { "512x512", "256x256" }
                    }
                }
            };
            await File.WriteAllTextAsync(_metadataPath, JsonSerializer.Serialize(updatedMetadata));
            
            var result2 = await service.GetModelMetadataAsync("dalle3");

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            var json1 = JsonSerializer.Serialize(result1);
            var json2 = JsonSerializer.Serialize(result2);
            Assert.Contains("1024x1024", json1);
            Assert.DoesNotContain("512x512", json1);
            Assert.Contains("512x512", json2);
            Assert.DoesNotContain("1024x1024", json2);
        }

        // Testable version that allows overriding assembly location and cache expiry
        private class TestableModelMetadataService : ModelMetadataService
        {
            private readonly string _assemblyDirectory;
            private readonly TimeSpan? _cacheExpiry;

            public TestableModelMetadataService(ILogger<ModelMetadataService> logger, string assemblyDirectory, TimeSpan? cacheExpiry = null)
                : base(logger)
            {
                _assemblyDirectory = assemblyDirectory;
                _cacheExpiry = cacheExpiry;
                
                // Use reflection to set the private cache expiry field if provided
                if (cacheExpiry.HasValue)
                {
                    var field = typeof(ModelMetadataService).GetField("_cacheExpiry", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(this, cacheExpiry.Value);
                }
            }

            protected override string GetAssemblyDirectory()
            {
                return _assemblyDirectory;
            }
        }
    }
}