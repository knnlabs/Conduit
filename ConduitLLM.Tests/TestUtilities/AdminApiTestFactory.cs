using System.Collections.Generic;

namespace ConduitLLM.Tests.TestUtilities
{
    /// <summary>
    /// Test factory specifically configured for Admin API tests
    /// </summary>
    public class AdminApiTestFactory : TestWebApplicationFactory<ConduitLLM.Admin.Program>
    {
        public AdminApiTestFactory()
        {
            // Use parent's in-memory database configuration
            AdditionalConfiguration["AdminApi:MasterKey"] = "test-master-key";
            // Parent class already configures in-memory database
        }
    }
}