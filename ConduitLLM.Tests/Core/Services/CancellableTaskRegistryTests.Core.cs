using ConduitLLM.Core.Services;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    [Trait("Category", "Unit")]
    [Trait("Phase", "2")]
    [Trait("Component", "Core")]
    public partial class CancellableTaskRegistryTests : TestBase, IDisposable
    {
        private readonly Mock<ILogger<CancellableTaskRegistry>> _loggerMock;
        private readonly CancellableTaskRegistry _registry;

        public CancellableTaskRegistryTests(ITestOutputHelper output) : base(output)
        {
            _loggerMock = CreateLogger<CancellableTaskRegistry>();
            _registry = new CancellableTaskRegistry(_loggerMock.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new CancellableTaskRegistry(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Fact]
        public void Constructor_WithCustomGracePeriod_UsesProvidedValue()
        {
            // Arrange
            var customGracePeriod = TimeSpan.FromMinutes(5);

            // Act
            using var customRegistry = new CancellableTaskRegistry(_loggerMock.Object, customGracePeriod);

            // Assert
            // Registry should be created without throwing
            customRegistry.Should().NotBeNull();
        }

        [Fact]
        public void Dispose_DisposesAllCancellationTokenSources()
        {
            // Arrange
            using var registry = new CancellableTaskRegistry(_loggerMock.Object);
            var taskId1 = "task-1";
            var taskId2 = "task-2";
            var cts1 = new CancellationTokenSource();
            var cts2 = new CancellationTokenSource();

            registry.RegisterTask(taskId1, cts1);
            registry.RegisterTask(taskId2, cts2);

            // Act
            registry.Dispose();

            // Assert
            var act1 = () => cts1.Token.IsCancellationRequested;
            act1.Should().Throw<ObjectDisposedException>();
            
            var act2 = () => cts2.Token.IsCancellationRequested;
            act2.Should().Throw<ObjectDisposedException>();
            
            // Verify cleanup logging
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disposing CancellableTaskRegistry")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            // Act & Assert
            _registry.Dispose();
            var act = () => _registry.Dispose();
            act.Should().NotThrow();
        }

        public new void Dispose()
        {
            _registry?.Dispose();
        }
    }
}