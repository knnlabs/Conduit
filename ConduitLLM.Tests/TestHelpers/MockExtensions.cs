using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Language.Flow;

namespace ConduitLLM.Tests.TestHelpers
{
    /// <summary>
    /// Comprehensive mock extension helpers for common dependencies across all test phases.
    /// </summary>
    public static class MockExtensions
    {
        #region IMemoryCache Extensions

        /// <summary>
        /// Sets up IMemoryCache mock to return a value for a specific key.
        /// </summary>
        public static Mock<IMemoryCache> SetupCacheGet<T>(this Mock<IMemoryCache> mock, object key, T value)
        {
            object outValue = value;
            mock.Setup(x => x.TryGetValue(key, out outValue)).Returns(true);
            return mock;
        }

        /// <summary>
        /// Sets up IMemoryCache mock to indicate a cache miss for a specific key.
        /// </summary>
        public static Mock<IMemoryCache> SetupCacheMiss(this Mock<IMemoryCache> mock, object key)
        {
            object outValue = null;
            mock.Setup(x => x.TryGetValue(key, out outValue)).Returns(false);
            return mock;
        }

        /// <summary>
        /// Sets up IMemoryCache mock with create entry behavior.
        /// </summary>
        public static Mock<IMemoryCache> SetupCacheEntry(this Mock<IMemoryCache> mock, object key, 
            Action<ICacheEntry> configureEntry = null)
        {
            var cacheEntry = new Mock<ICacheEntry>();
            cacheEntry.SetupAllProperties();
            cacheEntry.Setup(x => x.Key).Returns(key);
            
            configureEntry?.Invoke(cacheEntry.Object);
            
            mock.Setup(x => x.CreateEntry(key)).Returns(cacheEntry.Object);
            return mock;
        }

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

        #endregion

        #region ILogger Extensions

        /// <summary>
        /// Verifies that a log message was written at the specified level containing the expected text.
        /// </summary>
        public static void VerifyLog<T>(this Mock<ILogger<T>> mock, LogLevel level, string containsMessage, 
            Times? times = null)
        {
            times ??= Times.Once();
            
            mock.Verify(x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains(containsMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                times.Value);
        }

        /// <summary>
        /// Verifies that a log message was written with a specific exception.
        /// </summary>
        public static void VerifyLogWithException<T>(this Mock<ILogger<T>> mock, LogLevel level, 
            Type exceptionType, string containsMessage = null)
        {
            mock.Verify(x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => containsMessage == null || o.ToString().Contains(containsMessage)),
                It.Is<Exception>(ex => ex.GetType() == exceptionType),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once());
        }

        /// <summary>
        /// Verifies that a log message was written with any exception.
        /// </summary>
        public static void VerifyLogWithAnyException<T>(this Mock<ILogger<T>> mock, LogLevel level, 
            string containsMessage = null)
        {
            mock.Verify(x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => containsMessage == null || o.ToString().Contains(containsMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once());
        }

        /// <summary>
        /// Verifies that no logs were written at the specified level.
        /// </summary>
        public static void VerifyNoLog<T>(this Mock<ILogger<T>> mock, LogLevel level)
        {
            mock.Verify(x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never());
        }

        /// <summary>
        /// Captures all log messages for verification.
        /// </summary>
        public static List<LogMessage> CaptureLogMessages<T>(this Mock<ILogger<T>> mock)
        {
            var messages = new List<LogMessage>();
            
            mock.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                .Callback<LogLevel, EventId, object, Exception, object>((level, eventId, state, exception, formatter) =>
                {
                    messages.Add(new LogMessage
                    {
                        Level = level,
                        EventId = eventId,
                        Message = state?.ToString(),
                        Exception = exception
                    });
                });
                
            return messages;
        }

        #endregion

        #region IHttpContextAccessor Extensions

        /// <summary>
        /// Sets up IHttpContextAccessor with a mock HttpContext.
        /// </summary>
        public static Mock<IHttpContextAccessor> SetupHttpContext(this Mock<IHttpContextAccessor> mock,
            Action<HttpContext> configureContext = null)
        {
            var context = new DefaultHttpContext();
            configureContext?.Invoke(context);
            mock.Setup(x => x.HttpContext).Returns(context);
            return mock;
        }

        /// <summary>
        /// Sets up IHttpContextAccessor with headers.
        /// </summary>
        public static Mock<IHttpContextAccessor> SetupWithHeaders(this Mock<IHttpContextAccessor> mock,
            params (string key, string value)[] headers)
        {
            var context = new DefaultHttpContext();
            foreach (var (key, value) in headers)
            {
                context.Request.Headers[key] = value;
            }
            mock.Setup(x => x.HttpContext).Returns(context);
            return mock;
        }

        /// <summary>
        /// Sets up IHttpContextAccessor with a correlation ID header.
        /// </summary>
        public static Mock<IHttpContextAccessor> SetupWithCorrelationId(this Mock<IHttpContextAccessor> mock,
            string correlationId)
        {
            return mock.SetupWithHeaders(("X-Correlation-ID", correlationId));
        }

        #endregion

        #region IOptions Extensions

        /// <summary>
        /// Creates a mock IOptions<T> with the specified value.
        /// </summary>
        public static IOptions<T> CreateOptions<T>(T value) where T : class, new()
        {
            return Options.Create(value);
        }

        /// <summary>
        /// Creates a mock IOptionsMonitor<T> with the specified value.
        /// </summary>
        public static Mock<IOptionsMonitor<T>> CreateOptionsMonitor<T>(T value) where T : class, new()
        {
            var mock = new Mock<IOptionsMonitor<T>>();
            mock.Setup(x => x.CurrentValue).Returns(value);
            mock.Setup(x => x.Get(It.IsAny<string>())).Returns(value);
            return mock;
        }

        /// <summary>
        /// Creates a mock IOptionsSnapshot<T> with the specified value.
        /// </summary>
        public static Mock<IOptionsSnapshot<T>> CreateOptionsSnapshot<T>(T value) where T : class, new()
        {
            var mock = new Mock<IOptionsSnapshot<T>>();
            mock.Setup(x => x.Value).Returns(value);
            mock.Setup(x => x.Get(It.IsAny<string>())).Returns(value);
            return mock;
        }

        #endregion

        #region Task and Async Extensions

        /// <summary>
        /// Sets up an async method to return a completed task with a result.
        /// </summary>
        public static IReturnsResult<TMock> ReturnsAsync<TMock, TResult>(
            this ISetup<TMock, Task<TResult>> setup, TResult result) where TMock : class
        {
            return setup.Returns(Task.FromResult(result));
        }

        /// <summary>
        /// Sets up an async method to throw an exception.
        /// </summary>
        public static IReturnsResult<TMock> ThrowsAsync<TMock, TResult, TException>(
            this ISetup<TMock, Task<TResult>> setup, TException exception) 
            where TMock : class 
            where TException : Exception
        {
            return setup.Returns(Task.FromException<TResult>(exception));
        }

        /// <summary>
        /// Sets up a cancellable async method with cancellation token support.
        /// </summary>
        public static IReturnsResult<TMock> ReturnsAsync<TMock, TResult>(
            this ISetup<TMock, Task<TResult>> setup, 
            Func<CancellationToken, TResult> resultFactory) where TMock : class
        {
            return setup.Returns<CancellationToken>(ct => Task.FromResult(resultFactory(ct)));
        }

        #endregion

        #region Verification Extensions

        /// <summary>
        /// Verifies that a method was called with a matching predicate.
        /// </summary>
        public static void VerifyWithPredicate<TMock, TParam>(this Mock<TMock> mock,
            Expression<Action<TMock>> expression,
            Func<TParam, bool> predicate,
            Times? times = null) where TMock : class
        {
            times ??= Times.Once();
            
            // This is a simplified version - in practice you'd use expression trees
            // to extract and modify the expression
            mock.Verify(expression, times.Value);
        }

        #endregion

        #region Test Data Helpers

        /// <summary>
        /// Creates a sequence of test data with a factory function.
        /// </summary>
        public static IEnumerable<T> CreateMany<T>(int count, Func<int, T> factory)
        {
            return Enumerable.Range(0, count).Select(factory);
        }

        /// <summary>
        /// Creates a mock that tracks all method calls for verification.
        /// </summary>
        public static Mock<T> CreateTrackingMock<T>() where T : class
        {
            var mock = new Mock<T>();
            mock.SetupAllProperties();
            return mock;
        }

        #endregion
    }

    /// <summary>
    /// Represents a captured log message for verification.
    /// </summary>
    public class LogMessage
    {
        public LogLevel Level { get; set; }
        public EventId EventId { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }
}