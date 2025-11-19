using System.Text.Json;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Http.Authorization;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Http.Controllers;

/// <summary>
/// Controller for executing functions (e.g., Exa search) through the Core API.
/// </summary>
[ApiController]
[Route("v1/functions")]
[Authorize]
[RequireBalance]
[Tags("Functions")]
public class FunctionsController : EventPublishingControllerBase
{
    private readonly IFunctionExecutionService _executionService;
    private readonly IFunctionConfigurationRepository _configurationRepository;
    private readonly ConduitLLM.Functions.Services.FunctionParameterValidationService _validationService;
    private readonly ILogger<FunctionsController> _logger;

    /// <summary>
    /// Initializes a new instance of the FunctionsController.
    /// </summary>
    public FunctionsController(
        IFunctionExecutionService executionService,
        IFunctionConfigurationRepository configurationRepository,
        ConduitLLM.Functions.Services.FunctionParameterValidationService validationService,
        IPublishEndpoint publishEndpoint,
        ILogger<FunctionsController> logger)
        : base(publishEndpoint, logger)
    {
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes a function with the provided parameters.
    /// </summary>
    /// <param name="request">The function execution request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The function execution result</returns>
    [HttpPost("execute")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExecuteFunction(
        [FromBody] FunctionExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new ErrorResponseDto("Request body is required"));
            }

            // Get virtual key ID from authentication context
            var virtualKeyId = User.FindFirst("VirtualKeyId")?.Value;
            if (string.IsNullOrEmpty(virtualKeyId) || !int.TryParse(virtualKeyId, out var keyId))
            {
                _logger.LogWarning("Invalid or missing VirtualKeyId claim");
                return Unauthorized(new ErrorResponseDto("Invalid authentication"));
            }

            // Validate function configuration exists and is enabled
            var configuration = await _configurationRepository.GetByIdAsync(request.FunctionConfigurationId, cancellationToken);
            if (configuration == null)
            {
                return NotFound(new ErrorResponseDto($"Function configuration {request.FunctionConfigurationId} not found"));
            }

            if (!configuration.IsEnabled)
            {
                return BadRequest(new ErrorResponseDto($"Function configuration {request.FunctionConfigurationId} is disabled"));
            }

            // Validate parameters against schema if available
            var parameters = request.Parameters ?? new Dictionary<string, object>();
            var validationResult = _validationService.ValidateParameters(parameters, configuration.ParameterSchema);

            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Parameter validation failed for function {FunctionName} (config {ConfigId}): {Errors}",
                    configuration.ConfigurationName,
                    configuration.Id,
                    string.Join(", ", validationResult.Errors));

                return BadRequest(new ErrorResponseDto(
                    $"Parameter validation failed: {string.Join("; ", validationResult.Errors)}"));
            }

            if (validationResult.Warnings.Count > 0)
            {
                _logger.LogWarning(
                    "Parameter validation warnings for function {FunctionName} (config {ConfigId}): {Warnings}",
                    configuration.ConfigurationName,
                    configuration.Id,
                    string.Join(", ", validationResult.Warnings));
            }

            // Store provider info for middleware usage tracking
            HttpContext.Items["ProviderId"] = configuration.ProviderType;
            HttpContext.Items["ProviderType"] = configuration.ProviderType;
            HttpContext.Items["FunctionConfigurationId"] = configuration.Id;

            _logger.LogInformation(
                "Executing function {FunctionName} (config {ConfigId}) for virtual key {VirtualKeyId}",
                configuration.ConfigurationName,
                configuration.Id,
                keyId);

            // Execute the function
            var execution = await _executionService.ExecuteAsync(
                request.FunctionConfigurationId,
                keyId,
                request.Parameters ?? new Dictionary<string, object>(),
                request.IdempotencyKey,
                request.Metadata,
                cancellationToken);

            // Store execution info for middleware billing
            HttpContext.Items["FunctionExecutionId"] = execution.Id;
            HttpContext.Items["EstimatedCost"] = execution.EstimatedCost;
            HttpContext.Items["ActualCost"] = execution.ActualCost;

            // Return execution result
            var response = new FunctionExecutionResponse
            {
                ExecutionId = execution.Id,
                FunctionConfigurationId = execution.FunctionConfigurationId,
                State = execution.State.ToString(),
                Result = execution.ResponseJson != null
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(execution.ResponseJson)
                    : null,
                ErrorMessage = execution.ErrorMessage,
                EstimatedCost = execution.EstimatedCost,
                ActualCost = execution.ActualCost,
                StartedAt = execution.StartedAt,
                CompletedAt = execution.CompletedAt,
                Duration = execution.Duration?.TotalMilliseconds != null ? (long)execution.Duration.Value.TotalMilliseconds : null
            };

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid function execution request");
            return BadRequest(new ErrorResponseDto(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponseDto("An unexpected error occurred during function execution"));
        }
    }

    /// <summary>
    /// Gets the status and result of a function execution.
    /// </summary>
    /// <param name="executionId">The execution ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The function execution details</returns>
    [HttpGet("executions/{executionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetExecution(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get virtual key ID from authentication context
            var virtualKeyId = User.FindFirst("VirtualKeyId")?.Value;
            if (string.IsNullOrEmpty(virtualKeyId) || !int.TryParse(virtualKeyId, out var keyId))
            {
                _logger.LogWarning("Invalid or missing VirtualKeyId claim");
                return Unauthorized(new ErrorResponseDto("Invalid authentication"));
            }

            var execution = await _executionService.GetExecutionAsync(executionId, cancellationToken);

            if (execution == null)
            {
                return NotFound(new ErrorResponseDto($"Function execution {executionId} not found"));
            }

            // Verify the execution belongs to the authenticated virtual key
            if (execution.VirtualKeyId != keyId)
            {
                _logger.LogWarning(
                    "Virtual key {VirtualKeyId} attempted to access execution {ExecutionId} owned by key {OwnerKeyId}",
                    keyId, executionId, execution.VirtualKeyId);
                return NotFound(new ErrorResponseDto($"Function execution {executionId} not found"));
            }

            var response = new FunctionExecutionResponse
            {
                ExecutionId = execution.Id,
                FunctionConfigurationId = execution.FunctionConfigurationId,
                State = execution.State.ToString(),
                Result = execution.ResponseJson != null
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(execution.ResponseJson)
                    : null,
                ErrorMessage = execution.ErrorMessage,
                EstimatedCost = execution.EstimatedCost,
                ActualCost = execution.ActualCost,
                StartedAt = execution.StartedAt,
                CompletedAt = execution.CompletedAt,
                Duration = execution.Duration?.TotalMilliseconds != null ? (long)execution.Duration.Value.TotalMilliseconds : null
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function execution {ExecutionId}", executionId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponseDto("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Request model for function execution.
    /// </summary>
    public class FunctionExecutionRequest
    {
        /// <summary>
        /// The function configuration ID to execute.
        /// </summary>
        public int FunctionConfigurationId { get; set; }

        /// <summary>
        /// Parameters to pass to the function.
        /// </summary>
        public Dictionary<string, object>? Parameters { get; set; }

        /// <summary>
        /// Optional metadata to associate with the execution.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Optional idempotency key to prevent duplicate executions.
        /// </summary>
        public string? IdempotencyKey { get; set; }
    }

    /// <summary>
    /// Response model for function execution.
    /// </summary>
    public class FunctionExecutionResponse
    {
        /// <summary>
        /// The unique execution ID.
        /// </summary>
        public Guid ExecutionId { get; set; }

        /// <summary>
        /// The function configuration ID that was executed.
        /// </summary>
        public int FunctionConfigurationId { get; set; }

        /// <summary>
        /// The current state of the execution.
        /// </summary>
        public string State { get; set; } = null!;

        /// <summary>
        /// The function execution result (provider-specific).
        /// </summary>
        public Dictionary<string, object>? Result { get; set; }

        /// <summary>
        /// Error message if execution failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Estimated cost before execution.
        /// </summary>
        public decimal? EstimatedCost { get; set; }

        /// <summary>
        /// Actual cost after execution.
        /// </summary>
        public decimal? ActualCost { get; set; }

        /// <summary>
        /// When the execution started.
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// When the execution completed.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Execution duration in milliseconds.
        /// </summary>
        public long? Duration { get; set; }
    }
}
