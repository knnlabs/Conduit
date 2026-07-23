using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using System.Buffers;
using System.Collections.Generic;

namespace ConduitLLM.Benchmarks;

/// <summary>
/// Benchmarks comparing JSON vs MessagePack protocol performance for SignalR.
/// Measures serialization speed, payload size, and throughput.
/// </summary>
/// <remarks>
/// Run with: dotnet run -c Release --project ConduitLLM.Benchmarks --filter *MessagePack*
///
/// Success criteria:
/// - MessagePack payload size ≤ 60% of JSON (30-50% savings)
/// - MessagePack throughput ≥ JSON throughput
/// - MessagePack latency ≤ 110% of JSON
/// </remarks>
[MemoryDiagnoser]
[ShortRunJob]
public class SignalRMessagePackBenchmarks
{
    private IHubProtocol _jsonProtocol = null!;
    private IHubProtocol _messagePackProtocol = null!;

    private HubMessage _smallMessage = null!;
    private HubMessage _mediumMessage = null!;
    private HubMessage _largeMessage = null!;
    private HubMessage _veryLargeMessage = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup JSON protocol
        var jsonServices = new ServiceCollection();
        jsonServices.AddLogging();
        jsonServices.AddSignalR();
        var jsonProvider = jsonServices.BuildServiceProvider();
        _jsonProtocol = jsonProvider.GetServices<IHubProtocol>().First(p => p.Name == "json");

        // Setup MessagePack protocol
        var messagePackServices = new ServiceCollection();
        messagePackServices.AddLogging();
        messagePackServices.AddSignalR()
            .AddMessagePackProtocol(options =>
            {
                options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
                    .WithResolver(MessagePack.Resolvers.StandardResolver.Instance)
                    .WithSecurity(MessagePack.MessagePackSecurity.UntrustedData)
                    .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray)
                    .WithCompressionMinLength(256);
            });
        var messagePackProvider = messagePackServices.BuildServiceProvider();
        _messagePackProtocol = messagePackProvider.GetServices<IHubProtocol>().First(p => p.Name == "messagepack");

        // Create test messages of various sizes
        _smallMessage = new InvocationMessage("1", "taskProgress", new object[]
        {
            new
            {
                TaskId = "task-123",
                Status = "processing",
                Progress = 50
            }
        });

        _mediumMessage = new InvocationMessage("2", "taskProgress", new object[]
        {
            new
            {
                TaskId = "task-456-789-abc",
                Status = "processing video generation",
                Progress = 75,
                Message = new string('x', 300), // Above compression threshold
                VideoUrl = "https://example.com/videos/test-video-url.mp4",
                Metadata = new Dictionary<string, object>
                {
                    { "duration", "5.2s" },
                    { "resolution", "1920x1080" },
                    { "codec", "h264" }
                }
            }
        });

        _largeMessage = new InvocationMessage("3", "taskProgress", new object[]
        {
            new
            {
                TaskId = "task-large-payload-test",
                Status = "processing large content",
                Progress = 85,
                Message = new string('y', 1000),
                VideoUrl = "https://example.com/videos/very-long-video-url-with-many-parameters.mp4?token=abc123&quality=hd",
                Metadata = new Dictionary<string, object>
                {
                    { "timestamp", "2025-12-18T00:00:00Z" },
                    { "chunks", new List<string> { "chunk1", "chunk2", "chunk3", "chunk4", "chunk5" } },
                    { "statistics", new Dictionary<string, int> { { "frames", 1200 }, { "bitrate", 5000 } } }
                }
            }
        });

        _veryLargeMessage = new InvocationMessage("4", "taskProgress", new object[]
        {
            new
            {
                TaskId = "task-very-large-payload",
                Status = "processing very large content with lots of repeated data",
                Progress = 95,
                Message = string.Join(" ", Enumerable.Repeat("This is repeated content that should compress well.", 100)),
                VideoUrl = "https://example.com/videos/ultra-high-quality-4k-video-with-extremely-long-url.mp4",
                Metadata = new Dictionary<string, object>
                {
                    { "frames", Enumerable.Range(1, 100).Select(i => new { frame = i, data = $"frame_{i}" }).ToList() }
                }
            }
        });
    }

    #region Serialization Speed Benchmarks

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SmallPayload")]
    public int Json_Serialize_SmallPayload()
    {
        var writer = new ArrayBufferWriter<byte>();
        _jsonProtocol.WriteMessage(_smallMessage, writer);
        return writer.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("SmallPayload")]
    public int MessagePack_Serialize_SmallPayload()
    {
        var writer = new ArrayBufferWriter<byte>();
        _messagePackProtocol.WriteMessage(_smallMessage, writer);
        return writer.WrittenCount;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MediumPayload")]
    public int Json_Serialize_MediumPayload()
    {
        var writer = new ArrayBufferWriter<byte>();
        _jsonProtocol.WriteMessage(_mediumMessage, writer);
        return writer.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("MediumPayload")]
    public int MessagePack_Serialize_MediumPayload()
    {
        var writer = new ArrayBufferWriter<byte>();
        _messagePackProtocol.WriteMessage(_mediumMessage, writer);
        return writer.WrittenCount;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LargePayload")]
    public int Json_Serialize_LargePayload()
    {
        var writer = new ArrayBufferWriter<byte>();
        _jsonProtocol.WriteMessage(_largeMessage, writer);
        return writer.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("LargePayload")]
    public int MessagePack_Serialize_LargePayload()
    {
        var writer = new ArrayBufferWriter<byte>();
        _messagePackProtocol.WriteMessage(_largeMessage, writer);
        return writer.WrittenCount;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("VeryLargePayload")]
    public int Json_Serialize_VeryLargePayload()
    {
        var writer = new ArrayBufferWriter<byte>();
        _jsonProtocol.WriteMessage(_veryLargeMessage, writer);
        return writer.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("VeryLargePayload")]
    public int MessagePack_Serialize_VeryLargePayload()
    {
        var writer = new ArrayBufferWriter<byte>();
        _messagePackProtocol.WriteMessage(_veryLargeMessage, writer);
        return writer.WrittenCount;
    }

    #endregion

    #region Payload Size Benchmarks

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MediumPayloadSize")]
    public int Json_PayloadSize_Medium()
    {
        var writer = new ArrayBufferWriter<byte>();
        _jsonProtocol.WriteMessage(_mediumMessage, writer);
        return writer.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("MediumPayloadSize")]
    public int MessagePack_PayloadSize_Medium()
    {
        var writer = new ArrayBufferWriter<byte>();
        _messagePackProtocol.WriteMessage(_mediumMessage, writer);
        return writer.WrittenCount;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LargePayloadSize")]
    public int Json_PayloadSize_Large()
    {
        var writer = new ArrayBufferWriter<byte>();
        _jsonProtocol.WriteMessage(_largeMessage, writer);
        return writer.WrittenCount;
    }

    [Benchmark]
    [BenchmarkCategory("LargePayloadSize")]
    public int MessagePack_PayloadSize_Large()
    {
        var writer = new ArrayBufferWriter<byte>();
        _messagePackProtocol.WriteMessage(_largeMessage, writer);
        return writer.WrittenCount;
    }

    #endregion

    #region Throughput Benchmarks (messages per second)

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Throughput100")]
    public void Json_Throughput_100Messages()
    {
        for (int i = 0; i < 100; i++)
        {
            var writer = new ArrayBufferWriter<byte>();
            _jsonProtocol.WriteMessage(_mediumMessage, writer);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Throughput100")]
    public void MessagePack_Throughput_100Messages()
    {
        for (int i = 0; i < 100; i++)
        {
            var writer = new ArrayBufferWriter<byte>();
            _messagePackProtocol.WriteMessage(_mediumMessage, writer);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Throughput1000")]
    public void Json_Throughput_1000Messages()
    {
        for (int i = 0; i < 1000; i++)
        {
            var writer = new ArrayBufferWriter<byte>();
            _jsonProtocol.WriteMessage(_smallMessage, writer);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Throughput1000")]
    public void MessagePack_Throughput_1000Messages()
    {
        for (int i = 0; i < 1000; i++)
        {
            var writer = new ArrayBufferWriter<byte>();
            _messagePackProtocol.WriteMessage(_smallMessage, writer);
        }
    }

    #endregion

    #region Memory Allocation Benchmarks

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Memory")]
    public byte[] Json_Memory_Allocations()
    {
        var writer = new ArrayBufferWriter<byte>();
        _jsonProtocol.WriteMessage(_mediumMessage, writer);
        return writer.WrittenMemory.ToArray();
    }

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public byte[] MessagePack_Memory_Allocations()
    {
        var writer = new ArrayBufferWriter<byte>();
        _messagePackProtocol.WriteMessage(_mediumMessage, writer);
        return writer.WrittenMemory.ToArray();
    }

    #endregion

    #region Compression Ratio Benchmarks

    [Benchmark]
    [BenchmarkCategory("CompressionRatio")]
    public (int JsonSize, int MessagePackSize, double Ratio) CompressionRatio_RepetitiveData()
    {
        // Create message with highly repetitive data that compresses well
        var repetitiveMessage = new InvocationMessage("1", "test", new object[]
        {
            new
            {
                Data = string.Join(" ", Enumerable.Repeat("REPEAT", 200))
            }
        });

        var jsonWriter = new ArrayBufferWriter<byte>();
        _jsonProtocol.WriteMessage(repetitiveMessage, jsonWriter);
        var jsonSize = jsonWriter.WrittenCount;

        var msgPackWriter = new ArrayBufferWriter<byte>();
        _messagePackProtocol.WriteMessage(repetitiveMessage, msgPackWriter);
        var msgPackSize = msgPackWriter.WrittenCount;

        var ratio = (double)msgPackSize / jsonSize;
        return (jsonSize, msgPackSize, ratio);
    }

    [Benchmark]
    [BenchmarkCategory("CompressionRatio")]
    public (int JsonSize, int MessagePackSize, double Ratio) CompressionRatio_RealWorldData()
    {
        var jsonWriter = new ArrayBufferWriter<byte>();
        _jsonProtocol.WriteMessage(_largeMessage, jsonWriter);
        var jsonSize = jsonWriter.WrittenCount;

        var msgPackWriter = new ArrayBufferWriter<byte>();
        _messagePackProtocol.WriteMessage(_largeMessage, msgPackWriter);
        var msgPackSize = msgPackWriter.WrittenCount;

        var ratio = (double)msgPackSize / jsonSize;
        return (jsonSize, msgPackSize, ratio);
    }

    #endregion
}
