using System.Text;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public class ImageTokenCalculatorTests
    {
        private readonly Mock<ILogger<ImageTokenCalculator>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly ImageTokenCalculator _calculator;

        public ImageTokenCalculatorTests()
        {
            _mockLogger = new Mock<ILogger<ImageTokenCalculator>>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _calculator = new ImageTokenCalculator(_mockLogger.Object, _httpClient);
        }

        [Fact]
        public async Task CalculateImageTokensAsync_LowDetail_ReturnsFixedTokenCount()
        {
            // Arrange
            var imageUrl = new ImageUrl 
            { 
                Url = "https://example.com/image.jpg",
                Detail = "low"
            };

            // Act
            var tokens = await _calculator.CalculateImageTokensAsync(imageUrl);

            // Assert
            Assert.Equal(ImageTokenCalculator.LowDetailTokens, tokens);
            Assert.Equal(85, tokens);
        }

        [Fact]
        public async Task CalculateImageTokensAsync_HighDetail_512x512_ReturnsCorrectTokens()
        {
            // Arrange - Create a small PNG image header (512x512)
            var pngBytes = CreatePngHeader(512, 512);
            var base64Data = Convert.ToBase64String(pngBytes);
            var imageUrl = new ImageUrl 
            { 
                Url = $"data:image/png;base64,{base64Data}",
                Detail = "high"
            };

            // Act
            var tokens = await _calculator.CalculateImageTokensAsync(imageUrl);

            // Assert
            // 512x512 = 1 tile, so 170 + (1 * 170) = 340 tokens
            Assert.Equal(340, tokens);
        }

        [Fact]
        public async Task CalculateImageTokensAsync_HighDetail_1024x1024_ReturnsCorrectTokens()
        {
            // Arrange - Create a PNG image header (1024x1024)
            var pngBytes = CreatePngHeader(1024, 1024);
            var base64Data = Convert.ToBase64String(pngBytes);
            var imageUrl = new ImageUrl 
            { 
                Url = $"data:image/png;base64,{base64Data}",
                Detail = "high"
            };

            // Act
            var tokens = await _calculator.CalculateImageTokensAsync(imageUrl);

            // Assert
            // 1024x1024 after scaling: 768x768 (shortest side limited to 768)
            // 768x768 = 2x2 tiles = 4 tiles
            // 170 + (4 * 170) = 850 tokens
            Assert.Equal(850, tokens);
        }

        [Fact]
        public async Task CalculateImageTokensAsync_HighDetail_2048x2048_ScalesCorrectly()
        {
            // Arrange - Create a PNG image header (2048x2048)
            var pngBytes = CreatePngHeader(2048, 2048);
            var base64Data = Convert.ToBase64String(pngBytes);
            var imageUrl = new ImageUrl 
            { 
                Url = $"data:image/png;base64,{base64Data}",
                Detail = "high"
            };

            // Act
            var tokens = await _calculator.CalculateImageTokensAsync(imageUrl);

            // Assert
            // 2048x2048 stays as is (within max dimension)
            // But shortest side (2048) > 768, so scale down
            // Scale factor: 768/2048 = 0.375
            // New dimensions: 768x768
            // 768x768 = 2x2 tiles = 4 tiles
            // 170 + (4 * 170) = 850 tokens
            Assert.Equal(850, tokens);
        }

        [Fact]
        public async Task CalculateImageTokensAsync_HighDetail_4096x2048_ScalesCorrectly()
        {
            // Arrange - Create a PNG image header (4096x2048)
            var pngBytes = CreatePngHeader(4096, 2048);
            var base64Data = Convert.ToBase64String(pngBytes);
            var imageUrl = new ImageUrl 
            { 
                Url = $"data:image/png;base64,{base64Data}",
                Detail = "high"
            };

            // Act
            var tokens = await _calculator.CalculateImageTokensAsync(imageUrl);

            // Assert
            // First scale to max dimension: 4096 > 2048, so scale down
            // Scale factor: 2048/4096 = 0.5
            // New dimensions: 2048x1024
            // Shortest side (1024) > 768, so scale down again
            // Scale factor: 768/1024 = 0.75
            // Final dimensions: 1536x768
            // Tiles: ceil(1536/512) x ceil(768/512) = 3x2 = 6 tiles
            // 170 + (6 * 170) = 1190 tokens
            Assert.Equal(1190, tokens);
        }

        [Fact]
        public async Task CalculateImageTokensAsync_DefaultDetail_UsesHighDetail()
        {
            // Arrange - Create a small PNG image header
            var pngBytes = CreatePngHeader(512, 512);
            var base64Data = Convert.ToBase64String(pngBytes);
            var imageUrl = new ImageUrl 
            { 
                Url = $"data:image/png;base64,{base64Data}",
                Detail = null // No detail specified
            };

            // Act
            var tokens = await _calculator.CalculateImageTokensAsync(imageUrl);

            // Assert
            // Should default to high detail: 170 + (1 * 170) = 340 tokens
            Assert.Equal(340, tokens);
        }

        [Fact]
        public async Task CalculateImageTokensAsync_InvalidBase64_ReturnsConservativeEstimate()
        {
            // Arrange
            var imageUrl = new ImageUrl 
            { 
                Url = "data:image/png;base64,invalid_base64_data",
                Detail = "high"
            };

            // Act
            var tokens = await _calculator.CalculateImageTokensAsync(imageUrl);

            // Assert - Should return conservative estimate (850 tokens for 1024x1024)
            Assert.Equal(850, tokens);
        }

        [Fact]
        public async Task CalculateImageTokensAsync_RemoteUrl_FetchesAndCalculates()
        {
            // Arrange
            var remoteUrl = "https://example.com/image.png";
            var pngBytes = CreatePngHeader(768, 768);
            
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK
                });

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new ByteArrayContent(pngBytes)
                });

            var imageUrl = new ImageUrl 
            { 
                Url = remoteUrl,
                Detail = "high"
            };

            // Act
            var tokens = await _calculator.CalculateImageTokensAsync(imageUrl);

            // Assert
            // 768x768 = 2x2 tiles = 4 tiles
            // 170 + (4 * 170) = 850 tokens
            Assert.Equal(850, tokens);
        }

        [Fact]
        public async Task CalculateImageTokensAsync_JpegImage_ParsesDimensionsCorrectly()
        {
            // Arrange - Create a JPEG header with dimensions
            var jpegBytes = CreateJpegHeader(1024, 768);
            var base64Data = Convert.ToBase64String(jpegBytes);
            var imageUrl = new ImageUrl 
            { 
                Url = $"data:image/jpeg;base64,{base64Data}",
                Detail = "high"
            };

            // Act
            var tokens = await _calculator.CalculateImageTokensAsync(imageUrl);

            // Assert
            // 1024x768: shortest side is already 768, no scaling needed
            // Tiles: ceil(1024/512) x ceil(768/512) = 2x2 = 4 tiles
            // 170 + (4 * 170) = 850 tokens
            Assert.Equal(850, tokens);
        }

        [Fact]
        public async Task CalculateImageTokensAsync_GifImage_ParsesDimensionsCorrectly()
        {
            // Arrange - Create a GIF header with dimensions
            var gifBytes = CreateGifHeader(256, 256);
            var base64Data = Convert.ToBase64String(gifBytes);
            var imageUrl = new ImageUrl 
            { 
                Url = $"data:image/gif;base64,{base64Data}",
                Detail = "high"
            };

            // Act
            var tokens = await _calculator.CalculateImageTokensAsync(imageUrl);

            // Assert
            // 256x256 = 1 tile (smaller than 512x512)
            // 170 + (1 * 170) = 340 tokens
            Assert.Equal(340, tokens);
        }

        [Fact]
        public async Task CalculateImageTokensAsync_NullImageUrl_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _calculator.CalculateImageTokensAsync(null!));
        }

        [Theory]
        [InlineData(100, 100, 340)]    // 1 tile
        [InlineData(512, 512, 340)]    // 1 tile
        [InlineData(513, 513, 850)]    // 2x2 tiles after scaling to 513x513
        [InlineData(1024, 512, 510)]   // 1024x512 -> no scaling needed -> 2x1 tiles = 170 + 2*170 = 510
        [InlineData(512, 1024, 510)]   // 512x1024 -> no scaling needed -> 1x2 tiles = 170 + 2*170 = 510
        public async Task CalculateImageTokensAsync_VariousDimensions_CalculatesCorrectly(
            int width, int height, int expectedTokens)
        {
            // Arrange
            var pngBytes = CreatePngHeader(width, height);
            var base64Data = Convert.ToBase64String(pngBytes);
            var imageUrl = new ImageUrl 
            { 
                Url = $"data:image/png;base64,{base64Data}",
                Detail = "high"
            };

            // Act
            var tokens = await _calculator.CalculateImageTokensAsync(imageUrl);

            // Assert
            Assert.Equal(expectedTokens, tokens);
        }

        [Fact]
        public void EstimateImageTokens_LowDetail_ReturnsFixedTokensWithoutConservativeFlag()
        {
            var imageUrl = new ImageUrl { Url = "https://example.com/image.jpg", Detail = "low" };

            var (tokens, isConservativeDefault) = ImageTokenCalculator.EstimateImageTokens(imageUrl);

            Assert.Equal(ImageTokenCalculator.LowDetailTokens, tokens);
            Assert.False(isConservativeDefault);
        }

        [Fact]
        public void EstimateImageTokens_Base64WithReadableDimensions_UsesHighDetailFormula()
        {
            var pngBytes = CreatePngHeader(512, 512);
            var imageUrl = new ImageUrl
            {
                Url = $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}",
                Detail = "high"
            };

            var (tokens, isConservativeDefault) = ImageTokenCalculator.EstimateImageTokens(imageUrl);

            // 512x512 = 1 tile: 170 + (1 * 170) = 340 tokens
            Assert.Equal(340, tokens);
            Assert.False(isConservativeDefault);
        }

        [Fact]
        public void EstimateImageTokens_RemoteUrl_ReturnsConservativeDefaultWithoutFetching()
        {
            // No HTTP handler is involved: the sync estimator must never touch the network.
            var imageUrl = new ImageUrl { Url = "https://example.com/image.png", Detail = "high" };

            var (tokens, isConservativeDefault) = ImageTokenCalculator.EstimateImageTokens(imageUrl);

            Assert.Equal(ImageTokenCalculator.ConservativeHighDetailTokens, tokens);
            Assert.True(isConservativeDefault);
        }

        [Fact]
        public void EstimateImageTokens_InvalidBase64_ReturnsConservativeDefault()
        {
            var imageUrl = new ImageUrl { Url = "data:image/png;base64,invalid_base64_data", Detail = "high" };

            var (tokens, isConservativeDefault) = ImageTokenCalculator.EstimateImageTokens(imageUrl);

            Assert.Equal(ImageTokenCalculator.ConservativeHighDetailTokens, tokens);
            Assert.True(isConservativeDefault);
        }

        [Fact]
        public void EstimateImageTokens_LargeBase64Payload_ReadsDimensionsFromHeaderOnly()
        {
            // A multi-megabyte payload whose header carries the dimensions: the bounded decode
            // must still find them rather than decoding (or choking on) the whole body.
            var headerBytes = CreatePngHeader(1024, 1024);
            var payload = new byte[2 * 1024 * 1024];
            headerBytes.CopyTo(payload, 0);
            var imageUrl = new ImageUrl
            {
                Url = $"data:image/png;base64,{Convert.ToBase64String(payload)}",
                Detail = "high"
            };

            var (tokens, isConservativeDefault) = ImageTokenCalculator.EstimateImageTokens(imageUrl);

            // 1024x1024 scales to 768x768 = 4 tiles: 170 + (4 * 170) = 850 tokens
            Assert.Equal(850, tokens);
            Assert.False(isConservativeDefault);
        }

        [Fact]
        public void EstimateImageTokens_NullImageUrl_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ImageTokenCalculator.EstimateImageTokens(null!));
        }

        // Helper methods to create image headers with specific dimensions

        private byte[] CreatePngHeader(int width, int height)
        {
            var bytes = new List<byte>();
            
            // PNG signature
            bytes.AddRange(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            
            // IHDR chunk
            bytes.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x0D }); // Chunk length
            bytes.AddRange(new byte[] { 0x49, 0x48, 0x44, 0x52 }); // "IHDR"
            
            // Width (4 bytes, big-endian)
            bytes.Add((byte)(width >> 24));
            bytes.Add((byte)(width >> 16));
            bytes.Add((byte)(width >> 8));
            bytes.Add((byte)width);
            
            // Height (4 bytes, big-endian)
            bytes.Add((byte)(height >> 24));
            bytes.Add((byte)(height >> 16));
            bytes.Add((byte)(height >> 8));
            bytes.Add((byte)height);
            
            // Bit depth, color type, etc.
            bytes.AddRange(new byte[] { 0x08, 0x02, 0x00, 0x00, 0x00 });
            
            // CRC (dummy)
            bytes.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            
            return bytes.ToArray();
        }

        private byte[] CreateJpegHeader(int width, int height)
        {
            var bytes = new List<byte>();
            
            // JPEG SOI marker
            bytes.AddRange(new byte[] { 0xFF, 0xD8 });
            
            // SOF0 marker (baseline DCT)
            bytes.AddRange(new byte[] { 0xFF, 0xC0 });
            
            // Length of segment
            bytes.AddRange(new byte[] { 0x00, 0x11 }); // 17 bytes
            
            // Data precision
            bytes.Add(0x08);
            
            // Height (2 bytes, big-endian)
            bytes.Add((byte)(height >> 8));
            bytes.Add((byte)height);
            
            // Width (2 bytes, big-endian)
            bytes.Add((byte)(width >> 8));
            bytes.Add((byte)width);
            
            // Number of components and component data (dummy)
            bytes.AddRange(new byte[] { 0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01 });
            
            return bytes.ToArray();
        }

        private byte[] CreateGifHeader(int width, int height)
        {
            var bytes = new List<byte>();
            
            // GIF signature
            bytes.AddRange(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }); // "GIF89a"
            
            // Width (2 bytes, little-endian)
            bytes.Add((byte)width);
            bytes.Add((byte)(width >> 8));
            
            // Height (2 bytes, little-endian)
            bytes.Add((byte)height);
            bytes.Add((byte)(height >> 8));
            
            // Packed fields and background color index
            bytes.AddRange(new byte[] { 0xF7, 0x00, 0x00 });
            
            return bytes.ToArray();
        }
    }
}