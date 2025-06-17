using System;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ConduitLLM.Tests.Configuration
{
    /// <summary>
    /// Integration tests for environment variable configuration
    /// </summary>
    public class EnvironmentVariableIntegrationTests : IDisposable
    {
        private readonly Dictionary<string, string?> _originalEnvVars = new();
        private readonly List<string> _envVarsToClean = new()
        {
            "REDIS_URL",
            "CONDUIT_REDIS_CONNECTION_STRING",
            "CONDUIT_CACHE_ENABLED",
            "CONDUIT_CACHE_TYPE",
            "CONDUIT_REDIS_INSTANCE_NAME",
            "CONDUIT_MASTER_KEY",
            "AdminApi__MasterKey"
        };

        public EnvironmentVariableIntegrationTests()
        {
            // Save original environment variables
            foreach (var key in _envVarsToClean)
            {
                _originalEnvVars[key] = Environment.GetEnvironmentVariable(key);
            }
        }

        public void Dispose()
        {
            // Restore original environment variables
            foreach (var kvp in _originalEnvVars)
            {
                if (kvp.Value == null)
                {
                    Environment.SetEnvironmentVariable(kvp.Key, null);
                }
                else
                {
                    Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
                }
            }
        }

        [Fact]
        public void RedisUrl_TakesPrecedenceOver_LegacyConnectionString()
        {
            // Arrange
            Environment.SetEnvironmentVariable("REDIS_URL", "redis://newhost:6380");
            Environment.SetEnvironmentVariable("CONDUIT_REDIS_CONNECTION_STRING", "oldhost:6379");

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            
            // Act
            services.AddCacheService(configuration);
            var provider = services.BuildServiceProvider();
            var cacheOptions = provider.GetRequiredService<IOptions<CacheOptions>>().Value;

            // Assert
            Assert.Contains("newhost:6380", cacheOptions.RedisConnectionString);
        }

        [Fact]
        public void LegacyConnectionString_UsedWhenRedisUrlNotProvided()
        {
            // Arrange
            Environment.SetEnvironmentVariable("REDIS_URL", null);
            Environment.SetEnvironmentVariable("CONDUIT_REDIS_CONNECTION_STRING", "legacyhost:6379");

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            
            // Act
            services.AddCacheService(configuration);
            var provider = services.BuildServiceProvider();
            var cacheOptions = provider.GetRequiredService<IOptions<CacheOptions>>().Value;

            // Assert
            Assert.Equal("legacyhost:6379", cacheOptions.RedisConnectionString);
        }

        [Fact]
        public void CacheAutoEnables_WhenRedisUrlProvided()
        {
            // Arrange
            Environment.SetEnvironmentVariable("REDIS_URL", "redis://autohost:6379");
            Environment.SetEnvironmentVariable("CONDUIT_CACHE_ENABLED", null); // Not set

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            
            // Act
            services.AddCacheService(configuration);
            var provider = services.BuildServiceProvider();
            var cacheOptions = provider.GetRequiredService<IOptions<CacheOptions>>().Value;

            // Assert
            Assert.True(cacheOptions.IsEnabled);
            Assert.Equal("Redis", cacheOptions.CacheType);
        }

        [Fact]
        public void ExplicitCacheDisable_OverridesAutoEnable()
        {
            // Arrange
            Environment.SetEnvironmentVariable("REDIS_URL", "redis://autohost:6379");
            Environment.SetEnvironmentVariable("CONDUIT_CACHE_ENABLED", "false");

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            
            // Act
            services.AddCacheService(configuration);
            var provider = services.BuildServiceProvider();
            var cacheOptions = provider.GetRequiredService<IOptions<CacheOptions>>().Value;

            // Assert
            Assert.False(cacheOptions.IsEnabled);
            // CacheType is still Redis because Redis is configured
            Assert.Equal("Redis", cacheOptions.CacheType);
        }

        [Fact]
        public void RedisUrlWithAuth_ParsesCorrectly()
        {
            // Arrange
            Environment.SetEnvironmentVariable("REDIS_URL", "redis://user:pass@authhost:6379");

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            
            // Act
            services.AddCacheService(configuration);
            var provider = services.BuildServiceProvider();
            var cacheOptions = provider.GetRequiredService<IOptions<CacheOptions>>().Value;

            // Assert
            Assert.Contains("authhost:6379", cacheOptions.RedisConnectionString);
            Assert.Contains("user=user", cacheOptions.RedisConnectionString);
            Assert.Contains("password=pass", cacheOptions.RedisConnectionString);
            Assert.True(cacheOptions.IsEnabled);
        }

        [Fact]
        public void InvalidRedisUrl_FallsBackToLegacy()
        {
            // Arrange
            Environment.SetEnvironmentVariable("REDIS_URL", "not-a-valid-url");
            Environment.SetEnvironmentVariable("CONDUIT_REDIS_CONNECTION_STRING", "fallbackhost:6379");

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            
            // Act
            services.AddCacheService(configuration);
            var provider = services.BuildServiceProvider();
            var cacheOptions = provider.GetRequiredService<IOptions<CacheOptions>>().Value;

            // Assert
            Assert.Equal("fallbackhost:6379", cacheOptions.RedisConnectionString);
        }

        [Fact]
        public void RedisInstanceName_CanBeOverridden()
        {
            // Arrange
            Environment.SetEnvironmentVariable("REDIS_URL", "redis://localhost:6379");
            Environment.SetEnvironmentVariable("CONDUIT_REDIS_INSTANCE_NAME", "custom-instance");

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cache:RedisInstanceName"] = "from-config"
                })
                .Build();
            
            // Act
            services.AddCacheService(configuration);
            var provider = services.BuildServiceProvider();
            var cacheOptions = provider.GetRequiredService<IOptions<CacheOptions>>().Value;

            // Assert
            Assert.Equal("from-config", cacheOptions.RedisInstanceName); // Config takes precedence
        }

        [Fact]
        public void CacheService_WorksWithNewRedisUrl()
        {
            // Arrange
            Environment.SetEnvironmentVariable("REDIS_URL", "redis://localhost:6379");

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            services.AddLogging();
            services.AddCacheService(configuration);
            
            // Act
            var provider = services.BuildServiceProvider();
            var cacheService = provider.GetRequiredService<ICacheService>();
            var cacheOptions = provider.GetRequiredService<IOptions<CacheOptions>>().Value;

            // Assert
            Assert.NotNull(cacheService);
            Assert.True(cacheOptions.IsEnabled);
            Assert.Equal("Redis", cacheOptions.CacheType);
            
            // Test basic cache operations (will use memory cache if Redis not actually running)
            cacheService.Set("test-key", "test-value", TimeSpan.FromMinutes(1));
            var result = cacheService.Get<string>("test-key");
            Assert.Equal("test-value", result);
        }

        [Fact]
        public void MultipleRedisUrlFormats_ParseCorrectly()
        {
            var testCases = new (string, object)[]
            {
                ("redis://localhost:6379", "localhost:6379"),
                ("redis://:password@localhost:6379", "password=password"),
                ("redis://user:pass@host:1234", new[] { "host:1234", "user=user", "password=pass" }),
                ("rediss://secure:6380", new[] { "secure:6380", "ssl=true" })
            };

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();

            foreach (var testCase in testCases)
            {
                Environment.SetEnvironmentVariable("REDIS_URL", testCase.Item1);
                
                services.Clear();
                services.AddCacheService(configuration);
                var provider = services.BuildServiceProvider();
                var cacheOptions = provider.GetRequiredService<IOptions<CacheOptions>>().Value;

                if (testCase.Item2 is string expectedString)
                {
                    Assert.Contains(expectedString, cacheOptions.RedisConnectionString);
                }
                else if (testCase.Item2 is string[] expectedParts)
                {
                    foreach (var part in expectedParts)
                    {
                        Assert.Contains(part, cacheOptions.RedisConnectionString);
                    }
                }
            }
        }
    }
}