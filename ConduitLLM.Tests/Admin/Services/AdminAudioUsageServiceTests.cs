namespace ConduitLLM.Tests.Admin.Services
{
    /// <summary>
    /// Unit tests for the AdminAudioUsageService class.
    /// This partial class contains tests split across multiple files for better organization.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AudioUsage")]
    public partial class AdminAudioUsageServiceTests
    {
        // The implementation is split across the partial class files:
        // - AdminAudioUsageServiceTests.Setup.cs: Constructor and helper methods
        // - AdminAudioUsageServiceTests.UsageLogs.cs: GetUsageLogsAsync tests
        // - AdminAudioUsageServiceTests.Summary.cs: GetUsageSummaryAsync tests
        // - AdminAudioUsageServiceTests.ByKey.cs: GetUsageByKeyAsync tests
        // - AdminAudioUsageServiceTests.ByProvider.cs: GetUsageByProviderAsync tests
        // - AdminAudioUsageServiceTests.RealtimeSessions.cs: Realtime session tests
        // - AdminAudioUsageServiceTests.Export.cs: Export tests
        // - AdminAudioUsageServiceTests.Cleanup.cs: Cleanup tests
    }
}