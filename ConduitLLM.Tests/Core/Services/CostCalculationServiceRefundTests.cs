using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Tests for refund calculation functionality
    /// </summary>
    public partial class CostCalculationServiceRefundTests : CostCalculationServiceTestBase
    {
        public CostCalculationServiceRefundTests(ITestOutputHelper output) : base(output)
        {
        }
    }
}