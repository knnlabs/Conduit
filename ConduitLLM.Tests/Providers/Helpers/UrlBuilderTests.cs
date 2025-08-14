using System;
using Xunit;
using ConduitLLM.Providers.Helpers;

namespace ConduitLLM.Tests.Providers.Helpers
{
    public class UrlBuilderTests
    {
        #region Combine Tests

        [Theory]
        [InlineData("https://api.example.com", "/v1/models", "https://api.example.com/v1/models")]
        [InlineData("https://api.example.com/", "/v1/models", "https://api.example.com/v1/models")]
        [InlineData("https://api.example.com", "v1/models", "https://api.example.com/v1/models")]
        [InlineData("https://api.example.com/", "v1/models", "https://api.example.com/v1/models")]
        [InlineData("https://api.example.com/v1", "/models", "https://api.example.com/v1/models")]
        [InlineData("https://api.example.com/v1/", "models", "https://api.example.com/v1/models")]
        [InlineData("https://api.example.com", "", "https://api.example.com")]
        [InlineData("https://api.example.com", null, "https://api.example.com")]
        public void Combine_TwoSegments_ReturnsProperlyFormattedUrl(string baseUrl, string path, string expected)
        {
            // Act
            var result = UrlBuilder.Combine(baseUrl, path);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("https://api.example.com", "v1", "models", "https://api.example.com/v1/models")]
        [InlineData("https://api.example.com/", "/v1/", "/models", "https://api.example.com/v1/models")]
        [InlineData("https://api.example.com", "v1", "deployments", "model123", "chat/completions", "https://api.example.com/v1/deployments/model123/chat/completions")]
        public void Combine_MultipleSegments_ReturnsProperlyFormattedUrl(params string[] segments)
        {
            // Arrange
            var expectedSegments = segments[segments.Length - 1];
            var urlSegments = new string[segments.Length - 1];
            Array.Copy(segments, 0, urlSegments, 0, segments.Length - 1);

            // Act
            var result = UrlBuilder.Combine(urlSegments);

            // Assert
            Assert.Equal(expectedSegments, result);
        }

        [Fact]
        public void Combine_NullBaseUrl_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => UrlBuilder.Combine(null, "path"));
        }

        [Fact]
        public void Combine_EmptyBaseUrl_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => UrlBuilder.Combine("", "path"));
        }

        [Fact]
        public void Combine_WhitespaceBaseUrl_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => UrlBuilder.Combine("   ", "path"));
        }

        #endregion

        #region AppendQueryString Tests

        [Theory]
        [InlineData("https://api.example.com/models", new string[] { "key", "abc123" }, "https://api.example.com/models?key=abc123")]
        [InlineData("https://api.example.com/models?existing=value", new string[] { "key", "abc123" }, "https://api.example.com/models?existing=value&key=abc123")]
        [InlineData("https://api.example.com/models", new string[] { "key", "abc123", "version", "2024-01-01" }, "https://api.example.com/models?key=abc123&version=2024-01-01")]
        [InlineData("https://api.example.com/models", new string[] { "special", "hello world" }, "https://api.example.com/models?special=hello%20world")]
        [InlineData("https://api.example.com/models", new string[] { }, "https://api.example.com/models")]
        public void AppendQueryString_ValidParameters_ReturnsUrlWithQuery(string url, string[] paramArray, string expected)
        {
            // Arrange
            var parameters = new (string, string)[paramArray.Length / 2];
            for (int i = 0; i < paramArray.Length; i += 2)
            {
                parameters[i / 2] = (paramArray[i], paramArray[i + 1]);
            }

            // Act
            var result = UrlBuilder.AppendQueryString(url, parameters);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void AppendQueryString_NullUrl_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => UrlBuilder.AppendQueryString(null, ("key", "value")));
        }

        #endregion

        #region EnsureSegment Tests

        [Theory]
        [InlineData("https://api.example.com", "/v1", "https://api.example.com/v1")]
        [InlineData("https://api.example.com/v1", "/v1", "https://api.example.com/v1")]
        [InlineData("https://api.example.com/v1/models", "/v1", "https://api.example.com/v1/models")]
        [InlineData("https://api.example.com", "v1beta", "https://api.example.com/v1beta")]
        [InlineData("https://api.example.com/other", "/v1", "https://api.example.com/other/v1")]
        public void EnsureSegment_ValidInputs_ReturnsUrlWithSegment(string url, string segment, string expected)
        {
            // Act
            var result = UrlBuilder.EnsureSegment(url, segment);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EnsureSegment_NullUrl_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => UrlBuilder.EnsureSegment(null, "/v1"));
        }

        #endregion

        #region IsValidUrl Tests

        [Theory]
        [InlineData("https://api.example.com", true)]
        [InlineData("http://localhost:8080", true)]
        [InlineData("https://api.example.com/v1/models", true)]
        [InlineData("ftp://example.com", false)]
        [InlineData("not-a-url", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("   ", false)]
        public void IsValidUrl_ValidatesCorrectly(string url, bool expected)
        {
            // Act
            var result = UrlBuilder.IsValidUrl(url);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region ToWebSocketUrl Tests

        [Theory]
        [InlineData("https://api.example.com", "wss://api.example.com")]
        [InlineData("http://localhost:8080", "ws://localhost:8080")]
        [InlineData("HTTPS://API.EXAMPLE.COM", "wss://API.EXAMPLE.COM")]
        [InlineData("wss://api.example.com", "wss://api.example.com")]
        [InlineData("ws://localhost", "ws://localhost")]
        public void ToWebSocketUrl_ValidUrls_ReturnsWebSocketUrl(string input, string expected)
        {
            // Act
            var result = UrlBuilder.ToWebSocketUrl(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("ftp://example.com")]
        [InlineData("not-a-url")]
        [InlineData("example.com")]
        public void ToWebSocketUrl_InvalidUrls_ThrowsArgumentException(string input)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => UrlBuilder.ToWebSocketUrl(input));
        }

        [Fact]
        public void ToWebSocketUrl_NullUrl_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => UrlBuilder.ToWebSocketUrl(null));
        }

        #endregion
    }
}