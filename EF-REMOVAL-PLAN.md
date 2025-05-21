# Entity Framework Removal Plan for WebUI Project

## Overview

The Conduit WebUI project is transitioning from direct database access using Entity Framework Core to using the Admin API exclusively. This document outlines the steps required to fully remove Entity Framework dependencies from the WebUI project.

## Current Status

- Many services have already been migrated to use Admin API adapters
- VirtualKeyMaintenanceService has been updated to only use Admin API
- ProviderHealthMonitorService has been updated to only use Admin API
- Program.cs has been modified to register service adapters
- Several services still reference Entity Framework directly

## Required Changes

### 1. Interface Definitions

Create the following interfaces in `/ConduitLLM.WebUI/Interfaces/`:

```csharp
public interface IHttpRetryConfigurationService
{
    Task UpdateRetryConfigurationAsync(RetryOptions retryOptions);
    Task LoadSettingsFromDatabaseAsync();
}

public interface IHttpTimeoutConfigurationService
{
    Task UpdateTimeoutConfigurationAsync(TimeoutOptions timeoutOptions);
    Task LoadSettingsFromDatabaseAsync();
}

public interface IProviderStatusService
{
    Task<Dictionary<string, ProviderStatus>> CheckAllProvidersStatusAsync();
    Task<ProviderStatus> CheckProviderStatusAsync(ProviderCredential provider);
    Task<ProviderStatus> CheckProviderStatusAsync(string providerName);
}
```

### 2. Admin API Client Extension

Add methods to the IAdminApiClient interface in `/ConduitLLM.WebUI/Interfaces/IAdminApiClient.cs`:

```csharp
// Provider Status Methods
Task<Dictionary<string, ProviderStatus>> CheckAllProvidersStatusAsync();
Task<ProviderStatus> CheckProviderStatusAsync(string providerName);

// HTTP Configuration Methods
Task<string> GetSettingAsync(string key);
Task SetSettingAsync(string key, string value);
Task<bool> InitializeHttpTimeoutConfigurationAsync();
Task<bool> InitializeHttpRetryConfigurationAsync();
```

### 3. Admin API Client Implementation

Implement the new methods in the AdminApiClient class:

```csharp
// Provider Status Methods
public async Task<Dictionary<string, ProviderStatus>> CheckAllProvidersStatusAsync()
{
    var response = await _httpClient.GetAsync($"/api/providerhealth/status/all");
    response.EnsureSuccessStatusCode();
    var content = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<Dictionary<string, ProviderStatus>>(content, _jsonOptions);
}

public async Task<ProviderStatus> CheckProviderStatusAsync(string providerName)
{
    var response = await _httpClient.GetAsync($"/api/providerhealth/status/{providerName}");
    response.EnsureSuccessStatusCode();
    var content = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<ProviderStatus>(content, _jsonOptions);
}

// HTTP Configuration Methods
public async Task<string> GetSettingAsync(string key)
{
    var response = await _httpClient.GetAsync($"/api/globalsettings/{Uri.EscapeDataString(key)}");
    response.EnsureSuccessStatusCode();
    var content = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<string>(content, _jsonOptions);
}

public async Task SetSettingAsync(string key, string value)
{
    var response = await _httpClient.PostAsJsonAsync($"/api/globalsettings", new { Key = key, Value = value });
    response.EnsureSuccessStatusCode();
}

public async Task<bool> InitializeHttpTimeoutConfigurationAsync()
{
    var response = await _httpClient.PostAsync($"/api/globalsettings/initialize/timeout", null);
    response.EnsureSuccessStatusCode();
    var content = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<bool>(content, _jsonOptions);
}

public async Task<bool> InitializeHttpRetryConfigurationAsync()
{
    var response = await _httpClient.PostAsync($"/api/globalsettings/initialize/retry", null);
    response.EnsureSuccessStatusCode();
    var content = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<bool>(content, _jsonOptions);
}
```

### 4. Service Adapter Implementations

#### A. HttpRetryConfigurationServiceAdapter

Create a new file `/ConduitLLM.WebUI/Services/Adapters/HttpRetryConfigurationServiceAdapter.cs`:

```csharp
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.Providers.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services.Adapters
{
    public class HttpRetryConfigurationServiceAdapter : IHttpRetryConfigurationService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly IOptionsMonitor<RetryOptions> _options;
        private readonly ILogger<HttpRetryConfigurationServiceAdapter> _logger;

        public HttpRetryConfigurationServiceAdapter(
            IAdminApiClient adminApiClient,
            IOptionsMonitor<RetryOptions> options,
            ILogger<HttpRetryConfigurationServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient;
            _options = options;
            _logger = logger;
        }

        public async Task UpdateRetryConfigurationAsync(RetryOptions retryOptions)
        {
            try
            {
                await _adminApiClient.SetSettingAsync("HttpRetry:MaxRetries", retryOptions.MaxRetries.ToString());
                await _adminApiClient.SetSettingAsync("HttpRetry:InitialDelaySeconds", retryOptions.InitialDelaySeconds.ToString());
                await _adminApiClient.SetSettingAsync("HttpRetry:MaxDelaySeconds", retryOptions.MaxDelaySeconds.ToString());
                await _adminApiClient.SetSettingAsync("HttpRetry:EnableRetryLogging", retryOptions.EnableRetryLogging.ToString());
                
                _logger.LogInformation("HTTP retry configuration updated via Admin API: MaxRetries={MaxRetries}, InitialDelay={InitialDelay}s, MaxDelay={MaxDelay}s, Logging={Logging}",
                    retryOptions.MaxRetries, retryOptions.InitialDelaySeconds, retryOptions.MaxDelaySeconds, retryOptions.EnableRetryLogging);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating HTTP retry configuration");
                throw;
            }
        }

        public async Task LoadSettingsFromDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Loading HTTP retry configuration from Admin API");
                
                var maxRetryStr = await _adminApiClient.GetSettingAsync("HttpRetry:MaxRetries");
                var initialDelayStr = await _adminApiClient.GetSettingAsync("HttpRetry:InitialDelaySeconds");
                var maxDelayStr = await _adminApiClient.GetSettingAsync("HttpRetry:MaxDelaySeconds");
                var enableLoggingStr = await _adminApiClient.GetSettingAsync("HttpRetry:EnableRetryLogging");
                
                var options = new RetryOptions();
                
                if (int.TryParse(maxRetryStr, out int maxRetries))
                {
                    options.MaxRetries = maxRetries;
                }
                
                if (int.TryParse(initialDelayStr, out int initialDelay))
                {
                    options.InitialDelaySeconds = initialDelay;
                }
                
                if (int.TryParse(maxDelayStr, out int maxDelay))
                {
                    options.MaxDelaySeconds = maxDelay;
                }
                
                if (bool.TryParse(enableLoggingStr, out bool enableLogging))
                {
                    options.EnableRetryLogging = enableLogging;
                }
                
                // Use reflection to update the options instance since IOptionsMonitor is readonly
                var optionsField = typeof(RetryOptions).GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (optionsField != null)
                {
                    optionsField.SetValue(_options.CurrentValue, options);
                }
                
                _logger.LogInformation("HTTP retry configuration loaded: MaxRetries={MaxRetries}, InitialDelay={InitialDelay}s, MaxDelay={MaxDelay}s, Logging={Logging}",
                    options.MaxRetries, options.InitialDelaySeconds, options.MaxDelaySeconds, options.EnableRetryLogging);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading HTTP retry configuration from database");
            }
        }
    }
}
```

#### B. HttpTimeoutConfigurationServiceAdapter

Create a new file `/ConduitLLM.WebUI/Services/Adapters/HttpTimeoutConfigurationServiceAdapter.cs`:

```csharp
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.Providers.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services.Adapters
{
    public class HttpTimeoutConfigurationServiceAdapter : IHttpTimeoutConfigurationService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly IOptionsMonitor<TimeoutOptions> _options;
        private readonly ILogger<HttpTimeoutConfigurationServiceAdapter> _logger;

        public HttpTimeoutConfigurationServiceAdapter(
            IAdminApiClient adminApiClient,
            IOptionsMonitor<TimeoutOptions> options,
            ILogger<HttpTimeoutConfigurationServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient;
            _options = options;
            _logger = logger;
        }

        public async Task UpdateTimeoutConfigurationAsync(TimeoutOptions timeoutOptions)
        {
            try
            {
                await _adminApiClient.SetSettingAsync("HttpTimeout:DefaultTimeoutSeconds", timeoutOptions.DefaultTimeoutSeconds.ToString());
                await _adminApiClient.SetSettingAsync("HttpTimeout:LongRunningTimeoutSeconds", timeoutOptions.LongRunningTimeoutSeconds.ToString());
                
                _logger.LogInformation("HTTP timeout configuration updated via Admin API: DefaultTimeout={DefaultTimeout}s, LongRunningTimeout={LongRunningTimeout}s",
                    timeoutOptions.DefaultTimeoutSeconds, timeoutOptions.LongRunningTimeoutSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating HTTP timeout configuration");
                throw;
            }
        }

        public async Task LoadSettingsFromDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Loading HTTP timeout configuration from Admin API");
                
                var defaultTimeoutStr = await _adminApiClient.GetSettingAsync("HttpTimeout:DefaultTimeoutSeconds");
                var longRunningTimeoutStr = await _adminApiClient.GetSettingAsync("HttpTimeout:LongRunningTimeoutSeconds");
                
                var options = new TimeoutOptions();
                
                if (int.TryParse(defaultTimeoutStr, out int defaultTimeout))
                {
                    options.DefaultTimeoutSeconds = defaultTimeout;
                }
                
                if (int.TryParse(longRunningTimeoutStr, out int longRunningTimeout))
                {
                    options.LongRunningTimeoutSeconds = longRunningTimeout;
                }
                
                // Use reflection to update the options instance since IOptionsMonitor is readonly
                var optionsField = typeof(TimeoutOptions).GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (optionsField != null)
                {
                    optionsField.SetValue(_options.CurrentValue, options);
                }
                
                _logger.LogInformation("HTTP timeout configuration loaded: DefaultTimeout={DefaultTimeout}s, LongRunningTimeout={LongRunningTimeout}s",
                    options.DefaultTimeoutSeconds, options.LongRunningTimeoutSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading HTTP timeout configuration from database");
            }
        }
    }
}
```

#### C. ProviderStatusServiceAdapter

Create a new file `/ConduitLLM.WebUI/Services/Adapters/ProviderStatusServiceAdapter.cs`:

```csharp
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Adapters
{
    public class ProviderStatusServiceAdapter : IProviderStatusService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ProviderStatusServiceAdapter> _logger;

        public ProviderStatusServiceAdapter(
            IAdminApiClient adminApiClient,
            ILogger<ProviderStatusServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient;
            _logger = logger;
        }

        public async Task<Dictionary<string, ProviderStatus>> CheckAllProvidersStatusAsync()
        {
            try
            {
                _logger.LogInformation("Checking status of all providers via Admin API");
                return await _adminApiClient.CheckAllProvidersStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking status of all providers");
                return new Dictionary<string, ProviderStatus>();
            }
        }

        public async Task<ProviderStatus> CheckProviderStatusAsync(ProviderCredential provider)
        {
            return await CheckProviderStatusAsync(provider.ProviderName);
        }

        public async Task<ProviderStatus> CheckProviderStatusAsync(string providerName)
        {
            try
            {
                _logger.LogInformation("Checking status of provider {ProviderName} via Admin API", providerName);
                return await _adminApiClient.CheckProviderStatusAsync(providerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking status of provider {ProviderName}", providerName);
                return new ProviderStatus
                {
                    Status = ProviderStatus.StatusType.Unknown,
                    StatusMessage = $"Error: {ex.Message}",
                    LastCheckedUtc = DateTime.UtcNow
                };
            }
        }
    }
}
```

### 5. Update Startup Filters

#### A. Update HttpRetryConfigurationStartupFilter

Update `/ConduitLLM.WebUI/Services/HttpRetryConfigurationStartupFilter.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.WebUI.Services
{
    public class HttpRetryConfigurationStartupFilter : IStartupFilter
    {
        private readonly ILogger<HttpRetryConfigurationStartupFilter> _logger;

        public HttpRetryConfigurationStartupFilter(ILogger<HttpRetryConfigurationStartupFilter> logger)
        {
            _logger = logger;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                // Apply the retry configuration during startup
                using (var scope = builder.ApplicationServices.CreateScope())
                {
                    var adminApiClient = scope.ServiceProvider.GetRequiredService<IAdminApiClient>();
                    
                    try
                    {
                        // Initialize default configuration if needed via the API
                        var initialized = Task.Run(async () => 
                            await adminApiClient.InitializeHttpRetryConfigurationAsync()
                        ).GetAwaiter().GetResult();
                        
                        if (initialized)
                        {
                            _logger.LogInformation("HTTP retry configuration initialized successfully");
                        }
                        
                        // Now load the settings into the application options
                        var retryConfigService = scope.ServiceProvider.GetRequiredService<IHttpRetryConfigurationService>();
                        Task.Run(async () => await retryConfigService.LoadSettingsFromDatabaseAsync()).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error initializing HTTP retry configuration");
                    }
                }

                // Call the next step in the pipeline
                next(builder);
            };
        }
    }
}
```

#### B. Update HttpTimeoutConfigurationStartupFilter

Update `/ConduitLLM.WebUI/Services/HttpTimeoutConfigurationStartupFilter.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.WebUI.Services
{
    public class HttpTimeoutConfigurationStartupFilter : IStartupFilter
    {
        private readonly ILogger<HttpTimeoutConfigurationStartupFilter> _logger;

        public HttpTimeoutConfigurationStartupFilter(ILogger<HttpTimeoutConfigurationStartupFilter> logger)
        {
            _logger = logger;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                // Apply the timeout configuration during startup
                using (var scope = builder.ApplicationServices.CreateScope())
                {
                    var adminApiClient = scope.ServiceProvider.GetRequiredService<IAdminApiClient>();
                    
                    try
                    {
                        // Initialize default configuration if needed via the API
                        var initialized = Task.Run(async () => 
                            await adminApiClient.InitializeHttpTimeoutConfigurationAsync()
                        ).GetAwaiter().GetResult();
                        
                        if (initialized)
                        {
                            _logger.LogInformation("HTTP timeout configuration initialized successfully");
                        }
                        
                        // Now load the settings into the application options
                        var timeoutConfigService = scope.ServiceProvider.GetRequiredService<IHttpTimeoutConfigurationService>();
                        Task.Run(async () => await timeoutConfigService.LoadSettingsFromDatabaseAsync()).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error initializing HTTP timeout configuration");
                    }
                }

                // Call the next step in the pipeline
                next(builder);
            };
        }
    }
}
```

### 6. Update Program.cs Service Registrations

Update the service registrations in `Program.cs`:

```csharp
// Register Provider Status Service using Admin API adapter
builder.Services.AddScoped<IProviderStatusService, ProviderStatusServiceAdapter>();

// Register HTTP configuration services using Admin API adapters
builder.Services.AddOptions<RetryOptions>()
    .Bind(builder.Configuration.GetSection(RetryOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddScoped<IHttpRetryConfigurationService, HttpRetryConfigurationServiceAdapter>();
builder.Services.AddTransient<IStartupFilter, HttpRetryConfigurationStartupFilter>();

builder.Services.AddOptions<TimeoutOptions>()
    .Bind(builder.Configuration.GetSection(TimeoutOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddScoped<IHttpTimeoutConfigurationService, HttpTimeoutConfigurationServiceAdapter>();
builder.Services.AddTransient<IStartupFilter, HttpTimeoutConfigurationStartupFilter>();
```

### 7. Admin API Implementation

Implement the required Admin API endpoints in the ConduitLLM.Admin project:

- Create `ProviderHealthController` for health checks
- Add methods to `GlobalSettingsController` for configuration

### 8. Remove Entity Framework from WebUI project

1. Remove any remaining references to Microsoft.EntityFrameworkCore from the project file
2. Remove any IDbContextFactory<ConfigurationDbContext> usage
3. Search for any remaining references to DbContext and replace them

## Implementation Plan

Follow these steps in order:

1. Create a feature branch
   ```bash
   git checkout -b remove-ef-from-webui
   ```

2. Create the required interfaces in WebUI project
   - IHttpRetryConfigurationService
   - IHttpTimeoutConfigurationService
   - IProviderStatusService

3. Add required methods to IAdminApiClient

4. Implement the methods in AdminApiClient

5. Implement the service adapters
   - HttpRetryConfigurationServiceAdapter
   - HttpTimeoutConfigurationServiceAdapter
   - ProviderStatusServiceAdapter

6. Update the startup filters
   - HttpRetryConfigurationStartupFilter 
   - HttpTimeoutConfigurationStartupFilter

7. Update Program.cs service registrations

8. Implement required endpoints in Admin API

9. Test the API and adapters

10. Remove remaining Entity Framework references

11. Run integration tests to verify everything works

12. Create a PR for review

## Testing

1. Test each adapter service in isolation
2. Verify HTTP configuration still works
3. Check provider status checks work properly
4. Verify virtual key maintenance works
5. Test provider health monitoring
6. Ensure all UI components continue to function

## Commit Plan

Create the following commits:

1. "feat: Add interface definitions for WebUI services"
2. "feat: Extend IAdminApiClient with config methods"
3. "feat: Add configuration service adapters"
4. "feat: Add provider status service adapter"
5. "feat: Update startup filters to use Admin API"
6. "feat: Update Program.cs to use adapter services"
7. "refactor: Remove remaining EF references from WebUI"
8. "test: Add adapter service tests"

## Rollback Plan

If issues are encountered:

1. Revert the changes
2. Return to the hybrid approach where services can use either Admin API or direct DB access
3. Gradually migrate one service at a time with more targeted testing