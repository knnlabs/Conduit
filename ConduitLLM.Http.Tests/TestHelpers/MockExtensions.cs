using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace ConduitLLM.Http.Tests.TestHelpers
{
    /// <summary>
    /// Mock extension helpers for common dependencies in HTTP tests.
    /// </summary>
    public static class MockExtensions
    {
        /// <summary>
        /// Sets up IMemoryCache mock with full get/set behavior using a backing dictionary.
        /// </summary>
        public static Mock<IMemoryCache> SetupWorkingCache(this Mock<IMemoryCache> mock)
        {
            var cache = new Dictionary<object, object>();
            
            mock.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
                .Returns((object key, out object value) =>
                {
                    return cache.TryGetValue(key, out value);
                });
                
            mock.Setup(x => x.CreateEntry(It.IsAny<object>()))
                .Returns((object key) =>
                {
                    var entry = new Mock<ICacheEntry>();
                    entry.SetupAllProperties();
                    entry.Setup(e => e.Key).Returns(key);
                    entry.Setup(e => e.Value).Returns(() => cache.ContainsKey(key) ? cache[key] : null);
                    entry.SetupSet(e => e.Value = It.IsAny<object>())
                        .Callback<object>(value => cache[key] = value);
                    return entry.Object;
                });
                
            mock.Setup(x => x.Remove(It.IsAny<object>()))
                .Callback((object key) => cache.Remove(key));
                
            return mock;
        }
    }
}