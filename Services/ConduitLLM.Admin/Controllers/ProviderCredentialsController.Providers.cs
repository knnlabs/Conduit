using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using MassTransit;

using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Core.Events;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing provider credentials
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public partial class ProviderCredentialsController : AdminControllerBase
    {
        private readonly IProviderRepository _providerRepository;
        private readonly IProviderKeyCredentialRepository _keyRepository;
        private readonly ILLMClientFactory _clientFactory;

        /// <summary>
        /// Initializes a new instance of the ProviderCredentialsController
        /// </summary>
        public ProviderCredentialsController(
            IProviderRepository providerRepository,
            IProviderKeyCredentialRepository keyRepository,
            ILLMClientFactory clientFactory,
            IPublishEndpoint publishEndpoint,
            ILogger<ProviderCredentialsController> logger)
            : base(publishEndpoint, logger)
        {
            _providerRepository = providerRepository ?? throw new ArgumentNullException(nameof(providerRepository));
            _keyRepository = keyRepository ?? throw new ArgumentNullException(nameof(keyRepository));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        }

        /// <summary>
        /// Gets all provider configurations with pagination
        /// </summary>
        /// <param name="page">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Paginated list of providers</returns>
        [HttpGet]
        [ProducesResponseType(typeof(Configuration.DTOs.PagedResult<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllProviders(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            // Validate and clamp page parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var (providers, totalCount) = await _providerRepository.GetPaginatedAsync(page, pageSize, cancellationToken);
            var items = providers.Select(p => new
            {
                p.Id,
                p.ProviderType,
                p.ProviderName,
                p.BaseUrl,
                p.IsEnabled,
                p.CreatedAt,
                p.UpdatedAt,
                KeyCount = p.ProviderKeyCredentials?.Count ?? 0
            }).ToList();

            var result = new Configuration.DTOs.PagedResult<object>
            {
                Items = items.Cast<object>().ToList(),
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return Ok(result);
        }

        /// <summary>
        /// Gets a provider by ID
        /// </summary>
        /// <param name="id">The ID of the provider</param>
        /// <returns>The provider</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProviderById(int id)
        {
            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider == null)
            {
                return this.NotFoundEntity("Provider", id);
            }

            return Ok(new
            {
                provider.Id,
                provider.ProviderType,
                provider.ProviderName,
                provider.BaseUrl,
                provider.IsEnabled,
                provider.CreatedAt,
                provider.UpdatedAt,
                KeyCount = provider.ProviderKeyCredentials?.Count ?? 0
            });
        }

        /// <summary>
        /// Creates a new provider
        /// </summary>
        /// <returns>The created provider</returns>
        [HttpPost]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateProvider([FromBody] CreateProviderRequest request)
        {
            var provider = new Provider
            {
                ProviderType = request.ProviderType,
                ProviderName = request.ProviderName,
                BaseUrl = request.BaseUrl,
                IsEnabled = request.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var id = await _providerRepository.CreateAsync(provider);
            provider.Id = id;

            // Publish provider created event
            PublishEventFireAndForget(new ProviderCreated
            {
                ProviderId = id,
                ProviderType = provider.ProviderType.ToString(),
                ProviderName = provider.ProviderName,
                BaseUrl = provider.BaseUrl,
                IsEnabled = provider.IsEnabled,
                CreatedAt = provider.CreatedAt,
                CorrelationId = Guid.NewGuid().ToString()
            }, "create provider");

            LogAdminAudit("Created", "Provider", id, $"Type: {provider.ProviderType}, Name: {LoggingSanitizer.S(provider.ProviderName)}");
            AdminOperationsMetricsService.RecordProviderOperation("create", provider.ProviderType.ToString(), "success");
            AdminOperationsMetricsService.RecordConfigurationChange("provider", "create");

            return CreatedAtAction(nameof(GetProviderById), new { id = provider.Id }, new
            {
                provider.Id,
                provider.ProviderType,
                provider.ProviderName,
                provider.BaseUrl,
                provider.IsEnabled,
                provider.CreatedAt,
                provider.UpdatedAt,
                KeyCount = 0
            });
        }

        /// <summary>
        /// Updates a provider
        /// </summary>
        /// <param name="id">The ID of the provider to update</param>
        /// <param name="request">The update request containing new provider values</param>
        /// <returns>No content if successful</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateProvider(int id, [FromBody] UpdateProviderRequest request)
        {
            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider == null)
            {
                return this.NotFoundEntity("Provider", id);
            }

            var changes = new List<(string Property, string? OldValue, string? NewValue)>();

            if (!string.IsNullOrEmpty(request.ProviderName) && provider.ProviderName != request.ProviderName)
            {
                changes.Add(("ProviderName", provider.ProviderName, request.ProviderName));
                provider.ProviderName = request.ProviderName;
            }

            if (provider.BaseUrl != request.BaseUrl)
            {
                changes.Add(("BaseUrl", provider.BaseUrl, request.BaseUrl));
                provider.BaseUrl = request.BaseUrl;
            }

            if (provider.IsEnabled != request.IsEnabled)
            {
                changes.Add(("IsEnabled", provider.IsEnabled.ToString(), request.IsEnabled.ToString()));
                provider.IsEnabled = request.IsEnabled;
            }

            provider.UpdatedAt = DateTime.UtcNow;

            await _providerRepository.UpdateAsync(provider);

            // Publish provider updated event
            if (changes.Count > 0)
            {
                var changedProperties = changes.Select(c => c.Property).ToArray();
                PublishEventFireAndForget(new ProviderUpdated
                {
                    ProviderId = id,
                    IsEnabled = provider.IsEnabled,
                    ChangedProperties = changedProperties,
                    CorrelationId = Guid.NewGuid().ToString()
                }, "update provider", new { ProviderId = id, ChangedProperties = string.Join(", ", changedProperties) });

                LogAdminAuditWithChanges("Provider", id, changes);
            }

            AdminOperationsMetricsService.RecordProviderOperation("update", provider.ProviderType.ToString(), "success");
            AdminOperationsMetricsService.RecordConfigurationChange("provider", "update");

            return NoContent();
        }

        /// <summary>
        /// Deletes a provider
        /// </summary>
        /// <param name="id">The ID of the provider to delete</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteProvider(int id)
        {
            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider == null)
            {
                return this.NotFoundEntity("Provider", id);
            }

            await _providerRepository.DeleteAsync(id);

            // Publish provider deleted event
            PublishEventFireAndForget(new ProviderDeleted
            {
                ProviderId = id,
                CorrelationId = Guid.NewGuid().ToString()
            }, "delete provider", new { ProviderId = id });

            LogAdminAudit("Deleted", "Provider", id, $"Name: {LoggingSanitizer.S(provider.ProviderName)}");
            AdminOperationsMetricsService.RecordProviderOperation("delete", provider.ProviderType.ToString(), "success");
            AdminOperationsMetricsService.RecordConfigurationChange("provider", "delete");

            return NoContent();
        }
    }
}
