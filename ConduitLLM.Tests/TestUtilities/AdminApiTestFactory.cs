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
            AdditionalConfiguration = new Dictionary<string, string?>
            {
                { "AdminApi:MasterKey", "test-master-key" },
                { "ConnectionStrings:ConfigurationDb", "Data Source=:memory:" }
            };
        }
    }
}