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