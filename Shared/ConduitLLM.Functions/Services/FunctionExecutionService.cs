using System.Diagnostics;
using System.Text.Json;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Functions.Services;

/// <summary>
/// Service for executing functions.
/// </summary>
/// <remarks>
/// This service orchestrates function execution:
/// 1. Load function configuration and credentials
/// 2. Create execution record
/// 3. Execute function via provider client
/// 4. Calculate actual usage and cost
/// 5. Update execution record with results
///
/// Billing is handled by the calling layer (middleware/controller).
/// </remarks>
public class FunctionExecutionService : IFunctionExecutionService
{
    private readonly IFunctionConfigurationRepository _functionConfigurationRepository;
    private readonly IFunctionCredentialRepository _credentialRepository;
    private readonly IFunctionExecutionRepository _executionRepository;
    private readonly IFunctionCostCalculationService _costCalculationService;
    private readonly IFunctionClientFactory _clientFactory;
    private readonly ILogger<FunctionExecutionService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FunctionExecutionService(
        IFunctionConfigurationRepository functionConfigurationRepository,
        IFunctionCredentialRepository credentialRepository,
        IFunctionExecutionRepository executionRepository,
        IFunctionCostCalculationService costCalculationService,
        IFunctionClientFactory clientFactory,
        ILogger<FunctionExecutionService> logger)
    {
        _functionConfigurationRepository = functionConfigurationRepository ?? throw new ArgumentNullException(nameof(functionConfigurationRepository));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
        _costCalculationService = costCalculationService ?? throw new ArgumentNullException(nameof(costCalculationService));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <inheritdoc />
    public async Task<FunctionExecution> ExecuteAsync(
        int functionConfigurationId,
        int virtualKeyId,
        Dictionary<string, object> parameters,
        string? idempotencyKey = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var stopwatch = Stopwatch.StartNew();
        FunctionExecution? execution = null;

        try
        {
            // 1. Load function configuration
            var configuration = await _functionConfigurationRepository.GetByIdAsync(functionConfigurationId, cancellationToken);
            if (configuration == null)
            {
                throw new InvalidOperationException($"Function configuration {functionConfigurationId} not found");
            }

             if (!configuration.IsEnabled)
            {
                throw new InvalidOperationException($"Function configuration {functionConfigurationId} is not enabled");
            }

            _logger.LogInformation("Executing function {ConfigName} (Config: {ConfigId}) for VirtualKey {VirtualKeyId}",
                configuration.ConfigurationName, functionConfigurationId, virtualKeyId);

            // 2. Estimate cost conservatively (for logging and execution record)
            var estimatedCost = await _costCalculationService.EstimateCostAsync(functionConfigurationId, parameters, cancellationToken);

            _logger.LogDebug("Estimated cost for function {ConfigName}: ${EstimatedCost:F4}",
                configuration.ConfigurationName, estimatedCost);

            // 3. Create execution record
            var requestData = new
            {
                parameters,
                metadata,
                idempotencyKey
            };

            execution = new FunctionExecution
            {
                Id = Guid.NewGuid(),
                FunctionConfigurationId = functionConfigurationId,
                VirtualKeyId = virtualKeyId,
                ExecutionMode = configuration.DefaultExecutionMode,
                State = ExecutionState.Running,
                RequestedAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow,
                RequestJson = JsonSerializer.Serialize(requestData, _jsonOptions),
                EstimatedCost = estimatedCost,
                RetryCount = 0,
                Version = 1,
                WebhookDelivered = false
            };

            await _executionRepository.CreateAsync(execution, cancellationToken);

            _logger.LogInformation("Created execution record {ExecutionId} for function {ConfigName}",
                execution.Id, configuration.ConfigurationName);

            // 4. Get credentials
            var credentials = await _credentialRepository.GetByFunctionConfigurationIdAsync(
                functionConfigurationId, cancellationToken);

            if (!credentials.Any())
            {
                throw new InvalidOperationException(
                    $"No credentials configured for function configuration {functionConfigurationId}");
            }

            // Use first enabled credential for now (could implement rotation/load balancing later)
            var credential = credentials.FirstOrDefault(c => c.IsEnabled);
            if (credential == null)
            {
                throw new InvalidOperationException(
                    $"No enabled credentials found for function configuration {functionConfigurationId}");
            }

            // 5. Execute function via provider client
            var client = _clientFactory.GetClient(configuration.ProviderType, functionConfigurationId);

            _logger.LogInformation("Executing {ProviderType} function via client...",
                configuration.ProviderType);

            var result = await client.ExecuteAsync(parameters, credential.ApiKey, cancellationToken);

            stopwatch.Stop();

            // 6. Calculate actual usage and cost
            var usage = client.CalculateUsageFromResponse(parameters, result);
            var actualCost = await _costCalculationService.CalculateCostAsync(
                functionConfigurationId, usage, cancellationToken);

            _logger.LogInformation("Function execution completed. Estimated: ${Estimated:F4}, Actual: ${Actual:F4}, Duration: {Duration}ms",
                estimatedCost, actualCost, stopwatch.ElapsedMilliseconds);

            // 7. Update execution record with results
            var costDetails = new
            {
                usage,
                estimatedCost,
                actualCost,
                httpStatusCode = result.HttpStatusCode
            };

            execution.State = result.IsSuccess ? ExecutionState.Completed : ExecutionState.Failed;
            execution.CompletedAt = DateTime.UtcNow;
            execution.Duration = stopwatch.Elapsed;
            execution.ResponseJson = result.ResponseJson;
            execution.ErrorMessage = result.ErrorMessage;
            execution.CostCalculationDetails = JsonSerializer.Serialize(costDetails, _jsonOptions);
            execution.ActualCost = actualCost;

            await _executionRepository.UpdateAsync(execution, cancellationToken);

            return execution;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Error executing function {ConfigId}: {Error}",
                functionConfigurationId, ex.Message);

            // Create/update execution record with error if we can
            try
            {
                if (execution == null)
                {
                    // Create failed execution record
                    var requestData = new
                    {
                        parameters,
                        metadata,
                        idempotencyKey
                    };

                    execution = new FunctionExecution
                    {
                        Id = Guid.NewGuid(),
                        FunctionConfigurationId = functionConfigurationId,
                        VirtualKeyId = virtualKeyId,
                        ExecutionMode = ExecutionMode.Synchronous, // Default for error case
                        State = ExecutionState.Failed,
                        RequestedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow,
                        Duration = stopwatch.Elapsed,
                        RequestJson = JsonSerializer.Serialize(requestData, _jsonOptions),
                        ActualCost = 0m,
                        ErrorMessage = ex.Message,
                        RetryCount = 0,
                        Version = 1,
                        WebhookDelivered = false
                    };

                    await _executionRepository.CreateAsync(execution, cancellationToken);
                }
                else
                {
                    // Update existing execution with error
                    execution.State = ExecutionState.Failed;
                    execution.CompletedAt = DateTime.UtcNow;
                    execution.Duration = stopwatch.Elapsed;
                    execution.ErrorMessage = ex.Message;
                    execution.ActualCost = 0m;

                    await _executionRepository.UpdateAsync(execution, cancellationToken);
                }
            }
            catch (Exception recordEx)
            {
                _logger.LogError(recordEx, "Failed to record execution error for function {ConfigId}",
                    functionConfigurationId);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<FunctionExecution?> GetExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        return await _executionRepository.GetByIdAsync(executionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<FunctionExecution>> ListExecutionsAsync(
        int virtualKeyId,
        CancellationToken cancellationToken = default)
    {
        return await _executionRepository.GetByVirtualKeyIdAsync(virtualKeyId, cancellationToken);
    }
}
