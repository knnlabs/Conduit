using System;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Base class for CostCalculationService tests with shared setup and helpers
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Phase", "2")]
    [Trait("Component", "Core")]
    public abstract class CostCalculationServiceTestBase : TestBase
    {
        protected readonly Mock<IModelCostService> _modelCostServiceMock;
        protected readonly Mock<ILogger<CostCalculationService>> _loggerMock;
        protected readonly CostCalculationService _service;

        protected CostCalculationServiceTestBase(ITestOutputHelper output) : base(output)
        {
            _modelCostServiceMock = new Mock<IModelCostService>();
            _loggerMock = CreateLogger<CostCalculationService>();
            _service = new CostCalculationService(_modelCostServiceMock.Object, _loggerMock.Object);
        }
    }
}