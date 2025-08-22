using ConduitLLM.Core.Interfaces;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Hybrid implementation of async task management service that uses both database and cache.
    /// </summary>
    public partial class HybridAsyncTaskService : IAsyncTaskService
    {
        private readonly IAsyncTaskRepository _repository;
        private readonly IDistributedCache _cache;
        private readonly IPublishEndpoint? _publishEndpoint;
        private readonly ILogger<HybridAsyncTaskService> _logger;
        private const string TASK_KEY_PREFIX = "async:task:";
        private const int CACHE_EXPIRY_HOURS = 2; // Shorter expiry for completed tasks

        /// <summary>
        /// Initializes a new instance of the <see cref="HybridAsyncTaskService"/> class.
        /// </summary>
        /// <param name="repository">The async task repository.</param>
        /// <param name="cache">The distributed cache service.</param>
        /// <param name="logger">The logger instance.</param>
        public HybridAsyncTaskService(
            IAsyncTaskRepository repository,
            IDistributedCache cache,
            ILogger<HybridAsyncTaskService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HybridAsyncTaskService"/> class with event publishing.
        /// </summary>
        /// <param name="repository">The async task repository.</param>
        /// <param name="cache">The distributed cache service.</param>
        /// <param name="publishEndpoint">The event publish endpoint (optional, can be null).</param>
        /// <param name="logger">The logger instance.</param>
        public HybridAsyncTaskService(
            IAsyncTaskRepository repository,
            IDistributedCache cache,
            IPublishEndpoint? publishEndpoint,
            ILogger<HybridAsyncTaskService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _publishEndpoint = publishEndpoint; // Allow null
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }














    }
}