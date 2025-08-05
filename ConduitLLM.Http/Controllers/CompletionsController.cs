using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Handles legacy completion requests.
    /// </summary>
    [ApiController]
    [Route("v1")]
    [Authorize(AuthenticationSchemes = "VirtualKey")]
    [Tags("Completions")]
    public class CompletionsController : ControllerBase
    {
        private readonly ILogger<CompletionsController> _logger;

        public CompletionsController(ILogger<CompletionsController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Legacy completions endpoint - not implemented.
        /// </summary>
        /// <returns>A 501 Not Implemented response directing users to use /chat/completions.</returns>
        [HttpPost("completions")]
        [ProducesResponseType(typeof(object), StatusCodes.Status501NotImplemented)]
        public IActionResult CreateCompletion()
        {
            _logger.LogInformation("Legacy /completions endpoint called.");
            return StatusCode(501, new
            {
                error = "The /completions endpoint is not implemented. Please use /chat/completions."
            });
        }
    }
}