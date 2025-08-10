using System;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Admin.TestHelpers
{
    /// <summary>
    /// Extension methods for mocking ILogger in Admin tests.
    /// </summary>
    public static class LoggerMockExtensions
    {
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
            Exception exception, string containsMessage = null)
        {
            mock.Verify(x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => containsMessage == null || o.ToString().Contains(containsMessage)),
                exception,
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
    }
}