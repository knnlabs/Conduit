using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Http.Authorization;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Handles image generation requests following OpenAI's API format.
    /// </summary>
    [ApiController]
    [Route("v1/images")]
    [Authorize(AuthenticationSchemes = "VirtualKey,EphemeralKey")]
    [RequireBalance]
    [Tags("Images")]
    public partial class ImagesController : EventPublishingControllerBase
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IMediaStorageService _storageService;
        private readonly ILogger<ImagesController> _logger;
        private readonly IModelProviderMappingService _modelMappingService;
        private readonly IAsyncTaskService _taskService;
        private readonly ConduitLLM.Core.Interfaces.IVirtualKeyService _virtualKeyService;
        private readonly IMediaLifecycleService _mediaLifecycleService;
        private readonly IHttpClientFactory _httpClientFactory;

        public ImagesController(
            ILLMClientFactory clientFactory,
            IMediaStorageService storageService,
            ILogger<ImagesController> logger,
            IModelProviderMappingService modelMappingService,
            IAsyncTaskService taskService,
            IPublishEndpoint publishEndpoint,
            ConduitLLM.Core.Interfaces.IVirtualKeyService virtualKeyService,
            IMediaLifecycleService mediaLifecycleService,
            IHttpClientFactory httpClientFactory)
            : base(publishEndpoint, logger)
        {
            _clientFactory = clientFactory;
            _storageService = storageService;
            _logger = logger;
            _modelMappingService = modelMappingService;
            _taskService = taskService;
            _virtualKeyService = virtualKeyService;
            _mediaLifecycleService = mediaLifecycleService;
            _httpClientFactory = httpClientFactory;
        }
    }
}
