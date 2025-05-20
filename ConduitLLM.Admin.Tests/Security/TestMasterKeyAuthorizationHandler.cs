using ConduitLLM.Admin.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace ConduitLLM.Admin.Tests.Security
{
    /// <summary>
    /// Test version of MasterKeyAuthorizationHandler that exposes the HandleRequirementAsync method for testing
    /// </summary>
    public class TestMasterKeyAuthorizationHandler : MasterKeyAuthorizationHandler
    {
        public TestMasterKeyAuthorizationHandler(
            IConfiguration configuration,
            ILogger<MasterKeyAuthorizationHandler> logger)
            : base(configuration, logger)
        {
        }

        /// <summary>
        /// Public wrapper for the protected HandleRequirementAsync method
        /// </summary>
        public Task TestHandleRequirementAsync(
            AuthorizationHandlerContext context,
            MasterKeyRequirement requirement)
        {
            return HandleRequirementAsync(context, requirement);
        }
    }
}