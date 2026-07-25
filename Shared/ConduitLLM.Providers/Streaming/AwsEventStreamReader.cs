using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace ConduitLLM.Providers.Streaming
{
    /// <summary>
    /// A single decoded message from an AWS <c>application/vnd.amazon.eventstream</c> response.
    /// </summary>
    public sealed record AwsEventStreamMessage(
        IReadOnlyDictionary<string, string> Headers,
        byte[] Payload)
    {
        /// <summary>The <c>:message-type</c> header (<c>event</c> or <c>exception</c>), if present.</summary>
        public string? MessageType => Headers.GetValueOrDefault(":message-type");

        /// <summary>The <c>:event-type</c> header naming the event (for example <c>contentBlockDelta</c>).</summary>
        public string? EventType => Headers.GetValueOrDefault(":event-type");

        /// <summary>The <c>:exception-type</c> header naming the modeled exception, if this message is one.</summary>
        public string? ExceptionType => Headers.GetValueOrDefault(":exception-type");

        /// <summary>The payload decoded as UTF-8 text (event payloads are JSON documents).</summary>
        public string PayloadText => Encoding.UTF8.GetString(Payload);
    }

    /// <summary>
    /// Decodes the AWS event-stream binary framing (<c>application/vnd.amazon.eventstream</c>) used
    /// by streaming Bedrock APIs such as <c>ConverseStream</c>. Unlike SSE there is no textual
    /// delimiter: each message is a length-prefixed frame with CRC-protected prelude and body.
    /// </summary>
    /// <remarks>
    /// Frame layout: 4-byte big-endian total length, 4-byte headers length, 4-byte CRC32 of those
    /// 8 bytes, the headers block, the payload, and a trailing 4-byte CRC32 of everything before it.
    /// Each header is a length-prefixed name, a value-type byte, and a type-dependent value.
    /// Both CRCs are validated; a mismatch means the stream is corrupt and raises
    /// <see cref="InvalidDataException"/> rather than yielding garbled events.
    /// </remarks>
    public static class AwsEventStreamReader
    {
        private const int PreludeLength = 12;
        private const int MaxFrameLength = 16 * 1024 * 1024;

        /// <summary>
        /// Reads and decodes messages from the stream until it ends.
        /// </summary>
        /// <param name="stream">The raw response stream positioned at the first frame.</param>
        /// <param name="cancellationToken">A token to cancel the read.</param>
        /// <returns>The decoded messages in stream order.</returns>
        /// <exception cref="InvalidDataException">Thrown when a frame is malformed or fails CRC validation.</exception>
        public static async IAsyncEnumerable<AwsEventStreamMessage> ReadMessagesAsync(
            Stream stream,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var prelude = new byte[PreludeLength];
            while (true)
            {
                var read = await ReadBlockAsync(stream, prelude, cancellationToken);
                if (read == 0)
                {
                    yield break;
                }
                if (read < PreludeLength)
                {
                    throw new InvalidDataException("AWS event stream ended mid-frame while reading the prelude.");
                }

                var totalLength = BinaryPrimitives.ReadUInt32BigEndian(prelude.AsSpan(0, 4));
                var headersLength = BinaryPrimitives.ReadUInt32BigEndian(prelude.AsSpan(4, 4));
                var preludeCrc = BinaryPrimitives.ReadUInt32BigEndian(prelude.AsSpan(8, 4));

                if (Crc32.Compute(prelude.AsSpan(0, 8)) != preludeCrc)
                {
                    throw new InvalidDataException("AWS event stream prelude failed CRC validation.");
                }
                if (totalLength < PreludeLength + 4 || totalLength > MaxFrameLength
                    || headersLength > totalLength - PreludeLength - 4)
                {
                    throw new InvalidDataException(
                        $"AWS event stream frame has inconsistent lengths (total {totalLength}, headers {headersLength}).");
                }

                var remainder = new byte[totalLength - PreludeLength];
                if (await ReadBlockAsync(stream, remainder, cancellationToken) < remainder.Length)
                {
                    throw new InvalidDataException("AWS event stream ended mid-frame while reading the body.");
                }

                var messageCrc = BinaryPrimitives.ReadUInt32BigEndian(remainder.AsSpan(remainder.Length - 4, 4));
                var crc = Crc32.Append(Crc32.Compute(prelude), remainder.AsSpan(0, remainder.Length - 4));
                if (crc != messageCrc)
                {
                    throw new InvalidDataException("AWS event stream message failed CRC validation.");
                }

                var headers = ParseHeaders(remainder.AsSpan(0, (int)headersLength));
                var payload = remainder.AsSpan((int)headersLength, remainder.Length - (int)headersLength - 4).ToArray();
                yield return new AwsEventStreamMessage(headers, payload);
            }
        }

        private static async Task<int> ReadBlockAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
                if (read == 0)
                {
                    break;
                }
                offset += read;
            }

            return offset;
        }

        private static IReadOnlyDictionary<string, string> ParseHeaders(ReadOnlySpan<byte> block)
        {
            var headers = new Dictionary<string, string>(StringComparer.Ordinal);
            var offset = 0;
            while (offset < block.Length)
            {
                var nameLength = block[offset];
                offset += 1;
                RequireBytes(block, offset, nameLength);
                var name = Encoding.UTF8.GetString(block.Slice(offset, nameLength));
                offset += nameLength;

                RequireBytes(block, offset, 1);
                var valueType = block[offset];
                offset += 1;

                string value;
                switch (valueType)
                {
                    case 0: // bool true
                        value = "true";
                        break;
                    case 1: // bool false
                        value = "false";
                        break;
                    case 2: // byte
                        RequireBytes(block, offset, 1);
                        value = ((sbyte)block[offset]).ToString();
                        offset += 1;
                        break;
                    case 3: // int16
                        RequireBytes(block, offset, 2);
                        value = BinaryPrimitives.ReadInt16BigEndian(block.Slice(offset, 2)).ToString();
                        offset += 2;
                        break;
                    case 4: // int32
                        RequireBytes(block, offset, 4);
                        value = BinaryPrimitives.ReadInt32BigEndian(block.Slice(offset, 4)).ToString();
                        offset += 4;
                        break;
                    case 5: // int64
                    case 8: // timestamp (millis since epoch)
                        RequireBytes(block, offset, 8);
                        value = BinaryPrimitives.ReadInt64BigEndian(block.Slice(offset, 8)).ToString();
                        offset += 8;
                        break;
                    case 6: // byte array
                    case 7: // string
                        RequireBytes(block, offset, 2);
                        var length = BinaryPrimitives.ReadUInt16BigEndian(block.Slice(offset, 2));
                        offset += 2;
                        RequireBytes(block, offset, length);
                        value = valueType == 7
                            ? Encoding.UTF8.GetString(block.Slice(offset, length))
                            : Convert.ToBase64String(block.Slice(offset, length));
                        offset += length;
                        break;
                    case 9: // uuid
                        RequireBytes(block, offset, 16);
                        value = Convert.ToHexString(block.Slice(offset, 16)).ToLowerInvariant();
                        offset += 16;
                        break;
                    default:
                        throw new InvalidDataException($"AWS event stream header '{name}' has unknown value type {valueType}.");
                }

                headers[name] = value;
            }

            return headers;
        }

        private static void RequireBytes(ReadOnlySpan<byte> block, int offset, int count)
        {
            if (offset + count > block.Length)
            {
                throw new InvalidDataException("AWS event stream header block is truncated.");
            }
        }

        /// <summary>
        /// CRC-32 (IEEE 802.3, reflected polynomial 0xEDB88320) as used by the AWS event-stream
        /// framing. Implemented locally to avoid a package dependency for 20 lines of table lookup.
        /// </summary>
        internal static class Crc32
        {
            private static readonly uint[] Table = BuildTable();

            private static uint[] BuildTable()
            {
                var table = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    var crc = i;
                    for (var bit = 0; bit < 8; bit++)
                    {
                        crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                    }
                    table[i] = crc;
                }
                return table;
            }

            public static uint Compute(ReadOnlySpan<byte> data) => Append(0, data);

            public static uint Append(uint crc, ReadOnlySpan<byte> data)
            {
                var value = ~crc;
                foreach (var b in data)
                {
                    value = (value >> 8) ^ Table[(value ^ b) & 0xFF];
                }
                return ~value;
            }
        }
    }
}
