using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Policies;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests
{
    public class CachePolicyTests
    {
        #region TTL Policy Tests

        [Fact]
        public void FixedTtlPolicy_CalculatesCorrectExpiration()
        {
            // Arrange
            var policy = new FixedTtlPolicy("test", TimeSpan.FromMinutes(30));
            var entry = new MockCacheEntry
            {
                CreatedAt = DateTime.UtcNow
            };
            var context = new CachePolicyContext();

            // Act
            var expiration = policy.CalculateExpiration(entry, context);

            // Assert
            Assert.NotNull(expiration);
            Assert.Equal(entry.CreatedAt.AddMinutes(30), expiration.Value);
        }

        [Fact]
        public void SlidingTtlPolicy_CalculatesBasedOnLastAccess()
        {
            // Arrange
            var policy = new SlidingTtlPolicy("test", TimeSpan.FromMinutes(15));
            var now = DateTime.UtcNow;
            var entry = new MockCacheEntry
            {
                CreatedAt = now.AddMinutes(-20),
                LastAccessedAt = now.AddMinutes(-5)
            };
            var context = new CachePolicyContext();

            // Act
            var expiration = policy.CalculateExpiration(entry, context);

            // Assert
            Assert.NotNull(expiration);
            Assert.Equal(entry.LastAccessedAt.AddMinutes(15), expiration.Value);
        }

        [Fact]
        public void SlidingTtlPolicy_RespectsMaxLifetime()
        {
            // Arrange
            var policy = new SlidingTtlPolicy("test", 
                TimeSpan.FromMinutes(15), 
                TimeSpan.FromHours(1));
            var now = DateTime.UtcNow;
            var entry = new MockCacheEntry
            {
                CreatedAt = now.AddMinutes(-50),
                LastAccessedAt = now.AddMinutes(-5)
            };
            var context = new CachePolicyContext();

            // Act
            var expiration = policy.CalculateExpiration(entry, context);

            // Assert
            Assert.NotNull(expiration);
            Assert.Equal(entry.CreatedAt.AddHours(1), expiration.Value);
        }

        [Fact]
        public void AdaptiveTtlPolicy_ExtendsBasedOnAccessCount()
        {
            // Arrange
            var policy = new AdaptiveTtlPolicy("test", 
                TimeSpan.FromMinutes(5), 
                TimeSpan.FromHours(1),
                accessThreshold: 10,
                extensionFactor: 2.0);
            var entry = new MockCacheEntry
            {
                CreatedAt = DateTime.UtcNow,
                AccessCount = 25
            };
            var context = new CachePolicyContext();

            // Act
            var expiration = policy.CalculateExpiration(entry, context);

            // Assert
            Assert.NotNull(expiration);
            // With 25 accesses and threshold of 10, multiplier = 2
            // Extended TTL = 5 min * 2^2 = 20 min
            var expectedTtl = TimeSpan.FromMinutes(20);
            Assert.True(Math.Abs((expiration.Value - DateTime.UtcNow - expectedTtl).TotalSeconds) < 1);
        }

        #endregion

        #region Size Policy Tests

        [Fact]
        public void ItemCountPolicy_CountsAsOne()
        {
            // Arrange
            var policy = new ItemCountSizePolicy("test", 1000);
            var entry = new MockCacheEntry();

            // Act
            var size = policy.CalculateSize(entry);

            // Assert
            Assert.Equal(1, size);
        }

        [Fact]
        public void MemorySizePolicy_UsesProvidedSize()
        {
            // Arrange
            var policy = new MemorySizePolicy("test", 1024 * 1024); // 1MB
            var entry = new MockCacheEntry { SizeInBytes = 2048 };

            // Act
            var size = policy.CalculateSize(entry);

            // Assert
            Assert.Equal(2048, size);
        }

        [Fact]
        public void MemorySizePolicy_EstimatesWhenMissing()
        {
            // Arrange
            var policy = new MemorySizePolicy("test", 1024 * 1024)
            {
                EstimateSizeIfMissing = true,
                DefaultSizeEstimate = 512
            };
            var entry = new MockCacheEntry 
            { 
                SizeInBytes = null,
                Value = "test string"
            };

            // Act
            var size = policy.CalculateSize(entry);

            // Assert
            Assert.True(size > 0);
        }

        [Fact]
        public void DynamicSizePolicy_AdjustsBasedOnMemory()
        {
            // Arrange
            var policy = new DynamicSizePolicy("test", 50.0, 1024, 1024 * 1024);
            var entry = new MockCacheEntry();

            // Act
            var size = policy.CalculateSize(entry);
            var isValid = policy.Validate();

            // Assert
            Assert.True(size > 0);
            Assert.True(isValid);
            Assert.NotNull(policy.MaxSize);
            Assert.True(policy.MaxSize >= policy.MinSize);
        }

        #endregion

        #region Eviction Policy Tests

        [Fact]
        public async Task LruEvictionPolicy_SelectsOldestAccessed()
        {
            // Arrange
            var policy = new LruEvictionPolicy("test");
            var now = DateTime.UtcNow;
            var entries = new List<ICacheEntry>
            {
                new MockCacheEntry { Key = "1", LastAccessedAt = now.AddMinutes(-30), SizeInBytes = 100 },
                new MockCacheEntry { Key = "2", LastAccessedAt = now.AddMinutes(-10), SizeInBytes = 100 },
                new MockCacheEntry { Key = "3", LastAccessedAt = now.AddMinutes(-20), SizeInBytes = 100 }
            };
            var context = new CachePolicyContext();

            // Act
            var toEvict = await policy.SelectForEvictionAsync(entries, 150, context);

            // Assert
            var evictedKeys = toEvict.Select(e => e.Key).ToList();
            Assert.Equal(2, evictedKeys.Count);
            Assert.Contains("1", evictedKeys); // Oldest
            Assert.Contains("3", evictedKeys); // Second oldest
        }

        [Fact]
        public async Task PriorityEvictionPolicy_SelectsLowestPriority()
        {
            // Arrange
            var policy = new PriorityEvictionPolicy("test") { ConsiderAge = false };
            var entries = new List<ICacheEntry>
            {
                new MockCacheEntry { Key = "1", Priority = 50, SizeInBytes = 100 },
                new MockCacheEntry { Key = "2", Priority = 90, SizeInBytes = 100 },
                new MockCacheEntry { Key = "3", Priority = 30, SizeInBytes = 100 }
            };
            var context = new CachePolicyContext();

            // Act
            var toEvict = await policy.SelectForEvictionAsync(entries, 150, context);

            // Assert
            var evictedKeys = toEvict.Select(e => e.Key).ToList();
            Assert.Equal(2, evictedKeys.Count);
            Assert.Contains("3", evictedKeys); // Lowest priority
            Assert.Contains("1", evictedKeys); // Second lowest
        }

        #endregion

        #region Policy Engine Tests

        [Fact]
        public void PolicyEngine_RegistersAndRetrievesPolicies()
        {
            // Arrange
            var logger = new Mock<ILogger<CachePolicyEngine>>().Object;
            var engine = new CachePolicyEngine(logger);
            var policy1 = new FixedTtlPolicy("policy1", TimeSpan.FromMinutes(10));
            var policy2 = new LruEvictionPolicy("policy2");

            // Act
            engine.RegisterPolicy(policy1, new[] { CacheRegion.VirtualKeys });
            engine.RegisterPolicy(policy2);

            var allPolicies = engine.GetPolicies().ToList();
            var vkPolicies = engine.GetPoliciesForRegion(CacheRegion.VirtualKeys).ToList();

            // Assert
            Assert.Equal(2, allPolicies.Count);
            Assert.Equal(2, vkPolicies.Count); // policy2 applies to all regions
        }

        [Fact]
        public void PolicyEngine_AppliesTtlPolicies()
        {
            // Arrange
            var logger = new Mock<ILogger<CachePolicyEngine>>().Object;
            var engine = new CachePolicyEngine(logger);
            var policy1 = new FixedTtlPolicy("policy1", TimeSpan.FromMinutes(30));
            var policy2 = new FixedTtlPolicy("policy2", TimeSpan.FromMinutes(15)) { Priority = 100 };
            
            engine.RegisterPolicy(policy1);
            engine.RegisterPolicy(policy2);

            var entry = new MockCacheEntry { CreatedAt = DateTime.UtcNow };
            var context = new CachePolicyContext { Region = CacheRegion.Default };

            // Act
            var expiration = engine.ApplyTtlPolicies(entry, context);

            // Assert
            Assert.NotNull(expiration);
            // Should use the shorter TTL (most restrictive)
            Assert.Equal(entry.CreatedAt.AddMinutes(15), expiration.Value);
        }

        [Fact]
        public void PolicyEngine_ValidatesAllPolicies()
        {
            // Arrange
            var logger = new Mock<ILogger<CachePolicyEngine>>().Object;
            var engine = new CachePolicyEngine(logger);
            var validPolicy = new FixedTtlPolicy("valid", TimeSpan.FromMinutes(10));
            var invalidPolicy = new Mock<ICachePolicy>();
            invalidPolicy.Setup(p => p.Name).Returns("invalid");
            invalidPolicy.Setup(p => p.Validate()).Returns(false);

            engine.RegisterPolicy(validPolicy);
            
            // Act
            var results = engine.ValidatePolicies();

            // Assert
            Assert.Single(results);
            Assert.True(results["valid"]);
        }

        #endregion

        private class MockCacheEntry : ICacheEntry
        {
            public string Key { get; set; } = Guid.NewGuid().ToString();
            public object? Value { get; set; }
            public CacheRegion Region { get; set; } = CacheRegion.Default;
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
            public long AccessCount { get; set; }
            public long? SizeInBytes { get; set; }
            public int Priority { get; set; } = 50;
            public Dictionary<string, object>? Metadata { get; set; }
            public DateTime? ExpiresAt { get; set; }

            public void RecordAccess()
            {
                LastAccessedAt = DateTime.UtcNow;
                AccessCount++;
            }
        }
    }
}