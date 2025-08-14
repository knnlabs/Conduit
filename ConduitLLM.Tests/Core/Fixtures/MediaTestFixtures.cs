using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Tests.Core.Builders;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Core.Fixtures
{
    /// <summary>
    /// Provides common test fixtures for media storage tests.
    /// </summary>
    public static class MediaStorageTestFixtures
    {
        /// <summary>
        /// Creates a mock media storage service with standard setup.
        /// </summary>
        public static Mock<IMediaStorageService> CreateMockMediaStorageService()
        {
            var mock = new Mock<IMediaStorageService>();

            // Setup default behaviors
            mock.Setup(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<MediaMetadata>(), It.IsAny<IProgress<long>>()))
                .ReturnsAsync((Stream stream, MediaMetadata metadata, IProgress<long> progress) => 
                {
                    var storageKey = TestValueFactory.CreateStorageKey(metadata.MediaType);
                    return new MediaStorageResultBuilder()
                        .WithStorageKey(storageKey)
                        .WithSizeBytes(stream.Length)
                        .WithUrl(TestValueFactory.CreateUrl(storageKey))
                        .Build();
                });

            mock.Setup(x => x.StoreVideoAsync(It.IsAny<Stream>(), It.IsAny<VideoMediaMetadata>(), It.IsAny<Action<long>>()))
                .ReturnsAsync((Stream stream, VideoMediaMetadata metadata, Action<long> callback) =>
                {
                    var storageKey = TestValueFactory.CreateStorageKey(MediaType.Video);
                    callback?.Invoke(stream.Length);
                    return new MediaStorageResultBuilder()
                        .WithStorageKey(storageKey)
                        .WithSizeBytes(stream.Length)
                        .WithUrl(TestValueFactory.CreateUrl(storageKey))
                        .Build();
                });

            mock.Setup(x => x.GetStreamAsync(It.IsAny<string>()))
                .ReturnsAsync((string key) => TestStreamFactory.CreateImageStream());

            mock.Setup(x => x.GetInfoAsync(It.IsAny<string>()))
                .ReturnsAsync((string key) => new MediaInfoBuilder()
                    .WithStorageKey(key)
                    .Build());

            mock.Setup(x => x.DeleteAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            mock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            mock.Setup(x => x.GenerateUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync((string key, TimeSpan? expiration) => TestValueFactory.CreateUrl(key));

            return mock;
        }

        /// <summary>
        /// Creates a mock HTTP client factory for testing HTTP downloads.
        /// </summary>
        public static Mock<IHttpClientFactory> CreateMockHttpClientFactory(
            string responseContent = "fake image data",
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string contentType = "image/jpeg")
        {
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var responseBytes = Encoding.UTF8.GetBytes(responseContent);

            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new ByteArrayContent(responseBytes)
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType) }
                    }
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(x => x.CreateClient()).Returns(httpClient);

            return mockFactory;
        }

        /// <summary>
        /// Creates a mock media record repository with standard setup.
        /// </summary>
        public static Mock<IMediaRecordRepository> CreateMockMediaRecordRepository()
        {
            var mock = new Mock<IMediaRecordRepository>();

            mock.Setup(x => x.CreateAsync(It.IsAny<MediaRecord>()))
                .ReturnsAsync((MediaRecord record) => record);

            mock.Setup(x => x.GetByStorageKeyAsync(It.IsAny<string>()))
                .ReturnsAsync((string key) => new MediaRecordBuilder()
                    .WithStorageKey(key)
                    .Build());

            mock.Setup(x => x.GetByVirtualKeyIdAsync(It.IsAny<int>()))
                .ReturnsAsync((int keyId) => new List<MediaRecord>
                {
                    new MediaRecordBuilder().WithVirtualKeyId(keyId).Build()
                });

            mock.Setup(x => x.DeleteAsync(It.IsAny<Guid>()))
                .ReturnsAsync(true);

            mock.Setup(x => x.UpdateAccessStatsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(true);

            mock.Setup(x => x.GetExpiredMediaAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<MediaRecord>());

            mock.Setup(x => x.GetOrphanedMediaAsync())
                .ReturnsAsync(new List<MediaRecord>());

            mock.Setup(x => x.GetMediaOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<MediaRecord>());

            return mock;
        }

        /// <summary>
        /// Creates a mock virtual key service with standard setup.
        /// </summary>
        public static Mock<ConduitLLM.Core.Interfaces.IVirtualKeyService> CreateMockVirtualKeyService()
        {
            var mock = new Mock<ConduitLLM.Core.Interfaces.IVirtualKeyService>();

            mock.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string key, string model) => new VirtualKey
                {
                    Id = 1,
                    KeyName = key,
                    IsEnabled = true
                });

            mock.Setup(x => x.UpdateSpendAsync(It.IsAny<int>(), It.IsAny<decimal>()))
                .ReturnsAsync(true);

            return mock;
        }

        /// <summary>
        /// Creates test media records for various scenarios.
        /// </summary>
        public static class MediaRecords
        {
            public static MediaRecord CreateImageRecord(int virtualKeyId = 1, string storageKey = null)
            {
                return new MediaRecordBuilder()
                    .WithVirtualKeyId(virtualKeyId)
                    .WithStorageKey(storageKey ?? TestValueFactory.CreateStorageKey(MediaType.Image))
                    .WithMediaType("image")
                    .WithContentType("image/jpeg")
                    .WithSizeBytes(1024)
                    .WithProvider("openai")
                    .WithModel("dall-e-3")
                    .WithPrompt("A beautiful landscape")
                    .Build();
            }

            public static MediaRecord CreateVideoRecord(int virtualKeyId = 1, string storageKey = null)
            {
                return new MediaRecordBuilder()
                    .WithVirtualKeyId(virtualKeyId)
                    .WithStorageKey(storageKey ?? TestValueFactory.CreateStorageKey(MediaType.Video))
                    .WithMediaType("video")
                    .WithContentType("video/mp4")
                    .WithSizeBytes(5000000)
                    .WithProvider("minimax")
                    .WithModel("minimax-video-01")
                    .WithPrompt("A short video clip")
                    .Build();
            }

            public static MediaRecord CreateExpiredRecord(int virtualKeyId = 1, DateTime? expiredAt = null)
            {
                return new MediaRecordBuilder()
                    .WithVirtualKeyId(virtualKeyId)
                    .WithStorageKey(TestValueFactory.CreateStorageKey(MediaType.Image))
                    .WithMediaType("image")
                    .WithContentType("image/jpeg")
                    .WithSizeBytes(1024)
                    .WithExpiresAt(expiredAt ?? DateTime.UtcNow.AddDays(-1))
                    .Build();
            }

            public static MediaRecord CreateOrphanedRecord(int virtualKeyId = 999)
            {
                return new MediaRecordBuilder()
                    .WithVirtualKeyId(virtualKeyId)
                    .WithStorageKey(TestValueFactory.CreateStorageKey(MediaType.Image))
                    .WithMediaType("image")
                    .WithContentType("image/jpeg")
                    .WithSizeBytes(1024)
                    .Build();
            }

            public static MediaRecord CreateOldRecord(int virtualKeyId = 1, DateTime? createdAt = null)
            {
                return new MediaRecordBuilder()
                    .WithVirtualKeyId(virtualKeyId)
                    .WithStorageKey(TestValueFactory.CreateStorageKey(MediaType.Image))
                    .WithMediaType("image")
                    .WithContentType("image/jpeg")
                    .WithSizeBytes(1024)
                    .WithCreatedAt(createdAt ?? DateTime.UtcNow.AddDays(-100))
                    .Build();
            }

            public static MediaRecord CreateRecentlyAccessedRecord(int virtualKeyId = 1, DateTime? lastAccessedAt = null)
            {
                return new MediaRecordBuilder()
                    .WithVirtualKeyId(virtualKeyId)
                    .WithStorageKey(TestValueFactory.CreateStorageKey(MediaType.Image))
                    .WithMediaType("image")
                    .WithContentType("image/jpeg")
                    .WithSizeBytes(1024)
                    .WithCreatedAt(DateTime.UtcNow.AddDays(-100))
                    .WithLastAccessedAt(lastAccessedAt ?? DateTime.UtcNow.AddDays(-10))
                    .Build();
            }
        }

        /// <summary>
        /// Creates test media metadata for various scenarios.
        /// </summary>
        public static class MediaMetadatas
        {
            public static MediaMetadata CreateImageMetadata(string fileName = "test.jpg")
            {
                return new MediaMetadataBuilder()
                    .WithContentType("image/jpeg")
                    .WithFileName(fileName)
                    .WithMediaType(MediaType.Image)
                    .WithCustomMetadata("source", "test")
                    .Build();
            }

            public static MediaMetadata CreateVideoMetadata(string fileName = "test.mp4")
            {
                return new MediaMetadataBuilder()
                    .WithContentType("video/mp4")
                    .WithFileName(fileName)
                    .WithMediaType(MediaType.Video)
                    .WithCustomMetadata("duration", "30")
                    .Build();
            }

            public static VideoMediaMetadata CreateVideoMediaMetadata(string fileName = "test.mp4")
            {
                return new VideoMediaMetadataBuilder()
                    .WithFileName(fileName)
                    .WithContentType("video/mp4")
                    .WithDuration(TimeSpan.FromSeconds(30))
                    .WithDimensions(1920, 1080)
                    .WithFrameRate(30.0)
                    .WithCodec("h264")
                    .WithBitrate(5000000)
                    .WithGeneratedByModel("test-model")
                    .WithGenerationPrompt("test prompt")
                    .Build();
            }

            public static MediaLifecycleMetadata CreateLifecycleMetadata()
            {
                return new MediaLifecycleMetadataBuilder()
                    .WithContentType("image/jpeg")
                    .WithSizeBytes(1024)
                    .WithContentHash("test-hash")
                    .WithProvider("openai")
                    .WithModel("dall-e-3")
                    .WithPrompt("A beautiful landscape")
                    .WithStorageUrl("https://storage.example.com/image.jpg")
                    .WithPublicUrl("https://cdn.example.com/image.jpg")
                    .Build();
            }
        }

        /// <summary>
        /// Creates test media info objects for various scenarios.
        /// </summary>
        public static class MediaInfos
        {
            public static MediaInfo CreateImageInfo(string storageKey = null)
            {
                return new MediaInfoBuilder()
                    .WithStorageKey(storageKey ?? TestValueFactory.CreateStorageKey(MediaType.Image))
                    .WithContentType("image/jpeg")
                    .WithSizeBytes(1024)
                    .WithFileName("test.jpg")
                    .WithMediaType(MediaType.Image)
                    .WithCustomMetadata("source", "test")
                    .Build();
            }

            public static MediaInfo CreateVideoInfo(string storageKey = null)
            {
                return new MediaInfoBuilder()
                    .WithStorageKey(storageKey ?? TestValueFactory.CreateStorageKey(MediaType.Video))
                    .WithContentType("video/mp4")
                    .WithSizeBytes(5000000)
                    .WithFileName("test.mp4")
                    .WithMediaType(MediaType.Video)
                    .WithCustomMetadata("duration", "30")
                    .Build();
            }

            public static MediaInfo CreateExpiredInfo(string storageKey = null, DateTime? expiredAt = null)
            {
                return new MediaInfoBuilder()
                    .WithStorageKey(storageKey ?? TestValueFactory.CreateStorageKey(MediaType.Image))
                    .WithContentType("image/jpeg")
                    .WithSizeBytes(1024)
                    .WithFileName("test.jpg")
                    .WithMediaType(MediaType.Image)
                    .WithExpiresAt(expiredAt ?? DateTime.UtcNow.AddDays(-1))
                    .Build();
            }
        }

        /// <summary>
        /// Creates test storage results for various scenarios.
        /// </summary>
        public static class StorageResults
        {
            public static MediaStorageResult CreateImageResult(string storageKey = null)
            {
                var key = storageKey ?? TestValueFactory.CreateStorageKey(MediaType.Image);
                return new MediaStorageResultBuilder()
                    .WithStorageKey(key)
                    .WithUrl(TestValueFactory.CreateUrl(key))
                    .WithSizeBytes(1024)
                    .WithContentHash(TestValueFactory.CreateContentHash())
                    .Build();
            }

            public static MediaStorageResult CreateVideoResult(string storageKey = null)
            {
                var key = storageKey ?? TestValueFactory.CreateStorageKey(MediaType.Video);
                return new MediaStorageResultBuilder()
                    .WithStorageKey(key)
                    .WithUrl(TestValueFactory.CreateUrl(key))
                    .WithSizeBytes(5000000)
                    .WithContentHash(TestValueFactory.CreateContentHash())
                    .Build();
            }

            public static MediaStorageResult CreateLargeFileResult(string storageKey = null, long sizeBytes = 100 * 1024 * 1024)
            {
                var key = storageKey ?? TestValueFactory.CreateStorageKey(MediaType.Video);
                return new MediaStorageResultBuilder()
                    .WithStorageKey(key)
                    .WithUrl(TestValueFactory.CreateUrl(key))
                    .WithSizeBytes(sizeBytes)
                    .WithContentHash(TestValueFactory.CreateContentHash())
                    .Build();
            }
        }

        /// <summary>
        /// Creates test streams for various scenarios.
        /// </summary>
        public static class Streams
        {
            public static MemoryStream CreateSmallImageStream()
            {
                return TestStreamFactory.CreateImageStream("small image data");
            }

            public static MemoryStream CreateLargeImageStream()
            {
                return TestStreamFactory.CreateStreamWithSize(10 * 1024 * 1024); // 10MB
            }

            public static MemoryStream CreateSmallVideoStream()
            {
                return TestStreamFactory.CreateVideoStream("small video data");
            }

            public static MemoryStream CreateLargeVideoStream()
            {
                return TestStreamFactory.CreateStreamWithSize(150 * 1024 * 1024); // 150MB
            }

            public static MemoryStream CreateBase64ImageStream()
            {
                var base64Data = TestValueFactory.CreateBase64Data("image data");
                return TestStreamFactory.CreateBase64Stream(base64Data);
            }

            public static MemoryStream CreateEmptyStream()
            {
                return new MemoryStream();
            }
        }

        /// <summary>
        /// Creates mock logger instances for testing.
        /// </summary>
        public static class Loggers
        {
            public static Mock<ILogger<T>> CreateMockLogger<T>()
            {
                return new Mock<ILogger<T>>();
            }

            public static Mock<ILogger<T>> CreateMockLoggerWithVerification<T>()
            {
                var mock = new Mock<ILogger<T>>();
                
                // Setup to allow verification of log calls
                mock.Setup(x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                    .Verifiable();
                
                return mock;
            }
        }
    }
}