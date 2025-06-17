using System;
using ConduitLLM.Configuration.Utilities;
using Xunit;

namespace ConduitLLM.Tests.Configuration
{
    public class RedisUrlParserTests
    {
        [Fact]
        public void ParseRedisUrl_SimpleHostAndPort_ReturnsCorrectConnectionString()
        {
            // Arrange
            var redisUrl = "redis://localhost:6379";

            // Act
            var result = RedisUrlParser.ParseRedisUrl(redisUrl);

            // Assert
            Assert.Contains("localhost:6379", result);
            Assert.Contains("abortConnect=false", result);
            Assert.Contains("connectTimeout=10000", result);
        }

        [Fact]
        public void ParseRedisUrl_HostOnly_DefaultsToPort6379()
        {
            // Arrange
            var redisUrl = "redis://localhost";

            // Act
            var result = RedisUrlParser.ParseRedisUrl(redisUrl);

            // Assert
            Assert.Contains("localhost:6379", result);
        }

        [Fact]
        public void ParseRedisUrl_WithPassword_IncludesPassword()
        {
            // Arrange
            var redisUrl = "redis://:mypassword@localhost:6379";

            // Act
            var result = RedisUrlParser.ParseRedisUrl(redisUrl);

            // Assert
            Assert.Contains("localhost:6379", result);
            Assert.Contains("password=mypassword", result);
        }

        [Fact]
        public void ParseRedisUrl_WithUsernameAndPassword_IncludesBoth()
        {
            // Arrange
            var redisUrl = "redis://myuser:mypassword@localhost:6379";

            // Act
            var result = RedisUrlParser.ParseRedisUrl(redisUrl);

            // Assert
            Assert.Contains("localhost:6379", result);
            Assert.Contains("password=mypassword", result);
            Assert.Contains("user=myuser", result);
        }

        [Fact]
        public void ParseRedisUrl_WithoutProtocol_StillWorks()
        {
            // Arrange
            var redisUrl = "localhost:6379";

            // Act
            var result = RedisUrlParser.ParseRedisUrl(redisUrl);

            // Assert
            Assert.Contains("localhost:6379", result);
        }

        [Fact]
        public void ParseRedisUrl_WithSsl_AddsSslFlag()
        {
            // Arrange
            var redisUrl = "rediss://localhost:6380";

            // Act
            var result = RedisUrlParser.ParseRedisUrl(redisUrl);

            // Assert
            Assert.Contains("localhost:6380", result);
            Assert.Contains("ssl=true", result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseRedisUrl_EmptyOrNull_ThrowsArgumentException(string redisUrl)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => RedisUrlParser.ParseRedisUrl(redisUrl));
        }

        [Fact]
        public void ParseRedisUrl_ComplexFormat_ParsesSuccessfully()
        {
            // Arrange
            var redisUrl = "redis://invalid::format::here";

            // Act
            var result = RedisUrlParser.ParseRedisUrl(redisUrl);

            // Assert - our parser is lenient and treats this as a hostname
            Assert.Contains("invalid::format::here:6379", result);
        }

        [Fact]
        public void ParseRedisUrl_RealWorldAzureCache_ParsesCorrectly()
        {
            // Arrange
            var redisUrl = "redis://:abc123xyz456@myredis.redis.cache.windows.net:6380";

            // Act
            var result = RedisUrlParser.ParseRedisUrl(redisUrl);

            // Assert
            Assert.Contains("myredis.redis.cache.windows.net:6380", result);
            Assert.Contains("password=abc123xyz456", result);
        }

        [Fact]
        public void ParseRedisUrl_DockerComposeFormat_ParsesCorrectly()
        {
            // Arrange
            var redisUrl = "redis://redis:6379";

            // Act
            var result = RedisUrlParser.ParseRedisUrl(redisUrl);

            // Assert
            Assert.Contains("redis:6379", result);
            Assert.DoesNotContain("password=", result);
        }
    }
}