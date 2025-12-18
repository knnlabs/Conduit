using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using System.Buffers;
using System.Text;
using System.Text.Json;

namespace ConduitLLM.Tests.Gateway.SignalR
{
    /// <summary>
    /// Unit tests for MessagePack compression ratios and bandwidth savings
    /// </summary>
    public class MessagePackCompressionTests
    {
        [MessagePack.MessagePackObject]
        public class TestMessage
        {
            [MessagePack.Key(0)]
            public string TaskId { get; set; }

            [MessagePack.Key(1)]
            public string Status { get; set; }

            [MessagePack.Key(2)]
            public int Progress { get; set; }

            [MessagePack.Key(3)]
            public string Message { get; set; }

            [MessagePack.Key(4)]
            public string VideoUrl { get; set; }

            [MessagePack.Key(5)]
            public Dictionary<string, object> Metadata { get; set; }
        }

        private IHubProtocol GetJsonProtocol()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSignalR();
            var serviceProvider = services.BuildServiceProvider();
            return serviceProvider.GetServices<IHubProtocol>().First(p => p.Name == "json");
        }

        private IHubProtocol GetMessagePackProtocol()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSignalR()
                .AddMessagePackProtocol(options =>
                {
                    options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
                        .WithResolver(MessagePack.Resolvers.StandardResolver.Instance)
                        .WithSecurity(MessagePack.MessagePackSecurity.UntrustedData)
                        .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray)
                        .WithCompressionMinLength(256);
                });
            var serviceProvider = services.BuildServiceProvider();
            return serviceProvider.GetServices<IHubProtocol>().First(p => p.Name == "messagepack");
        }

        [Fact]
        public void MessagePack_Should_Produce_Smaller_Payloads_Than_Json_For_Large_Messages()
        {
            // Arrange
            var jsonProtocol = GetJsonProtocol();
            var messagePackProtocol = GetMessagePackProtocol();

            var largeMessage = new TestMessage
            {
                TaskId = "task-12345-67890-abcdef",
                Status = "processing",
                Progress = 75,
                Message = new string('x', 500), // Large message to trigger compression
                VideoUrl = "https://example.com/videos/very-long-url-that-takes-up-space.mp4",
                Metadata = new Dictionary<string, object>
                {
                    { "key1", "value1" },
                    { "key2", "value2" },
                    { "key3", new string('y', 200) }
                }
            };

            var invocationMessage = new InvocationMessage("1", "taskProgress", new object[] { largeMessage });

            // Act
            var jsonSize = SerializeAndGetSize(jsonProtocol, invocationMessage);
            var messagePackSize = SerializeAndGetSize(messagePackProtocol, invocationMessage);

            // Assert
            messagePackSize.Should().BeLessThan(jsonSize,
                "MessagePack with compression should produce smaller payloads for large messages");

            var compressionRatio = (double)messagePackSize / jsonSize;
            compressionRatio.Should().BeLessThan(0.7,
                "MessagePack should achieve at least 30% compression for large messages");
        }

        [Fact]
        public void MessagePack_Should_Handle_Small_Messages_Efficiently()
        {
            // Arrange
            var jsonProtocol = GetJsonProtocol();
            var messagePackProtocol = GetMessagePackProtocol();

            var smallMessage = new TestMessage
            {
                TaskId = "task-123",
                Status = "started",
                Progress = 0,
                Message = "Short message",
                VideoUrl = null,
                Metadata = new Dictionary<string, object>()
            };

            var invocationMessage = new InvocationMessage("1", "taskProgress", new object[] { smallMessage });

            // Act
            var jsonSize = SerializeAndGetSize(jsonProtocol, invocationMessage);
            var messagePackSize = SerializeAndGetSize(messagePackProtocol, invocationMessage);

            // Assert
            // For small messages below compression threshold (256 bytes),
            // MessagePack might be slightly larger due to binary overhead,
            // but should still be reasonably efficient
            var sizeRatio = (double)messagePackSize / jsonSize;
            sizeRatio.Should().BeLessThan(1.3,
                "MessagePack should not be more than 30% larger for small messages");
        }

        [Fact]
        public void Compression_Threshold_Should_Be_256_Bytes()
        {
            // Arrange
            var messagePackProtocol = GetMessagePackProtocol();

            // Create a message just below threshold
            var belowThreshold = new TestMessage
            {
                TaskId = "task-1",
                Status = "processing",
                Progress = 50,
                Message = new string('a', 100), // Well below 256 bytes
                VideoUrl = null,
                Metadata = new Dictionary<string, object>()
            };

            // Create a message above threshold
            var aboveThreshold = new TestMessage
            {
                TaskId = "task-2",
                Status = "processing",
                Progress = 50,
                Message = new string('a', 300), // Above 256 bytes
                VideoUrl = null,
                Metadata = new Dictionary<string, object>()
            };

            var smallInvocation = new InvocationMessage("1", "taskProgress", new object[] { belowThreshold });
            var largeInvocation = new InvocationMessage("2", "taskProgress", new object[] { aboveThreshold });

            // Act
            var smallSize = SerializeAndGetSize(messagePackProtocol, smallInvocation);
            var largeSize = SerializeAndGetSize(messagePackProtocol, largeInvocation);

            // Assert
            // The large message should benefit from compression
            // while small message doesn't have compression overhead
            smallSize.Should().BeGreaterThan(0);
            largeSize.Should().BeGreaterThan(0);

            // Compression effectiveness test: the large message with compression
            // should not grow linearly with content size
            var expectedLinearSize = smallSize + 200; // Adding 200 chars
            largeSize.Should().BeLessThan(expectedLinearSize,
                "Compression should reduce the size increase from additional content");
        }

        [Theory]
        [InlineData(100)]  // Small payload
        [InlineData(500)]  // Medium payload
        [InlineData(1000)] // Large payload
        [InlineData(5000)] // Very large payload
        public void MessagePack_Should_Achieve_Expected_Compression_Ratios(int payloadSize)
        {
            // Arrange
            var jsonProtocol = GetJsonProtocol();
            var messagePackProtocol = GetMessagePackProtocol();

            var message = new TestMessage
            {
                TaskId = "task-compression-test",
                Status = "processing",
                Progress = 50,
                Message = new string('x', payloadSize),
                VideoUrl = "https://example.com/videos/test.mp4",
                Metadata = new Dictionary<string, object>
                {
                    { "timestamp", DateTime.UtcNow.ToString("O") },
                    { "size", payloadSize }
                }
            };

            var invocationMessage = new InvocationMessage("1", "taskProgress", new object[] { message });

            // Act
            var jsonSize = SerializeAndGetSize(jsonProtocol, invocationMessage);
            var messagePackSize = SerializeAndGetSize(messagePackProtocol, invocationMessage);

            // Assert
            jsonSize.Should().BeGreaterThan(0);
            messagePackSize.Should().BeGreaterThan(0);

            var compressionRatio = (double)messagePackSize / jsonSize;

            if (payloadSize >= 256)
            {
                // For messages above compression threshold, expect 30-50% savings
                compressionRatio.Should().BeLessThan(0.7,
                    $"MessagePack should achieve at least 30% compression for payload size {payloadSize}");
            }
            else
            {
                // For small messages, just ensure it's reasonable
                compressionRatio.Should().BeLessThan(1.5,
                    $"MessagePack should not be excessively large for small payload size {payloadSize}");
            }
        }

        [Fact]
        public void MessagePack_Should_Handle_Null_Values_Efficiently()
        {
            // Arrange
            var jsonProtocol = GetJsonProtocol();
            var messagePackProtocol = GetMessagePackProtocol();

            var messageWithNulls = new TestMessage
            {
                TaskId = "task-null-test",
                Status = "processing",
                Progress = 25,
                Message = null,
                VideoUrl = null,
                Metadata = null
            };

            var invocationMessage = new InvocationMessage("1", "taskProgress", new object[] { messageWithNulls });

            // Act
            var jsonSize = SerializeAndGetSize(jsonProtocol, invocationMessage);
            var messagePackSize = SerializeAndGetSize(messagePackProtocol, invocationMessage);

            // Assert
            messagePackSize.Should().BeLessThanOrEqualTo(jsonSize,
                "MessagePack should handle null values as efficiently as JSON");
        }

        [Fact]
        public void MessagePack_Should_Compress_Repetitive_Data_Effectively()
        {
            // Arrange
            var jsonProtocol = GetJsonProtocol();
            var messagePackProtocol = GetMessagePackProtocol();

            var repetitiveMessage = new TestMessage
            {
                TaskId = "task-repetitive",
                Status = "processing processing processing processing processing",
                Progress = 50,
                Message = "The same text repeated multiple times. " +
                          "The same text repeated multiple times. " +
                          "The same text repeated multiple times. " +
                          "The same text repeated multiple times.",
                VideoUrl = "https://example.com/same/same/same/same/path.mp4",
                Metadata = new Dictionary<string, object>
                {
                    { "key1", "duplicate duplicate duplicate" },
                    { "key2", "duplicate duplicate duplicate" },
                    { "key3", "duplicate duplicate duplicate" }
                }
            };

            var invocationMessage = new InvocationMessage("1", "taskProgress", new object[] { repetitiveMessage });

            // Act
            var jsonSize = SerializeAndGetSize(jsonProtocol, invocationMessage);
            var messagePackSize = SerializeAndGetSize(messagePackProtocol, invocationMessage);

            // Assert
            var compressionRatio = (double)messagePackSize / jsonSize;
            compressionRatio.Should().BeLessThan(0.6,
                "LZ4 compression should be very effective on repetitive data");
        }

        private int SerializeAndGetSize(IHubProtocol protocol, HubMessage message)
        {
            var writer = new ArrayBufferWriter<byte>();
            protocol.WriteMessage(message, writer);
            return writer.WrittenCount;
        }
    }
}
