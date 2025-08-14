using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.TestHelpers
{
    /// <summary>
    /// Fluent builders for creating complex mock setups.
    /// </summary>
    public static class MockBuilders
    {
        /// <summary>
        /// Creates a fluent builder for IDistributedCache mock.
        /// </summary>
        public static DistributedCacheBuilder BuildDistributedCache()
        {
            return new DistributedCacheBuilder();
        }

        /// <summary>
        /// Creates a fluent builder for ICacheService mock.
        /// </summary>
        public static CacheServiceBuilder BuildCacheService()
        {
            return new CacheServiceBuilder();
        }

        /// <summary>
        /// Creates a fluent builder for complex memory cache scenarios.
        /// </summary>
        public static MemoryCacheBuilder BuildMemoryCache()
        {
            return new MemoryCacheBuilder();
        }
    }

    /// <summary>
    /// Fluent builder for IDistributedCache mocks.
    /// </summary>
    public class DistributedCacheBuilder
    {
        private readonly Mock<IDistributedCache> _mock = new();
        private readonly Dictionary<string, byte[]> _cache = new();

        public DistributedCacheBuilder WithValue(string key, string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            _cache[key] = bytes;
            return this;
        }

        public DistributedCacheBuilder WithValue(string key, byte[] value)
        {
            _cache[key] = value;
            return this;
        }

        public DistributedCacheBuilder WithGetBehavior()
        {
            _mock.Setup(x => x.Get(It.IsAny<string>()))
                .Returns((string key) => _cache.TryGetValue(key, out var value) ? value : null);

            _mock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, CancellationToken ct) => 
                    _cache.TryGetValue(key, out var value) ? value : null);

            return this;
        }

        public DistributedCacheBuilder WithSetBehavior()
        {
            _mock.Setup(x => x.Set(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>()))
                .Callback((string key, byte[] value, DistributedCacheEntryOptions options) => _cache[key] = value);

            _mock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .Returns((string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken ct) =>
                {
                    _cache[key] = value;
                    return Task.CompletedTask;
                });

            return this;
        }

        public DistributedCacheBuilder WithRemoveBehavior()
        {
            _mock.Setup(x => x.Remove(It.IsAny<string>()))
                .Callback((string key) => _cache.Remove(key));

            _mock.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string key, CancellationToken ct) =>
                {
                    _cache.Remove(key);
                    return Task.CompletedTask;
                });

            return this;
        }

        public DistributedCacheBuilder WithFullBehavior()
        {
            return WithGetBehavior().WithSetBehavior().WithRemoveBehavior();
        }

        public Mock<IDistributedCache> Build()
        {
            return _mock;
        }
    }

    /// <summary>
    /// Fluent builder for ICacheService mocks (ConduitLLM.Configuration.Services).
    /// </summary>
    public class CacheServiceBuilder
    {
        private readonly Mock<ConduitLLM.Configuration.Interfaces.ICacheService> _mock = new();
        private readonly Dictionary<string, object> _cache = new();
        private TimeSpan? _defaultExpiration;

        public CacheServiceBuilder WithDefaultExpiration(TimeSpan expiration)
        {
            _defaultExpiration = expiration;
            return this;
        }

        public CacheServiceBuilder WithCachedValue<T>(string key, T value)
        {
            _cache[key] = value;
            return this;
        }

        public CacheServiceBuilder WithGetBehavior()
        {
            _mock.Setup(x => x.Get<It.IsAnyType>(It.IsAny<string>()))
                .Returns((string key) =>
                {
                    if (_cache.TryGetValue(key, out var value))
                    {
                        return value;
                    }
                    return null;
                });

            return this;
        }

        public CacheServiceBuilder WithSetBehavior()
        {
            _mock.Setup(x => x.Set(It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>()))
                .Callback((string key, object value, TimeSpan? absoluteExpiration, TimeSpan? slidingExpiration) =>
                {
                    _cache[key] = value;
                });

            return this;
        }

        public CacheServiceBuilder WithGetOrCreateBehavior<T>(Func<string, Task<T>> factory = null)
        {
            _mock.Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<T>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>()))
                .Returns(async (string key, Func<Task<T>> valueFactory, TimeSpan? absoluteExpiration, TimeSpan? slidingExpiration) =>
                {
                    if (_cache.TryGetValue(key, out var cached) && cached is T typedValue)
                    {
                        return typedValue;
                    }

                    var value = factory != null ? await factory(key) : await valueFactory();
                    _cache[key] = value;
                    return value;
                });

            return this;
        }

        public Mock<ConduitLLM.Configuration.Interfaces.ICacheService> Build()
        {
            return _mock;
        }
    }

    /// <summary>
    /// Fluent builder for complex IMemoryCache scenarios.
    /// </summary>
    public class MemoryCacheBuilder
    {
        private readonly Mock<IMemoryCache> _mock = new();
        private readonly Dictionary<object, CacheItem> _cache = new();
        private readonly List<Microsoft.Extensions.Caching.Memory.ICacheEntry> _entries = new();

        private class CacheItem
        {
            public object Value { get; set; }
            public DateTimeOffset? AbsoluteExpiration { get; set; }
            public TimeSpan? SlidingExpiration { get; set; }
        }

        public MemoryCacheBuilder WithEntry(object key, object value, Action<Microsoft.Extensions.Caching.Memory.ICacheEntry> configure = null)
        {
            var item = new CacheItem { Value = value };
            _cache[key] = item;

            var entry = new Mock<Microsoft.Extensions.Caching.Memory.ICacheEntry>();
            entry.SetupAllProperties();
            entry.Setup(x => x.Key).Returns(key);
            entry.Setup(x => x.Value).Returns(() => item.Value);
            entry.SetupSet(x => x.Value = It.IsAny<object>()).Callback<object>(v => item.Value = v);
            entry.SetupSet(x => x.AbsoluteExpiration = It.IsAny<DateTimeOffset?>())
                .Callback<DateTimeOffset?>(exp => item.AbsoluteExpiration = exp);
            entry.SetupSet(x => x.SlidingExpiration = It.IsAny<TimeSpan?>())
                .Callback<TimeSpan?>(exp => item.SlidingExpiration = exp);

            configure?.Invoke(entry.Object);
            _entries.Add(entry.Object);

            return this;
        }

        public MemoryCacheBuilder WithEvictionCallback(Action<object, object, EvictionReason, object> callback)
        {
            foreach (var entry in _entries)
            {
                var mockEntry = Mock.Get(entry);
                mockEntry.Setup(x => x.PostEvictionCallbacks).Returns(new List<PostEvictionCallbackRegistration>
                {
                    new PostEvictionCallbackRegistration
                    {
                        EvictionCallback = (key, value, reason, state) => callback(key, value, reason, state),
                        State = null
                    }
                });
            }
            return this;
        }

        public MemoryCacheBuilder WithSizeLimit(long sizeLimit)
        {
            // This would require a more complex implementation with size tracking
            return this;
        }

        public Mock<IMemoryCache> Build()
        {
            _mock.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
                .Returns((object key, out object value) =>
                {
                    if (_cache.TryGetValue(key, out var item))
                    {
                        // Check expiration
                        if (item.AbsoluteExpiration.HasValue && item.AbsoluteExpiration.Value <= DateTimeOffset.UtcNow)
                        {
                            _cache.Remove(key);
                            value = null;
                            return false;
                        }
                        value = item.Value;
                        return true;
                    }
                    value = null;
                    return false;
                });

            _mock.Setup(x => x.CreateEntry(It.IsAny<object>()))
                .Returns((object key) =>
                {
                    var entry = _entries.FirstOrDefault(e => e.Key.Equals(key));
                    if (entry != null) return entry;

                    var newEntry = new Mock<Microsoft.Extensions.Caching.Memory.ICacheEntry>();
                    newEntry.SetupAllProperties();
                    newEntry.Setup(e => e.Key).Returns(key);
                    
                    var item = _cache.ContainsKey(key) ? _cache[key] : new CacheItem();
                    _cache[key] = item;
                    
                    newEntry.Setup(e => e.Value).Returns(() => item.Value);
                    newEntry.SetupSet(e => e.Value = It.IsAny<object>())
                        .Callback<object>(v => item.Value = v);
                    
                    return newEntry.Object;
                });

            _mock.Setup(x => x.Remove(It.IsAny<object>()))
                .Callback((object key) => _cache.Remove(key));

            return _mock;
        }
    }
}