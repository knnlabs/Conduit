using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
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