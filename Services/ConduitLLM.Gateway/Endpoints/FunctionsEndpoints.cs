using System.Text.Json;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Gateway.UsageTracking;

namespace ConduitLLM.Gateway.Endpoints;

/// <summary>
/// Controller for executing functions (e.g., Exa search) through the Gateway API.
/// </summary>
public class FunctionsEndpoints : GatewayEndpointHandlerBase
{
    private readonly IFunctionExecutionService _executionService;
    private readonly IFunctionConfigurationRepository _configurationRepository;
    private readonly ConduitLLM.Functions.Services.FunctionParameterValidationService _validationService;
    private readonly ILogger<FunctionsEndpoints> _logger;

    /// <summary>
    /// Initializes the Functions endpoint handler.
    /// </summary>
    public FunctionsEndpoints(
        IFunctionExecutionService executionService,
        IFunctionConfigurationRepository configurationRepository,
        ConduitLLM.Functions.Services.FunctionParameterValidationService validationService,
        IEventBus eventBus,
        IHttpContextAccessor httpContextAccessor,
        ILogger<FunctionsEndpoints> logger)
        : base(eventBus, httpContextAccessor, logger)
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
    public async Task<IResult> ExecuteFunction(
        FunctionExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null)
            {
                return OpenAIError(400, "Request body is required", "invalid_request");
            }

            // Get virtual key ID from authentication context
            var virtualKeyId = User.FindFirst("VirtualKeyId")?.Value;
            if (string.IsNullOrEmpty(virtualKeyId) || !int.TryParse(virtualKeyId, out var keyId))
            {
                _logger.LogWarning("Invalid or missing VirtualKeyId claim");
                return OpenAIError(401, "Invalid authentication", "invalid_auth", "authentication_error");
            }

            // Validate function configuration exists and is enabled
            var configuration = await _configurationRepository.GetByIdAsync(request.FunctionConfigurationId, cancellationToken);
            if (configuration == null)
            {
                return OpenAIError(404, $"Function configuration {request.FunctionConfigurationId} not found", "not_found", "not_found_error");
            }

            if (!configuration.IsEnabled)
            {
                return OpenAIError(400, $"Function configuration {request.FunctionConfigurationId} is disabled", "invalid_request");
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

                return OpenAIError(400, $"Parameter validation failed: {string.Join("; ", validationResult.Errors)}", "invalid_request");
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
            HttpContext.Items["FunctionConfigurationName"] = configuration.ConfigurationName;
            var accounting = HttpContext.GetOrCreateRequestAccountingContext();
            accounting.SetOperation(RequestOperation.Function, keyId, configuration.ConfigurationName);

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
                cancellationToken: cancellationToken);

            // Store execution info for middleware billing
            HttpContext.Items["FunctionExecutionId"] = execution.Id;
            HttpContext.Items["EstimatedCost"] = execution.EstimatedCost;
            HttpContext.Items["ActualCost"] = execution.ActualCost;
            var actualCost = execution.ActualCost ?? execution.EstimatedCost ?? 0m;
            accounting.RecordDirectCost(new DirectCostEvidence(
                configuration.ConfigurationName,
                actualCost,
                execution.Id.ToString(),
                JsonSerializer.Serialize(new
                {
                    functionConfigurationId = configuration.Id,
                    executionId = execution.Id,
                    state = execution.State.ToString()
                })));

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
            return OpenAIError(400, ex.Message, "invalid_request");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function");
            return OpenAIError(500, "An unexpected error occurred during function execution", "internal_error", "server_error");
        }
    }

    /// <summary>
    /// Gets the status and result of a function execution.
    /// </summary>
    /// <param name="executionId">The execution ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The function execution details</returns>
    public async Task<IResult> GetExecution(
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
                return OpenAIError(401, "Invalid authentication", "invalid_auth", "authentication_error");
            }

            var execution = await _executionService.GetExecutionAsync(executionId, cancellationToken);

            if (execution == null)
            {
                return OpenAIError(404, $"Function execution {executionId} not found", "not_found", "not_found_error");
            }

            // Verify the execution belongs to the authenticated virtual key
            if (execution.VirtualKeyId != keyId)
            {
                _logger.LogWarning(
                    "Virtual key {VirtualKeyId} attempted to access execution {ExecutionId} owned by key {OwnerKeyId}",
                    keyId, executionId, execution.VirtualKeyId);
                return OpenAIError(404, $"Function execution {executionId} not found", "not_found", "not_found_error");
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
            return OpenAIError(500, "An unexpected error occurred", "internal_error", "server_error");
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
