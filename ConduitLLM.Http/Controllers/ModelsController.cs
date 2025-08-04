using System;
using System.Linq;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Handles model listing requests following OpenAI's API format.
    /// </summary>
    [ApiController]
    [Route("v1")]
    [Authorize(Policy = "RequireVirtualKey")]
    [Tags("Models")]
    public class ModelsController : ControllerBase
    {
        private readonly ILLMRouter _router;
        private readonly ILogger<ModelsController> _logger;

        public ModelsController(
            ILLMRouter router,
            ILogger<ModelsController> logger)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Lists available models.
        /// </summary>
        /// <returns>A list of available models.</returns>
        [HttpGet("models")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status500InternalServerError)]
        public IActionResult ListModels()
        {
            try
            {
                _logger.LogInformation("Getting available models");

                // Get model names from the router
                var modelNames = _router.GetAvailableModels();

                // Convert to OpenAI format
                var basicModelData = modelNames.Select(m => new
                {
                    id = m,
                    @object = "model"
                }).ToList();

                // Create the response envelope
                var response = new
                {
                    data = basicModelData,
                    @object = "list"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving models list");
                return StatusCode(500, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = ex.Message,
                        Type = "server_error",
                        Code = "internal_error"
                    }
                });
            }
        }
    }
}