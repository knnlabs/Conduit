using ConduitLLM.Configuration.Constants;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.WebUI.Data;
using ConduitLLM.WebUI.Models;

using Microsoft.EntityFrameworkCore;

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
            // Create a new scope for database operations
            using var scope = _serviceProvider.CreateScope();
            // Resolve the CORRECT DbContext
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.ConfigurationDbContext>(); 
            
            await ResetExpiredBudgets(dbContext, scope.ServiceProvider, stoppingToken);
            await HandleExpiredKeys(dbContext, scope.ServiceProvider, stoppingToken);
            await CheckKeysApproachingBudgetLimits(dbContext, scope.ServiceProvider, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing virtual key maintenance tasks");
        }
    }

    // Update DbContext type in parameter
    private async Task ResetExpiredBudgets(ConduitLLM.Configuration.ConfigurationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken stoppingToken) 
    {
        var utcNow = DateTime.UtcNow;

        // Fetch potential monthly keys first
        var potentialMonthlyKeys = await dbContext.VirtualKeys
            .Where(k => k.BudgetDuration == VirtualKeyConstants.BudgetPeriods.Monthly && 
                        k.CurrentSpend > 0 &&
                        k.BudgetStartDate.HasValue) // Simpler filter for DB
            .ToListAsync(stoppingToken);

        // Filter in memory using the complex date logic
        var monthlyKeys = potentialMonthlyKeys
            .Where(k => (utcNow - k.BudgetStartDate!.Value).TotalDays >= 30) // Non-null asserted due to previous filter
            .ToList();

        // Fetch potential daily keys first
        var potentialDailyKeys = await dbContext.VirtualKeys
            .Where(k => k.BudgetDuration == VirtualKeyConstants.BudgetPeriods.Daily && 
                        k.CurrentSpend > 0 &&
                        k.BudgetStartDate.HasValue) // Simpler filter for DB
            .ToListAsync(stoppingToken);

        // Filter in memory using the complex date logic
        var dailyKeys = potentialDailyKeys
            .Where(k => (utcNow - k.BudgetStartDate!.Value).TotalDays >= 1) // Non-null asserted due to previous filter
            .ToList();

        int resetCount = 0;
        
        var notificationService = serviceProvider.GetRequiredService<NotificationService>();
        
        // Reset monthly budgets
        foreach (var key in monthlyKeys)
        {
            _logger.LogInformation("Resetting monthly budget for key: {KeyName} (ID: {KeyId})", key.KeyName, key.Id);
            key.CurrentSpend = 0;
            key.BudgetStartDate = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            key.UpdatedAt = utcNow;
            
            // Send notification
            notificationService.AddKeyBudgetReset(key.KeyName, key.Id, VirtualKeyConstants.BudgetPeriods.Monthly);
            
            resetCount++;
        }
        
        // Reset daily budgets
        foreach (var key in dailyKeys)
        {
            _logger.LogInformation("Resetting daily budget for key: {KeyName} (ID: {KeyId})", key.KeyName, key.Id);
            key.CurrentSpend = 0;
            key.BudgetStartDate = utcNow.Date;
            key.UpdatedAt = utcNow;
            
            // Send notification
            notificationService.AddKeyBudgetReset(key.KeyName, key.Id, VirtualKeyConstants.BudgetPeriods.Daily);
            
            resetCount++;
        }

        if (resetCount > 0)
        {
            _logger.LogInformation("Reset budgets for {Count} virtual keys", resetCount);
            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }

    // Update DbContext type in parameter
    private async Task HandleExpiredKeys(ConduitLLM.Configuration.ConfigurationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken stoppingToken) 
    {
        var utcNow = DateTime.UtcNow;
        
        // Find keys that have expired but are still enabled
        var expiredKeys = await dbContext.VirtualKeys
            .Where(k => k.ExpiresAt.HasValue && 
                        k.ExpiresAt.Value < utcNow &&
                        k.IsEnabled)
            .ToListAsync(stoppingToken);

        if (expiredKeys.Count > 0)
        {
            var notificationService = serviceProvider.GetRequiredService<NotificationService>();
            
            foreach (var key in expiredKeys)
            {
                _logger.LogInformation("Disabling expired key: {KeyName} (ID: {KeyId}), expired at {ExpiryDate}",
                    key.KeyName, key.Id, key.ExpiresAt);
                key.IsEnabled = false;
                key.UpdatedAt = utcNow;
                
                // Send notification
                notificationService.AddKeyExpired(key.KeyName, key.Id, key.ExpiresAt!.Value);
            }

            _logger.LogInformation("Disabled {Count} expired virtual keys", expiredKeys.Count);
            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }

    // Update DbContext type in parameter
    private async Task CheckKeysApproachingBudgetLimits(ConduitLLM.Configuration.ConfigurationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken stoppingToken) 
    {
        // Find keys that are approaching their budget limits (e.g., 80% used)
        var keysApproachingLimits = await dbContext.VirtualKeys
            .Where(k => k.MaxBudget.HasValue && 
                        k.IsEnabled &&
                        k.CurrentSpend > 0 &&
                        (k.CurrentSpend / k.MaxBudget.Value) >= VirtualKeyConstants.BudgetWarningThresholds.Low / 100m)
            .ToListAsync(stoppingToken);

        if (keysApproachingLimits.Count > 0)
        {
            var notificationService = serviceProvider.GetRequiredService<NotificationService>();
            
            foreach (var key in keysApproachingLimits)
            {
                var percentUsed = (key.CurrentSpend / key.MaxBudget!.Value) * 100;
                _logger.LogWarning("Virtual key approaching budget limit: {KeyName} (ID: {KeyId}), {PercentUsed:F1}% used ({CurrentSpend:F2} of {MaxBudget:F2})",
                    key.KeyName, key.Id, percentUsed, key.CurrentSpend, key.MaxBudget);
                
                // Send notification for keys that are at 80%, 90%, or 95% of their budget
                decimal lowThreshold = VirtualKeyConstants.BudgetWarningThresholds.Low;
                decimal mediumThreshold = VirtualKeyConstants.BudgetWarningThresholds.Medium;
                decimal highThreshold = VirtualKeyConstants.BudgetWarningThresholds.High;
                
                if (percentUsed >= highThreshold)
                {
                    notificationService.AddKeyApproachingBudget(key.KeyName, key.Id, key.CurrentSpend, key.MaxBudget.Value, (int)highThreshold);
                }
                else if (percentUsed >= mediumThreshold)
                {
                    notificationService.AddKeyApproachingBudget(key.KeyName, key.Id, key.CurrentSpend, key.MaxBudget.Value, (int)mediumThreshold);
                }
                else if (percentUsed >= lowThreshold)
                {
                    notificationService.AddKeyApproachingBudget(key.KeyName, key.Id, key.CurrentSpend, key.MaxBudget.Value, (int)lowThreshold);
                }
            }
        }
    }
}
