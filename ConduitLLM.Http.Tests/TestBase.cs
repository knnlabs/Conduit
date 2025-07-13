using System;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace ConduitLLM.Http.Tests
{
    /// <summary>
    /// Base class for all HTTP unit tests providing common setup and utilities.
    /// </summary>
    public abstract class TestBase : IDisposable
    {
        protected readonly ITestOutputHelper Output;
        protected readonly ILogger Logger;
        private readonly Mock<ILogger> _loggerMock;
        protected bool Disposed { get; private set; }

        protected TestBase(ITestOutputHelper output)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            
            // Create a default logger that outputs to xUnit test output
            _loggerMock = new Mock<ILogger>();
            Logger = _loggerMock.Object;
        }

        /// <summary>
        /// Creates a logger mock for a specific type
        /// </summary>
        protected Mock<ILogger<T>> CreateLogger<T>()
        {
            var logger = new Mock<ILogger<T>>();
            SetupLogger(logger);
            return logger;
        }

        /// <summary>
        /// Sets up logger to write to xUnit test output
        /// </summary>
        private void SetupLogger<T>(Mock<ILogger<T>> logger)
        {
            logger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                .Callback<LogLevel, EventId, object, Exception, object>((level, eventId, state, exception, formatter) =>
                {
                    // Guard against using Output after test completion
                    if (!Disposed)
                    {
                        try
                        {
                            var message = state?.ToString() ?? string.Empty;
                            if (exception != null)
                            {
                                message += $" Exception: {exception}";
                            }
                            Output.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{level}] {message}");
                        }
                        catch (InvalidOperationException)
                        {
                            // Test output is no longer available - ignore
                        }
                    }
                });
        }

        /// <summary>
        /// Creates a cancellation token that times out after the specified duration
        /// </summary>
        protected CancellationToken CreateCancellationToken(TimeSpan? timeout = null)
        {
            var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));
            return cts.Token;
        }

        /// <summary>
        /// Logs a message to the test output
        /// </summary>
        protected void Log(string message)
        {
            Output.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
        }

        /// <summary>
        /// Logs a formatted message to the test output
        /// </summary>
        protected void Log(string format, params object[] args)
        {
            Output.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {string.Format(format, args)}");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    // Dispose of any managed resources if needed
                }
                Disposed = true;
            }
        }
    }
}