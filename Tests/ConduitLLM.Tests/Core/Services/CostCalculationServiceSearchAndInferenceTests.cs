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