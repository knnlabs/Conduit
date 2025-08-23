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
    public partial class AudioEncryptionServiceTests : TestBase
    {
        private readonly Mock<ILogger<AudioEncryptionService>> _loggerMock;
        private readonly AudioEncryptionService _service;

        public AudioEncryptionServiceTests(ITestOutputHelper output) : base(output)
        {
            _loggerMock = CreateLogger<AudioEncryptionService>();
            _service = new AudioEncryptionService(_loggerMock.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new AudioEncryptionService(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }
    }
}