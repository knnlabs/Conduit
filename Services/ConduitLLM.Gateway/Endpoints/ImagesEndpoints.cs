using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;

namespace ConduitLLM.Gateway.Endpoints
{
    /// <summary>
    /// Handles image generation requests following OpenAI's API format.
    /// </summary>
    public partial class ImagesEndpoints : GatewayEndpointHandlerBase
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IMediaStorageService _storageService;
        private readonly ILogger<ImagesEndpoints> _logger;
        private readonly IModelProviderMappingService _modelMappingService;
        private readonly IAsyncTaskService _taskService;
        private readonly ConduitLLM.Core.Interfaces.IVirtualKeyService _virtualKeyService;
        private readonly IMediaLifecycleService _mediaLifecycleService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IProviderErrorTrackingService _errorTrackingService;

        public ImagesEndpoints(
            ILLMClientFactory clientFactory,
            IMediaStorageService storageService,
            ILogger<ImagesEndpoints> logger,
            IModelProviderMappingService modelMappingService,
            IAsyncTaskService taskService,
            IEventBus eventBus,
            ConduitLLM.Core.Interfaces.IVirtualKeyService virtualKeyService,
            IMediaLifecycleService mediaLifecycleService,
            IHttpClientFactory httpClientFactory,
            IProviderErrorTrackingService errorTrackingService,
            IHttpContextAccessor httpContextAccessor)
            : base(eventBus, httpContextAccessor, logger)
        {
            _clientFactory = clientFactory;
            _storageService = storageService;
            _logger = logger;
            _modelMappingService = modelMappingService;
            _taskService = taskService;
            _virtualKeyService = virtualKeyService;
            _mediaLifecycleService = mediaLifecycleService;
            _httpClientFactory = httpClientFactory;
            _errorTrackingService = errorTrackingService;
        }
    }
}
