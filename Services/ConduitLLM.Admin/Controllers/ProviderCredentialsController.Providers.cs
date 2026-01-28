using ConduitLLM.Admin.Extensions;
using ConduitLLM.Configuration.Entities;
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
        public Task<IActionResult> GetAllProviders(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            // Validate and clamp page parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            return ExecuteAsync(
                async () =>
                {
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

                    return new Configuration.DTOs.PagedResult<object>
                    {
                        Items = items.Cast<object>().ToList(),
                        TotalCount = totalCount,
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                    };
                },
                result => Ok(result),
                "GetAllProviders");
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
        public Task<IActionResult> GetProviderById(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _providerRepository.GetByIdAsync(id),
                provider => Ok(new
                {
                    provider.Id,
                    provider.ProviderType,
                    provider.ProviderName,
                    provider.BaseUrl,
                    provider.IsEnabled,
                    provider.CreatedAt,
                    provider.UpdatedAt,
                    KeyCount = provider.ProviderKeyCredentials?.Count ?? 0
                }),
                "Provider",
                id,
                "GetProviderById");
        }

        /// <summary>
        /// Creates a new provider
        /// </summary>
        /// <returns>The created provider</returns>
        [HttpPost]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> CreateProvider([FromBody] CreateProviderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult<IActionResult>(BadRequest(ModelState));
            }

            return ExecuteAsync(
                async () =>
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

                    return provider;
                },
                provider => CreatedAtAction(nameof(GetProviderById), new { id = provider.Id }, new
                {
                    provider.Id,
                    provider.ProviderType,
                    provider.ProviderName,
                    provider.BaseUrl,
                    provider.IsEnabled,
                    provider.CreatedAt,
                    provider.UpdatedAt,
                    KeyCount = 0
                }),
                "CreateProvider");
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
        public Task<IActionResult> UpdateProvider(int id, [FromBody] UpdateProviderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult<IActionResult>(BadRequest(ModelState));
            }

            return ExecuteWithNotFoundAsync(
                () => _providerRepository.GetByIdAsync(id),
                async provider =>
                {
                    var changedProperties = new List<string>();

                    if (!string.IsNullOrEmpty(request.ProviderName) && provider.ProviderName != request.ProviderName)
                    {
                        provider.ProviderName = request.ProviderName;
                        changedProperties.Add("ProviderName");
                    }

                    if (provider.BaseUrl != request.BaseUrl)
                    {
                        provider.BaseUrl = request.BaseUrl;
                        changedProperties.Add("BaseUrl");
                    }

                    if (provider.IsEnabled != request.IsEnabled)
                    {
                        provider.IsEnabled = request.IsEnabled;
                        changedProperties.Add("IsEnabled");
                    }

                    provider.UpdatedAt = DateTime.UtcNow;

                    await _providerRepository.UpdateAsync(provider);

                    // Publish provider updated event
                    if (changedProperties.Count > 0)
                    {
                        PublishEventFireAndForget(new ProviderUpdated
                        {
                            ProviderId = id,
                            IsEnabled = provider.IsEnabled,
                            ChangedProperties = changedProperties.ToArray(),
                            CorrelationId = Guid.NewGuid().ToString()
                        }, "update provider", new { ProviderId = id, ChangedProperties = string.Join(", ", changedProperties) });
                    }

                    return NoContent();
                },
                "Provider",
                id,
                "UpdateProvider");
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
        public Task<IActionResult> DeleteProvider(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _providerRepository.GetByIdAsync(id),
                async provider =>
                {
                    await _providerRepository.DeleteAsync(id);

                    // Publish provider deleted event
                    PublishEventFireAndForget(new ProviderDeleted
                    {
                        ProviderId = id,
                        CorrelationId = Guid.NewGuid().ToString()
                    }, "delete provider", new { ProviderId = id });

                    return NoContent();
                },
                "Provider",
                id,
                "DeleteProvider");
        }
    }
}
