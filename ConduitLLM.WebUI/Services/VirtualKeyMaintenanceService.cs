using ConduitLLM.Configuration.Constants;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;
using ConduitLLM.WebUI.Options;

using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Background service for virtual key maintenance tasks (budget reset, expiration, etc.)
/// </summary>
public class VirtualKeyMaintenanceService : BackgroundService
{
    private readonly ILogger<VirtualKeyMaintenanceService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval;

    public VirtualKeyMaintenanceService(
        ILogger<VirtualKeyMaintenanceService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        // Get check interval from configuration, default to 1 hour
        var intervalMinutes = configuration.GetValue<int>("VirtualKeyMaintenance:CheckIntervalMinutes", 60);
        _checkInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Virtual Key maintenance service is starting");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Running virtual key maintenance tasks");

                await ProcessVirtualKeyMaintenance(stoppingToken);

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping the service
            _logger.LogInformation("Virtual Key maintenance service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Virtual Key maintenance service");
            throw; // Re-throw to restart the service
        }
    }

    private async Task ProcessVirtualKeyMaintenance(CancellationToken stoppingToken)
    {
        try
        {
            // Create a new scope for operations
            using var scope = _serviceProvider.CreateScope();

            // Use IVirtualKeyService interface
            var virtualKeyService = scope.ServiceProvider.GetRequiredService<ConduitLLM.WebUI.Interfaces.IVirtualKeyService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

            _logger.LogInformation("Running virtual key maintenance using Admin API service");

            try
            {
                // Use the service adapter for all maintenance tasks
                await virtualKeyService.PerformMaintenanceAsync();
                _logger.LogInformation("Virtual key maintenance completed successfully via Admin API");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing virtual key maintenance via Admin API");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing virtual key maintenance tasks");
        }
    }

    // Methods removed - now handled through Admin API PerformMaintenanceAsync
}
