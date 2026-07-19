using System.Diagnostics;
using System.Text.Json;

using ConduitLLM.Core;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Gateway.Metrics;
using GatewayOpsMetrics = ConduitLLM.Gateway.Services.GatewayOperationsMetricsService;

using ConduitLLM.Configuration.Messaging;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Gateway.Authorization;

namespace ConduitLLM.Gateway.Controllers
{
    /// <summary>
    /// Handles chat completion requests following OpenAI's API format.
    /// </summary>
    [ApiController]
    [Route("v1/chat")]
    [Authorize(AuthenticationSchemes = "VirtualKey")]
    [RequireBalance]
    [Tags("Chat")]
    public partial class ChatController : GatewayControllerBase
    {
        private readonly Conduit _conduit;
        private readonly ILogger<ChatController> _logger;
        private readonly ConduitLLM.Configuration.Interfaces.IModelProviderMappingService _modelMappingService;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly ConduitLLM.Core.Interfaces.IUsageEstimationService? _usageEstimationService;
        private readonly ConduitLLM.Functions.Interfaces.IFunctionConfigurationRepository? _functionConfigRepository;
        private readonly ConduitLLM.Configuration.Interfaces.IGlobalSettingsCacheService _globalSettingsCacheService;

        public ChatController(
            Conduit conduit,
            ILogger<ChatController> logger,
            ConduitLLM.Configuration.Interfaces.IModelProviderMappingService modelMappingService,
            JsonSerializerOptions jsonSerializerOptions,
            IEventBus eventBus,
            ConduitLLM.Configuration.Interfaces.IGlobalSettingsCacheService globalSettingsCacheService,
            ConduitLLM.Core.Interfaces.IUsageEstimationService? usageEstimationService = null,
            ConduitLLM.Functions.Interfaces.IFunctionConfigurationRepository? functionConfigRepository = null) : base(eventBus, logger)
        {
            _conduit = conduit ?? throw new ArgumentNullException(nameof(conduit));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modelMappingService = modelMappingService ?? throw new ArgumentNullException(nameof(modelMappingService));
            _jsonSerializerOptions = jsonSerializerOptions ?? throw new ArgumentNullException(nameof(jsonSerializerOptions));
            _globalSettingsCacheService = globalSettingsCacheService ?? throw new ArgumentNullException(nameof(globalSettingsCacheService));
            _usageEstimationService = usageEstimationService;
            _functionConfigRepository = functionConfigRepository;
        }

        /// <summary>
        /// Creates a chat completion.
        /// </summary>
        [HttpPost("completions")]
        [ProducesResponseType(typeof(ChatCompletionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateChatCompletion(
            [FromBody] ChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            using var activity = GatewayRequestMetrics.StartChatCompletionActivity(
                request.Model, request.Stream == true);
            var operationStopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Received /v1/chat/completions request for model: {Model}", LoggingSanitizer.S(request.Model));

            HttpContext.Items["IsStreamingRequest"] = request.Stream == true;

            await PopulateProviderMetadataAsync(request, activity);

            if (request.FunctionConfigurationIds != null && request.FunctionConfigurationIds.Count > 0)
            {
                var validationError = await ValidateFunctionCallRequestAsync(request, cancellationToken);
                if (validationError != null)
                    return validationError;
            }

            await ApplyAgenticDefaultsAsync(request);

            try
            {
                var virtualKeyId = CurrentVirtualKeyId;

                if (request.Stream != true)
                {
                    return await HandleNonStreamingRequestAsync(request, virtualKeyId, operationStopwatch, cancellationToken);
                }
                else
                {
                    await HandleStreamingRequestAsync(request, virtualKeyId, operationStopwatch, cancellationToken);
                    return new EmptyResult();
                }
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("error.type", ex.GetType().Name);
                _logger.LogError(ex, "Error processing request");
                GatewayOpsMetrics.RecordLlmOperation("chat_completion", request.Model, "error", operationStopwatch.Elapsed.TotalSeconds);
                return OpenAIError(500, ex.Message, "internal_error", "server_error");
            }
        }

        private async Task PopulateProviderMetadataAsync(ChatCompletionRequest request, Activity? activity)
        {
            try
            {
                var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(request.Model);
                if (modelMapping != null)
                {
                    HttpContext.Items["ProviderId"] = modelMapping.ProviderId;
                    HttpContext.Items["ProviderType"] = modelMapping.Provider?.ProviderType;
                    activity?.SetTag("gateway.provider_id", modelMapping.ProviderId);
                    activity?.SetTag("gateway.provider_type", modelMapping.Provider?.ProviderType.ToString());

                    if (modelMapping.ModelProviderTypeAssociation?.ModelCostId != null)
                    {
                        HttpContext.Items[ConduitLLM.Gateway.Constants.HttpContextKeys.ModelCostId] = modelMapping.ModelProviderTypeAssociation.ModelCostId;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get provider info for model {Model}", LoggingSanitizer.S(request.Model));
            }
        }

        private async Task ApplyAgenticDefaultsAsync(ChatCompletionRequest request)
        {
            if (!request.MaxAgenticIterations.HasValue)
            {
                request.MaxAgenticIterations = await _globalSettingsCacheService.GetMaxAgenticIterationsAsync();
            }
            if (!request.EnableAgenticMode.HasValue)
            {
                request.EnableAgenticMode = await _globalSettingsCacheService.GetDefaultAgenticModeEnabledAsync();
            }
        }

    }

    /// <summary>
    /// Helper class to capture function execution results for request logging.
    /// This data is stored in HttpContext.Items during streaming and used by
    /// the UsageTrackingMiddleware to populate request log metadata.
    /// </summary>
    public class FunctionExecutionResultForLogging
    {
        public string? ToolCallId { get; set; }
        public string? FunctionName { get; set; }
        public string? Status { get; set; }
        public decimal? Cost { get; set; }
        public string? ErrorMessage { get; set; }
        public Guid? FunctionExecutionId { get; set; }
    }
}
