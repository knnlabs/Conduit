using ConduitLLM.Core.Models;

using Microsoft.AspNetCore.Authorization;

namespace ConduitLLM.Gateway.Endpoints
{
    /// <summary>
    /// Handles legacy completion requests.
    /// </summary>
    public class CompletionsEndpoints : GatewayEndpointHandlerBase
    {
        public CompletionsEndpoints(IHttpContextAccessor httpContextAccessor, ILogger<CompletionsEndpoints> logger)
            : base(null, httpContextAccessor, logger)
        {
        }

        /// <summary>
        /// Legacy completions endpoint - not implemented.
        /// </summary>
        /// <returns>A 501 Not Implemented response directing users to use /chat/completions.</returns>
        public IResult CreateCompletion()
        {
            Logger.LogInformation("Legacy /completions endpoint called.");
            return OpenAIError(501, "The /completions endpoint is not implemented. Please use /chat/completions.", "not_implemented");
        }
    }
}
