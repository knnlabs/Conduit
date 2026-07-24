using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using VirtualKeyUtilities = ConduitLLM.Configuration.Utilities.VirtualKeyUtilities;

using ConduitLLM.Configuration.Messaging;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing virtual keys through the Admin API.
    /// Inherits shared CRUD operations from <see cref="VirtualKeyServiceBase"/> and adds
    /// Admin-specific concerns: cache invalidation, media cleanup, discovery, validation, and usage.
    /// </summary>
    public partial class AdminVirtualKeyService : VirtualKeyServiceBase, IAdminVirtualKeyService
    {
        // Aliases for partial class files that reference the underscore-prefixed fields
        private IVirtualKeyRepository _virtualKeyRepository => VirtualKeyRepository;
        private IVirtualKeyGroupRepository _groupRepository => GroupRepository;
        private IVirtualKeySpendHistoryRepository _spendHistoryRepository => SpendHistoryRepository;

        // Admin-specific dependencies
        private readonly IVirtualKeyCache? _cache;
        private readonly IMediaLifecycleService? _mediaLifecycleService;
        private readonly IModelProviderMappingRepository _modelProviderMappingRepository;
        private readonly IModelCapabilityService _modelCapabilityService;
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly ILogger<AdminVirtualKeyService> _logger;

        /// <summary>
        /// Initializes a new instance of the AdminVirtualKeyService class
        /// </summary>
        /// <param name="virtualKeyRepository">The virtual key repository</param>
        /// <param name="spendHistoryRepository">The spend history repository</param>
        /// <param name="groupRepository">The virtual key group repository</param>
        /// <param name="logger">The logger</param>
        /// <param name="modelProviderMappingRepository">The model provider mapping repository</param>
        /// <param name="modelCapabilityService">The model capability service</param>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="cache">Optional Redis cache for immediate invalidation (null if not configured)</param>
        /// <param name="eventBus">Optional event bus (null if not configured)</param>
        /// <param name="mediaLifecycleService">Optional media lifecycle service for cleaning up associated media files (null if not configured)</param>
        public AdminVirtualKeyService(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            IVirtualKeyGroupRepository groupRepository,
            ILogger<AdminVirtualKeyService> logger,
            IModelProviderMappingRepository modelProviderMappingRepository,
            IModelCapabilityService modelCapabilityService,
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            IVirtualKeyCache? cache = null,
            IEventBus? eventBus = null,
            IMediaLifecycleService? mediaLifecycleService = null)
            : base(virtualKeyRepository, groupRepository, spendHistoryRepository, eventBus, logger)
        {
            _cache = cache;
            _mediaLifecycleService = mediaLifecycleService;
            _modelProviderMappingRepository = modelProviderMappingRepository ?? throw new ArgumentNullException(nameof(modelProviderMappingRepository));
            _modelCapabilityService = modelCapabilityService ?? throw new ArgumentNullException(nameof(modelCapabilityService));
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Virtual Hook Overrides

        /// <summary>
        /// Cleans up associated media files before a virtual key is deleted.
        /// </summary>
        protected override async Task OnBeforeVirtualKeyDeleteAsync(int keyId)
        {
            if (_mediaLifecycleService != null)
            {
                try
                {
                    var result = await _mediaLifecycleService.DeleteMediaForVirtualKeyAsync(keyId);
                    if (result.FailedCount > 0)
                    {
                        throw new InvalidOperationException(
                            $"Virtual key deletion aborted: {result.FailedCount} media files could not be deleted from storage. " +
                            $"{result.DeletedCount} media files were deleted successfully; retry the operation.");
                    }

                    if (result.DeletedCount > 0)
                    {
                        Logger.LogInformation(
                            "Deleted {DeletedCount} media files for virtual key {KeyId}",
                            result.DeletedCount, keyId);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(
                        ex,
                        "Failed to delete media files for virtual key {KeyId}; key deletion is blocked to preserve media tracking",
                        keyId);
                    throw;
                }
            }
            else
            {
                Logger.LogWarning("Media lifecycle service not available, media files for virtual key {KeyId} will become orphaned", keyId);
            }
        }

        /// <summary>
        /// Invalidates the cache entry for an updated virtual key.
        /// </summary>
        protected override async Task OnVirtualKeyUpdatedAsync(VirtualKey key, string[] changedProperties)
        {
            if (_cache != null)
                await _cache.InvalidateVirtualKeyAsync(key.KeyHash);
        }

        /// <summary>
        /// Invalidates the cache entry for a deleted virtual key.
        /// </summary>
        protected override async Task OnVirtualKeyDeletedAsync(VirtualKey key)
        {
            if (_cache != null)
                await _cache.InvalidateVirtualKeyAsync(key.KeyHash);
        }

        #endregion

        /// <inheritdoc />
        public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync(int? virtualKeyGroupId = null)
        {
            if (virtualKeyGroupId.HasValue)
            {
                _logger.LogDebug("Listing virtual keys for group {GroupId}", virtualKeyGroupId.Value);
                var keysByGroup = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                    VirtualKeyRepository.GetByVirtualKeyGroupIdPaginatedAsync, virtualKeyGroupId.Value);
                return keysByGroup.ConvertAll(VirtualKeyUtilities.MapToDto);
            }
            else
            {
                _logger.LogDebug("Listing all virtual keys");
                var keys = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                    VirtualKeyRepository.GetPaginatedAsync);
                return keys.ConvertAll(VirtualKeyUtilities.MapToDto);
            }
        }


    }
}
