using System;
using System.IO;
using System.Text;

namespace ConduitLLM.Tests.Core.Builders
{
    /// <summary>
    /// Factory for creating common test streams.
    /// </summary>
    public static class TestStreamFactory
    {
        /// <summary>
        /// Creates a memory stream with test image data.
        /// </summary>
        public static MemoryStream CreateImageStream(string content = "fake image data")
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        /// <summary>
        /// Creates a memory stream with test video data.
        /// </summary>
        public static MemoryStream CreateVideoStream(string content = "fake video data")
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        /// <summary>
        /// Creates a memory stream with test audio data.
        /// </summary>
        public static MemoryStream CreateAudioStream(string content = "fake audio data")
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        /// <summary>
        /// Creates a memory stream with specified size.
        /// </summary>
        public static MemoryStream CreateStreamWithSize(int sizeBytes)
        {
            var data = new byte[sizeBytes];
            for (int i = 0; i < sizeBytes; i++)
            {
                data[i] = (byte)(i % 256);
            }
            return new MemoryStream(data);
        }

        /// <summary>
        /// Creates a memory stream with base64 encoded data.
        /// </summary>
        public static MemoryStream CreateBase64Stream(string base64Data)
        {
            var bytes = Convert.FromBase64String(base64Data);
            return new MemoryStream(bytes);
        }
    }
}