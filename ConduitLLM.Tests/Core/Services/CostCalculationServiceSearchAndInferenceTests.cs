using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Tests for search units and inference steps pricing functionality
    /// </summary>
    public partial class CostCalculationServiceSearchAndInferenceTests : CostCalculationServiceTestBase
    {
        public CostCalculationServiceSearchAndInferenceTests(ITestOutputHelper output) : base(output)
        {
        }
    }
}