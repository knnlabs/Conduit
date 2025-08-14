using System;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Unit tests for the SecurityEventMonitoringService class.
    /// This class is split into multiple partial class files:
    /// - SecurityEventMonitoringServiceTests.Setup.cs: Setup, mocks, helper methods and test models
    /// - SecurityEventMonitoringServiceTests.Constructor.cs: Constructor tests
    /// - SecurityEventMonitoringServiceTests.RecordEvent.cs: RecordEvent tests
    /// - SecurityEventMonitoringServiceTests.Query.cs: Get/Query event tests
    /// - SecurityEventMonitoringServiceTests.IpManagement.cs: IP blocking/unblocking tests
    /// - SecurityEventMonitoringServiceTests.Exceptions.cs: Exception handling tests
    /// </summary>
    [Xunit.Trait("Category", "Unit")]
    [Xunit.Trait("Component", "Security")]
    public partial class SecurityEventMonitoringServiceTests
    {
        // The implementation is split across the partial class files
    }
}