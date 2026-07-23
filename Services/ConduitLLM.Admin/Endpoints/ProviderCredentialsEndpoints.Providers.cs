using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Configuration;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Messaging;

using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Core.Events;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Admin.Endpoints
{
    /// <summary>
    /// Controller for managing provider credentials
    /// </summary>
    public partial class ProviderCredentialsEndpoints : AdminEndpointHandlerBase
    {
        private readonly IProviderRepository _providerRepository;
        private readonly IProviderKeyCredentialRepository _keyRepository;
        private readonly ILLMClientFactory _clientFactory;

        /// <summary>
        /// Initializes the Provider Credentials endpoint handler.
        /// </summary>
        public ProviderCredentialsEndpoints(
            IProviderRepository providerRepository,
            IProviderKeyCredentialRepository keyRepository,
            ILLMClientFactory clientFactory,
            IEventBus eventBus,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ProviderCredentialsEndpoints> logger)
            : base(eventBus, httpContextAccessor, logger)
        {
            _providerRepository = providerRepository ?? throw new ArgumentNullException(nameof(providerRepository));
            _keyRepository = keyRepository ?? throw new ArgumentNullException(nameof(keyRepository));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        }

        public static IEndpointRouteBuilder MapProviderCredentialsEndpoints(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/ProviderCredentials")
                .RequireAuthorization("MasterKeyPolicy")
                .AddEndpointFilter<ValidationEndpointFilter>()
                .AddEndpointFilter<OperationLoggingEndpointFilter>()
                .WithTags("Provider Credentials");

            group.MapGet("/", ([FromServices] ProviderCredentialsEndpoints endpoints, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default) =>
                    endpoints.GetAllProviders(page, pageSize, cancellationToken))
                .WithName("ProviderCredentials_GetAll")
                .Produces<Configuration.DTOs.PagedResult<ProviderDto>>();
            group.MapGet("/{id:int}", ([FromServices] ProviderCredentialsEndpoints endpoints, int id) => endpoints.GetProviderById(id))
                .WithName("ProviderCredentials_GetById").Produces<ProviderDto>().Produces(StatusCodes.Status404NotFound);
            group.MapPost("/", ([FromServices] ProviderCredentialsEndpoints endpoints, CreateProviderRequest request) => endpoints.CreateProvider(request))
                .WithName("ProviderCredentials_Create").Produces<ProviderDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status400BadRequest);
            group.MapPut("/{id:int}", ([FromServices] ProviderCredentialsEndpoints endpoints, int id, UpdateProviderRequest request) => endpoints.UpdateProvider(id, request))
                .WithName("ProviderCredentials_Update").Produces<ProviderDto>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);
            group.MapDelete("/{id:int}", ([FromServices] ProviderCredentialsEndpoints endpoints, int id) => endpoints.DeleteProvider(id))
                .WithName("ProviderCredentials_Delete").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);
            group.MapGet("/{providerId:int}/keys", ([FromServices] ProviderCredentialsEndpoints endpoints, int providerId) => endpoints.GetProviderKeyCredentials(providerId))
                .WithName("ProviderCredentials_GetKeys").Produces<IEnumerable<ProviderKeyCredentialDto>>().Produces(StatusCodes.Status404NotFound);
            group.MapGet("/{providerId:int}/keys/{keyId:int}", ([FromServices] ProviderCredentialsEndpoints endpoints, int providerId, int keyId) => endpoints.GetProviderKeyCredential(providerId, keyId))
                .WithName("ProviderCredentials_GetKey").Produces<ProviderKeyCredentialDto>().Produces(StatusCodes.Status404NotFound);
            group.MapPost("/{providerId:int}/keys", ([FromServices] ProviderCredentialsEndpoints endpoints, int providerId, CreateKeyRequest request) => endpoints.CreateProviderKeyCredential(providerId, request))
                .WithName("ProviderCredentials_CreateKey").Produces<ProviderKeyCredentialDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);
            group.MapPut("/{providerId:int}/keys/{keyId:int}", ([FromServices] ProviderCredentialsEndpoints endpoints, int providerId, int keyId, UpdateKeyRequest request) => endpoints.UpdateProviderKeyCredential(providerId, keyId, request))
                .WithName("ProviderCredentials_UpdateKey").Produces<ProviderKeyCredentialDto>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);
            group.MapDelete("/{providerId:int}/keys/{keyId:int}", ([FromServices] ProviderCredentialsEndpoints endpoints, int providerId, int keyId) => endpoints.DeleteProviderKeyCredential(providerId, keyId))
                .WithName("ProviderCredentials_DeleteKey").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);
            group.MapPost("/{providerId:int}/keys/{keyId:int}/set-primary", ([FromServices] ProviderCredentialsEndpoints endpoints, int providerId, int keyId) => endpoints.SetPrimaryKey(providerId, keyId))
                .WithName("ProviderCredentials_SetPrimaryKey").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);
            group.MapPost("/{id:int}/test", ([FromServices] ProviderCredentialsEndpoints endpoints, int id) => endpoints.TestProviderConnection(id))
                .WithName("ProviderCredentials_Test").Produces<StandardApiKeyTestResponse>().Produces(StatusCodes.Status404NotFound);
            group.MapPost("/test/{id:int}", ([FromServices] ProviderCredentialsEndpoints endpoints, int id) => endpoints.TestProviderConnection(id))
                .ExcludeFromDescription();
            group.MapPost("/test", ([FromServices] ProviderCredentialsEndpoints endpoints, TestProviderRequest request) => endpoints.TestProviderConnectionWithCredentials(request))
                .WithName("ProviderCredentials_TestCredentials").Produces<StandardApiKeyTestResponse>().Produces(StatusCodes.Status400BadRequest);
            group.MapPost("/{providerId:int}/keys/{keyId:int}/test", ([FromServices] ProviderCredentialsEndpoints endpoints, int providerId, int keyId) => endpoints.TestProviderKeyCredential(providerId, keyId))
                .WithName("ProviderCredentials_TestKey").Produces<StandardApiKeyTestResponse>().Produces(StatusCodes.Status404NotFound);
            return app;
        }

        /// <summary>
        /// Gets all provider configurations with pagination
        /// </summary>
        /// <param name="page">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Paginated list of providers</returns>
        public async Task<IResult> GetAllProviders(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            // Validate and clamp page parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var (providers, totalCount) = await _providerRepository.GetPaginatedAsync(page, pageSize, cancellationToken);
            var items = providers.Select(ToProviderDto).ToList();

            var result = new Configuration.DTOs.PagedResult<ProviderDto>
            {
                Items = items,
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
        public async Task<IResult> GetProviderById(int id)
        {
            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider == null)
            {
                return AdminResults.NotFoundEntity("Provider", id);
            }

            return Ok(ToProviderDto(provider));
        }

        /// <summary>
        /// Creates a new provider
        /// </summary>
        /// <returns>The created provider</returns>
        public async Task<IResult> CreateProvider(CreateProviderRequest request)
        {
            if (!ProviderTypeCatalog.IsConfigurable(request.ProviderType))
            {
                return BadRequest(new ErrorResponseDto("Provider type must identify a configurable provider."));
            }

            var provider = new Provider
            {
                ProviderType = request.ProviderType,
                ProviderName = request.ProviderName,
                BaseUrl = request.BaseUrl,
                IsEnabled = request.IsEnabled,
                TrustProviderReportedCosts = request.TrustProviderReportedCosts,
                ProviderCostMarkupMultiplier = request.ProviderCostMarkupMultiplier,
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

            return Results.Created($"/api/ProviderCredentials/{provider.Id}", ToProviderDto(provider));
        }

        /// <summary>
        /// Updates a provider
        /// </summary>
        /// <param name="id">The ID of the provider to update</param>
        /// <param name="request">The update request containing new provider values</param>
        /// <returns>No content if successful</returns>
        public async Task<IResult> UpdateProvider(int id, UpdateProviderRequest request)
        {
            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider == null)
            {
                return AdminResults.NotFoundEntity("Provider", id);
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

            if (provider.TrustProviderReportedCosts != request.TrustProviderReportedCosts)
            {
                changes.Add(("TrustProviderReportedCosts", provider.TrustProviderReportedCosts.ToString(), request.TrustProviderReportedCosts.ToString()));
                provider.TrustProviderReportedCosts = request.TrustProviderReportedCosts;
            }

            if (provider.ProviderCostMarkupMultiplier != request.ProviderCostMarkupMultiplier)
            {
                changes.Add(("ProviderCostMarkupMultiplier", provider.ProviderCostMarkupMultiplier.ToString(), request.ProviderCostMarkupMultiplier.ToString()));
                provider.ProviderCostMarkupMultiplier = request.ProviderCostMarkupMultiplier;
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

            return Ok(ToProviderDto(provider));
        }

        private static ProviderDto ToProviderDto(Provider provider) => new()
        {
            Id = provider.Id,
            ProviderType = provider.ProviderType,
            ProviderName = provider.ProviderName,
            BaseUrl = provider.BaseUrl,
            IsEnabled = provider.IsEnabled,
            TrustProviderReportedCosts = provider.TrustProviderReportedCosts,
            ProviderCostMarkupMultiplier = provider.ProviderCostMarkupMultiplier,
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.UpdatedAt,
            KeyCount = provider.ProviderKeyCredentials?.Count ?? 0
        };

        /// <summary>
        /// Deletes a provider
        /// </summary>
        /// <param name="id">The ID of the provider to delete</param>
        /// <returns>No content if successful</returns>
        public async Task<IResult> DeleteProvider(int id)
        {
            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider == null)
            {
                return AdminResults.NotFoundEntity("Provider", id);
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
