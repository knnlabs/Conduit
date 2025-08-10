using System;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Unit tests for the RouterService class.
    /// </summary>
    public partial class RouterServiceTests : TestBase
    {
        private readonly Mock<ILLMRouter> _routerMock;
        private readonly Mock<IRouterConfigRepository> _repositoryMock;
        private readonly Mock<ILogger<RouterService>> _loggerMock;
        private readonly RouterService _service;

        public RouterServiceTests(ITestOutputHelper output) : base(output)
        {
            _routerMock = new Mock<ILLMRouter>();
            _repositoryMock = new Mock<IRouterConfigRepository>();
            _loggerMock = CreateLogger<RouterService>();

            _service = new RouterService(
                _routerMock.Object,
                _repositoryMock.Object,
                _loggerMock.Object);
        }
    }
}