using ConduitLLM.Configuration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace ConduitLLM.Http.Security
{
    /// <summary>
    /// Authorization handler for master key requirement
    /// </summary>
    public class MasterKeyAuthorizationHandler : AuthorizationHandler<MasterKeyRequirement>
    {
        private readonly IGlobalSettingService _globalSettingService;
        private readonly ILogger<MasterKeyAuthorizationHandler> _logger;
        
        /// <summary>
        /// Initializes a new instance of the MasterKeyAuthorizationHandler
        /// </summary>
        /// <param name="globalSettingService">Service for accessing global settings</param>
        /// <param name="logger">Logger</param>
        public MasterKeyAuthorizationHandler(
            IGlobalSettingService globalSettingService,
            ILogger<MasterKeyAuthorizationHandler> logger)
        {
            _globalSettingService = globalSettingService;
            _logger = logger;
        }
        
        /// <inheritdoc/>
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            MasterKeyRequirement requirement)
        {
            // This handler requires HttpContext
            if (!(context.Resource is HttpContext httpContext))
            {
                _logger.LogWarning("Master key authorization failed: No HttpContext");
                return;
            }
            
            // Get the master key from the request header
            if (!httpContext.Request.Headers.TryGetValue("X-Master-Key", out var providedKeyValue))
            {
                _logger.LogWarning("Master key authorization failed: No key provided");
                return;
            }
            
            // Get the configured master key from settings
            var storedMasterKey = await _globalSettingService.GetSettingAsync("MasterKey");
            
            if (string.IsNullOrEmpty(storedMasterKey))
            {
                _logger.LogError("Master key is not configured");
                return;
            }
            
            // Validate the key
            if (providedKeyValue.ToString() == storedMasterKey)
            {
                context.Succeed(requirement);
                _logger.LogInformation("Master key authorization succeeded");
            }
            else
            {
                _logger.LogWarning("Master key authorization failed: Invalid key");
            }
        }
    }
}
