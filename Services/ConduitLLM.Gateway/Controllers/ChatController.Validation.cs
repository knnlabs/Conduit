using ConduitLLM.Core.Models;
using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Gateway.Controllers
{
    public partial class ChatController
    {
        /// <summary>
        /// Validates function calling request parameters.
        /// </summary>
        /// <returns>Error result if validation fails, null if validation succeeds.</returns>
        private async Task<IActionResult?> ValidateFunctionCallRequestAsync(
            ChatCompletionRequest request,
            CancellationToken cancellationToken)
        {
            if (_functionConfigRepository == null)
            {
                _logger.LogError("Function calling requested but IFunctionConfigurationRepository is not available");
                return StatusCode(500, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Function calling is not configured on this server",
                        Type = "server_error",
                        Code = "function_calling_unavailable"
                    }
                });
            }

            try
            {
                var functionConfigs = await _functionConfigRepository.GetByIdsAsync(
                    request.FunctionConfigurationIds!,
                    cancellationToken);

                var missingIds = request.FunctionConfigurationIds!
                    .Except(functionConfigs.Select(fc => fc.Id))
                    .ToList();

                if (missingIds.Count > 0)
                {
                    _logger.LogWarning("Function calling request includes non-existent function configuration IDs: {MissingIds}",
                        string.Join(", ", missingIds));
                    return BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = $"Function configuration IDs not found: {string.Join(", ", missingIds)}",
                            Type = "invalid_request_error",
                            Code = "invalid_function_configuration_ids"
                        }
                    });
                }

                var disabledConfigs = functionConfigs.Where(fc => !fc.IsEnabled).ToList();
                if (disabledConfigs.Count > 0)
                {
                    var disabledIds = string.Join(", ", disabledConfigs.Select(fc => fc.Id));
                    _logger.LogWarning("Function calling request includes disabled function configurations: {DisabledIds}", disabledIds);
                    return BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = $"Function configurations are disabled: {disabledIds}",
                            Type = "invalid_request_error",
                            Code = "disabled_function_configurations"
                        }
                    });
                }

                if (request.MaxAgenticIterations.HasValue)
                {
                    var minIterations = await _globalSettingsCacheService.GetMinAgenticIterationsAsync();
                    var maxIterations = await _globalSettingsCacheService.GetMaxAgenticIterationsAsync();

                    if (request.MaxAgenticIterations.Value < minIterations || request.MaxAgenticIterations.Value > maxIterations)
                    {
                        _logger.LogWarning("Invalid MaxAgenticIterations value: {Value}, valid range is {Min}-{Max}",
                            request.MaxAgenticIterations.Value, minIterations, maxIterations);
                        return BadRequest(new OpenAIErrorResponse
                        {
                            Error = new OpenAIError
                            {
                                Message = $"MaxAgenticIterations must be between {minIterations} and {maxIterations}",
                                Type = "invalid_request_error",
                                Code = "invalid_max_agentic_iterations"
                            }
                        });
                    }
                }

                _logger.LogDebug("Function calling validation passed for {Count} function configurations",
                    functionConfigs.Count);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating function calling request");
                return StatusCode(500, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Error validating function calling request",
                        Type = "server_error",
                        Code = "function_validation_error"
                    }
                });
            }
        }
    }
}
