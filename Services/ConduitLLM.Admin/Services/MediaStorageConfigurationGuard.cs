using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Blocks cleanup when the Admin API resolves in-memory storage while media records exist.
/// </summary>
public sealed class MediaStorageConfigurationGuard : IHostedService, IMediaStorageConfigurationGuard
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMediaStorageService _storageService;
    private readonly ILogger<MediaStorageConfigurationGuard> _logger;

    /// <inheritdoc />
    public string StorageBackend => GetBackendName(_storageService);

    /// <inheritdoc />
    public bool IsCleanupAllowed { get; private set; } = true;

    public MediaStorageConfigurationGuard(
        IServiceScopeFactory scopeFactory,
        IMediaStorageService storageService,
        ILogger<MediaStorageConfigurationGuard> logger)
    {
        _scopeFactory = scopeFactory;
        _storageService = storageService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) =>
        ValidateAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task<bool> ValidateAsync(CancellationToken cancellationToken = default)
    {
        if (_storageService is not InMemoryMediaStorageService)
        {
            IsCleanupAllowed = true;
            return true;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IConfigurationDbContext>();
            var hasPersistedMedia = await context.MediaRecords
                .AsNoTracking()
                .AnyAsync(cancellationToken);

            IsCleanupAllowed = !hasPersistedMedia;
            if (hasPersistedMedia)
            {
                _logger.LogCritical(
                    "MEDIA CLEANUP BLOCKED: Admin resolved {StorageBackend} while MediaRecords contains data. " +
                    "Set CONDUIT_MEDIA_STORAGE_TYPE=S3 and configure CONDUIT_S3_ENDPOINT before enabling cleanup.",
                    StorageBackend);
            }
            else
            {
                _logger.LogWarning(
                    "Admin resolved {StorageBackend}; cleanup is permitted only while MediaRecords is empty.",
                    StorageBackend);
            }
        }
        catch (Exception ex)
        {
            IsCleanupAllowed = false;
            _logger.LogCritical(
                ex,
                "MEDIA CLEANUP BLOCKED: unable to verify whether persisted media exists while using {StorageBackend}.",
                StorageBackend);
        }

        return IsCleanupAllowed;
    }

    internal static string GetBackendName(IMediaStorageService? storageService) =>
        storageService switch
        {
            S3MediaStorageService => "S3",
            InMemoryMediaStorageService => "InMemory",
            null => "Unavailable",
            _ => storageService.GetType().Name
        };
}
