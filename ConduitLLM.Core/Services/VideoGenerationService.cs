using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Events;
using MassTransit;

using ConduitLLM.Configuration.Interfaces;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Base implementation of the video generation service.
    /// Coordinates video generation across different providers and handles orchestration.
    /// </summary>
    public partial class VideoGenerationService : EventPublishingServiceBase, IVideoGenerationService
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IModelCapabilityService _capabilityService;
        private readonly ICostCalculationService _costService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly IMediaStorageService _mediaStorage;
        private readonly IAsyncTaskService _taskService;
        private readonly ICancellableTaskRegistry? _taskRegistry;
        private readonly ILogger<VideoGenerationService> _logger;
        private readonly IModelProviderMappingService _modelMappingService;

        public VideoGenerationService(
            ILLMClientFactory clientFactory,
            IModelCapabilityService capabilityService,
            ICostCalculationService costService,
            IVirtualKeyService virtualKeyService,
            IMediaStorageService mediaStorage,
            IAsyncTaskService taskService,
            ILogger<VideoGenerationService> logger,
            IModelProviderMappingService modelMappingService,
            IPublishEndpoint? publishEndpoint = null,
            ICancellableTaskRegistry? taskRegistry = null)
            : base(publishEndpoint, logger)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _capabilityService = capabilityService ?? throw new ArgumentNullException(nameof(capabilityService));
            _costService = costService ?? throw new ArgumentNullException(nameof(costService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _mediaStorage = mediaStorage ?? throw new ArgumentNullException(nameof(mediaStorage));
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _taskRegistry = taskRegistry;
            _modelMappingService = modelMappingService ?? throw new ArgumentNullException(nameof(modelMappingService));
            
            // Log event publishing configuration
            LogEventPublishingConfiguration(nameof(VideoGenerationService));
        }
    }
}