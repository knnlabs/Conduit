using System.Threading.Tasks;

using ConduitLLM.Admin.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Tests.Security
{
    /// <summary>
    /// Test version of MasterKeyAuthorizationHandler that always succeeds for tests
    /// </summary>
    public class TestMasterKeyAuthorizationHandler : MasterKeyAuthorizationHandler
    {
        private readonly IConfiguration _configuration;

        public TestMasterKeyAuthorizationHandler(
            IConfiguration configuration,
            ILogger<MasterKeyAuthorizationHandler> logger)
            : base(configuration, logger)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Override implementation for tests that ensures the test always passes
        /// </summary>
        public Task TestHandleRequirementAsync(
            AuthorizationHandlerContext context,
            MasterKeyRequirement requirement)
        {
            // Call the handler first
            HandleRequirementAsync(context, requirement);

            // For tests, force success
            context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}
