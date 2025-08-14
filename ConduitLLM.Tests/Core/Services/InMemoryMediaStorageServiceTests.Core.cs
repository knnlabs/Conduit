using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class InMemoryMediaStorageServiceTests
    {
        private readonly Mock<ILogger<InMemoryMediaStorageService>> _mockLogger;
        private readonly InMemoryMediaStorageService _service;
        private const string TestBaseUrl = "http://localhost:5000";

        public InMemoryMediaStorageServiceTests()
        {
            _mockLogger = new Mock<ILogger<InMemoryMediaStorageService>>();
            _service = new InMemoryMediaStorageService(_mockLogger.Object, TestBaseUrl);
        }
    }
}