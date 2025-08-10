using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Tests for basic cost calculation functionality
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Phase", "2")]
    [Trait("Component", "Core")]
    public partial class CostCalculationServiceBasicTests : CostCalculationServiceTestBase
    {
        public CostCalculationServiceBasicTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Constructor_WithNullModelCostService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new CostCalculationService(null!, _loggerMock.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("modelCostService");
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new CostCalculationService(_modelCostServiceMock.Object, null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }
    }
}