using System;
using System.IO;
using ConduitLLM.Configuration;
using ConduitLLM.WebUI;
using ConduitLLM.WebUI.Extensions;
using Microsoft.AspNetCore.Authentication; 
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core;
using ConduitLLM.Core.Caching;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Routing;
using ConduitLLM.WebUI.Authorization;
using ConduitLLM.WebUI.Components;
// Data directory has been removed
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Middleware;
using ConduitLLM.WebUI.Services;
using ConduitLLM.WebUI.Services.Providers;
using ConduitLLM.Providers.Extensions;
using ConduitLLM.Providers.Configuration;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.StaticWebAssets;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
    Args = args,
    // Don't load appsettings.json
    EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
});
builder.Configuration.Sources.Clear();
builder.Configuration.AddEnvironmentVariables();

// Entity Framework and direct database access have been removed, WebUI now only uses the Admin API
Console.WriteLine("[Conduit WebUI] Using Admin API client mode");

// Check for insecure mode
bool insecureMode = Environment.GetEnvironmentVariable("CONDUIT_INSECURE")?.ToLowerInvariant() == "true";

// Add services to the container.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "ConduitAuth";
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromDays(1);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

// Add authorization with policy requirements
builder.Services.AddAuthorization(options =>
{
    // Create a policy based on insecure mode state
    var masterKeyPolicy = new AuthorizationPolicyBuilder();
    
    if (insecureMode)
    {
        // In insecure mode, no requirements needed - always passes
        masterKeyPolicy.RequireAssertion(_ => true);
    }
    else
    {
        // Normal authentication requires the claim
        masterKeyPolicy.RequireClaim("MasterKeyAuthenticated", "true");
    }
    
    // Register the policy
    options.AddPolicy("MasterKeyPolicy", masterKeyPolicy.Build());
    
    // Set this as the default policy so it applies to all [Authorize] attributes without parameters
    options.DefaultPolicy = masterKeyPolicy.Build();
    
    // Configure a fallback policy that allows anonymous access by default
    // This allows public pages like Login and AccessDenied to be accessed without authentication
    // Individual pages will use [Authorize] attribute as needed
    options.FallbackPolicy = null; // Allow anonymous access by default
});

// Register InsecureModeProvider for displaying insecure mode banner
builder.Services.AddSingleton<ConduitLLM.WebUI.Interfaces.IInsecureModeProvider>(new ConduitLLM.WebUI.Services.InsecureModeProvider { IsInsecureMode = insecureMode });

// Add HttpContextAccessor - required for authentication in Razor components
builder.Services.AddHttpContextAccessor();

// Register HttpClient for calling the API proxy
// Default client for general use - REMOVED incorrect default registration with hardcoded base address
// builder.Services.AddHttpClient(); 

// Named client specifically for calling the Conduit HTTP API
builder.Services.AddHttpClient("ApiClient", client =>
{
    // Read the base URL from environment variable, fallback to "http://api:8080" for container env
    var apiBaseUrl = Environment.GetEnvironmentVariable("CONDUIT_API_BASE_URL") ?? 
                    (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ? 
                     "http://api:8080" : "http://localhost:5000");
    client.BaseAddress = new Uri(apiBaseUrl);
    Console.WriteLine($"[Conduit WebUI] Configuring ApiClient with BaseAddress: {apiBaseUrl}");
});

// Register Cache Service
builder.Services.AddCacheService(builder.Configuration);

// Register LLM Caching Components
builder.Services.AddLLMCaching();

// Configure router options
builder.Services.Configure<RouterOptions>(
    builder.Configuration.GetSection(RouterOptions.SectionName));

// Register Router services using the extension method
builder.Services.AddRouterServices(builder.Configuration);

// Register HTTP retry configuration services - using Admin API for settings
builder.Services.AddOptions<RetryOptions>()
    .Bind(builder.Configuration.GetSection(RetryOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddTransient<IStartupFilter, HttpRetryConfigurationStartupFilter>();

// Register HTTP timeout configuration services - using Admin API for settings
builder.Services.AddOptions<TimeoutOptions>()
    .Bind(builder.Configuration.GetSection(TimeoutOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddTransient<IStartupFilter, HttpTimeoutConfigurationStartupFilter>();

// Register HttpClient with retry policies for LLM providers
builder.Services.AddLLMProviderHttpClients();
builder.Services.AddScoped<ConduitLLM.WebUI.Services.ConfigurationChangeNotifier>();
// Database settings startup filter has been removed
// Provider Credential Service has been migrated to always use the adapter implementation

// Model costs tracking service - ModelCostService has been migrated to always use the adapter
builder.Services.AddScoped<ConduitLLM.Configuration.IModelProviderMappingService, ConduitLLM.Configuration.ModelProviderMappingService>();

// Repository pattern is now fully integrated and always enabled
// No need for separate configuration options

// Repository pattern configuration is now integrated directly in the repositories themselves
// No need for separate monitoring service

// Repository pattern and direct database access has been removed from WebUI

// Register the CacheStatusServiceProvider instead of the direct repository implementation
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.ICacheStatusService, ConduitLLM.WebUI.Services.Providers.CacheStatusServiceProvider>();

// Register database backup service
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IDatabaseBackupService, ConduitLLM.WebUI.Services.Providers.DatabaseBackupServiceProvider>();

// Register database backup service adapter for backward compatibility (to be removed)
// No longer needed as we're using our provider directly
// builder.Services.AddScoped<ConduitLLM.WebUI.Services.DatabaseBackupServiceAdapter>();

// Repository Virtual Key Service has been migrated to always use the adapter implementation

// Register controllers
builder.Services.AddControllers();
Console.WriteLine("[Conduit WebUI] Registering controllers");

// Configure routing options
builder.Services.AddRouting(options => {
    options.ConstraintMap.Add("controller", typeof(string));
});

// Register remaining services
builder.Services.AddTransient<ConduitLLM.WebUI.Services.InitialSetupService>(); 
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.NotificationService>();

// Register Version Check Service
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.VersionCheckService>();
builder.Services.AddHttpClient("GithubApi", client => {
    client.DefaultRequestHeaders.Add("User-Agent", "Conduit-Version-Check");
});

// Register Cache Metrics Service
builder.Services.AddSingleton<ICacheMetricsService, CacheMetricsService>();

// Register Redis Cache Metrics Service (only when Redis is configured)
if (builder.Configuration.GetSection("Caching")?.GetValue<string>("CacheType")?.ToLowerInvariant() == "redis")
{
    builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IRedisCacheMetricsService, ConduitLLM.WebUI.Services.RedisCacheMetricsService>();
}

// Register Cost Calculation Service
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.ICostCalculationService, ConduitLLM.Core.Services.CostCalculationService>();

// Add Conduit related services
builder.Services.AddSingleton<ConduitLLM.Core.ConduitRegistry>();

// Add provider models service for dropdown UI
builder.Services.AddScoped<ConduitLLM.WebUI.Services.ProviderModelsService>();

// Configure HTTP client for API access
builder.Services.AddHttpClient("ConduitAPI", client => {
    // Read the base URL from environment variable, fallback to "http://api:8080" for container env
    var apiBaseUrl = Environment.GetEnvironmentVariable("CONDUIT_API_BASE_URL") ??
                    (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ?
                     "http://api:8080" : "http://localhost:5000");
    client.BaseAddress = new Uri(apiBaseUrl);
    Console.WriteLine($"[Conduit WebUI] Configuring ConduitAPI client with BaseAddress: {apiBaseUrl}");
});

// Register the Conduit API client
builder.Services.AddHttpClient<IConduitApiClient, ConduitApiClient>(client => {
    // Same API Base URL as the default named client
    var apiBaseUrl = Environment.GetEnvironmentVariable("CONDUIT_API_BASE_URL") ??
                    (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ?
                     "http://api:8080" : "http://localhost:5000");
    client.BaseAddress = new Uri(apiBaseUrl);
    Console.WriteLine($"[Conduit WebUI] Configuring ConduitApiClient with BaseAddress: {apiBaseUrl}");
});

// Register the Admin API client
builder.Services.AddAdminApiClient(builder.Configuration);

// Add caching decorator for Admin API client
builder.Services.Decorate<IAdminApiClient, ConduitLLM.WebUI.Services.CachingAdminApiClient>();

// Register service providers that use the AdminApiClient
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IGlobalSettingService, ConduitLLM.WebUI.Services.Providers.GlobalSettingServiceProvider>();
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IVirtualKeyService, ConduitLLM.WebUI.Services.Providers.VirtualKeyServiceProvider>();
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IModelCostService, ConduitLLM.WebUI.Services.Providers.ModelCostServiceProvider>();
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IIpFilterService, ConduitLLM.WebUI.Services.Providers.IpFilterServiceProvider>();
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IProviderHealthService, ConduitLLM.WebUI.Services.Providers.ProviderHealthServiceProvider>();
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IRequestLogService, ConduitLLM.WebUI.Services.Providers.RequestLogServiceProvider>();

// Register remaining service providers
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.ICostDashboardService, ConduitLLM.WebUI.Services.Providers.CostDashboardServiceProvider>();
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IModelProviderMappingService, ConduitLLM.WebUI.Services.Providers.ModelProviderMappingServiceProvider>();
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IRouterService, ConduitLLM.WebUI.Services.Providers.RouterServiceProvider>();

// Register remaining service providers
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IProviderCredentialService, ConduitLLM.WebUI.Services.Providers.ProviderCredentialServiceProvider>();
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IHttpRetryConfigurationService, ConduitLLM.WebUI.Services.Providers.HttpRetryConfigurationServiceProvider>();
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IHttpTimeoutConfigurationService, ConduitLLM.WebUI.Services.Providers.HttpTimeoutConfigurationServiceProvider>();
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IProviderStatusService, ConduitLLM.WebUI.Services.Providers.ProviderStatusServiceProvider>();

// Register repository implementations needed by components
builder.Services.AddScoped<ConduitLLM.Configuration.Repositories.IProviderHealthRepository, ConduitLLM.WebUI.Services.Repositories.ProviderHealthRepositoryAdapter>();

// Chat page has been migrated to use Admin API
// No need for the workaround middleware anymore

// TODO: Implement Configuration.Services interfaces
// For now, commented out until we implement all service providers
/*
builder.Services.AddScoped<ConduitLLM.Configuration.Services.IModelCostService>(sp => {
    // Providers can implement Configuration.Services interfaces directly
    var service = sp.GetRequiredService<IModelCostService>();
    if (service is ConduitLLM.Configuration.Services.IModelCostService configService) {
        return configService;
    }
    throw new InvalidOperationException(
        $"The registered IModelCostService implementation does not implement ConduitLLM.Configuration.Services.IModelCostService");
});
    
builder.Services.AddScoped<ConduitLLM.Configuration.Services.IRequestLogService>(sp => {
    var service = sp.GetRequiredService<IRequestLogService>();
    if (service is ConduitLLM.Configuration.Services.IRequestLogService configService) {
        return configService;
    }
    throw new InvalidOperationException(
        $"The registered IRequestLogService implementation does not implement ConduitLLM.Configuration.Services.IRequestLogService");
});
*/

// Register Admin API health service
builder.Services.AddSingleton<ConduitLLM.WebUI.Interfaces.IAdminApiHealthService, ConduitLLM.WebUI.Services.AdminApiHealthService>();

// Register Admin API cache service
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IAdminApiCacheService, ConduitLLM.WebUI.Services.AdminApiCacheService>();

// Add Virtual Key maintenance background service
builder.Services.AddHostedService<ConduitLLM.WebUI.Services.VirtualKeyMaintenanceService>();

// Register Provider Health Monitoring service
builder.Services.AddHostedService<ConduitLLM.WebUI.Services.ProviderHealthMonitorService>();

// Add Razor Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register context management services
builder.Services.AddConduitContextManagement(builder.Configuration);

var app = builder.Build();

// Log usage mode
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Using Admin API mode. All services are using provider implementations.");
}

// Print master key for debugging purposes
var masterKey = Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY");

// Initialize Master Key using InitialSetupService
using (var scope = app.Services.CreateScope())
{
    var initialSetupService = scope.ServiceProvider.GetRequiredService<ConduitLLM.WebUI.Services.InitialSetupService>();
    await initialSetupService.EnsureMasterKeyExistsAsync();
    
    // Verify the master key is set and accessible
    var envMasterKey = Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY");
    if (string.IsNullOrEmpty(envMasterKey))
    {
        Console.WriteLine("WARNING: CONDUIT_MASTER_KEY environment variable is not set!");
    }
    else
    {
        Console.WriteLine($"CONDUIT_MASTER_KEY environment variable is set. Length: {envMasterKey.Length}");
    }

    // Get global settings
    var globalSettingService = scope.ServiceProvider.GetRequiredService<ConduitLLM.WebUI.Interfaces.IGlobalSettingService>();
    var storedHash = await globalSettingService.GetMasterKeyHashAsync();
    Console.WriteLine($"Master key hash from database: {storedHash ?? "NOT FOUND"}");
}

// Initialize Router with configuration
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Check if router is enabled
        var routerOptions = new RouterOptions();
        app.Configuration.GetSection(RouterOptions.SectionName).Bind(routerOptions);

        if (routerOptions.Enabled)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Initializing LLM Router...");
            
            // Get the required services
            var routerService = services.GetRequiredService<IRouterService>();
            
            try
            {
                // Initialize the router with the current configuration
                await routerService.InitializeRouterAsync();
                logger.LogInformation("LLM Router initialized successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error initializing LLM Router: {Message}", ex.Message);
            }
        }
        
        // Initialize VersionCheckService
        try
        {
            var versionCheckService = services.GetRequiredService<ConduitLLM.WebUI.Services.VersionCheckService>();
            versionCheckService.Initialize();
            
            // Get logger specifically for this section
            var versionLogger = services.GetRequiredService<ILogger<Program>>();
            versionLogger.LogInformation("Version check service initialized successfully");
            
            // Perform an initial version check
            await versionCheckService.CheckForNewVersionAsync(forceCheck: true);
        }
        catch (Exception ex)
        {
            var versionLogger = services.GetRequiredService<ILogger<Program>>();
            versionLogger.LogError(ex, "Error initializing version check service: {Message}", ex.Message);
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
        logger.LogError(ex, "An error occurred during initialization.");
    }
}

// Check for auto-login preference
using (var autoLoginScope = app.Services.CreateScope())
{
    var globalSettingService = autoLoginScope.ServiceProvider.GetRequiredService<ConduitLLM.WebUI.Interfaces.IGlobalSettingService>();
    var logger = autoLoginScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        var autoLoginSetting = await globalSettingService.GetSettingAsync("AutoLogin");
        logger.LogInformation("Retrieved AutoLogin setting: {AutoLoginSetting}", autoLoginSetting);
        if (bool.TryParse(autoLoginSetting, out bool autoLogin) && autoLogin)
        {
            logger.LogInformation("Auto-login is enabled, checking for master key in environment");
            
            string? envMasterKey = Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY");
            if (!string.IsNullOrEmpty(envMasterKey))
            {
                logger.LogInformation("Master key found in environment, auto-login will be performed");
                // Note: Actual login will happen on first page access
            }
            else
            {
                logger.LogWarning("Auto-login is enabled but CONDUIT_MASTER_KEY is not set");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error checking auto-login preference");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
else
{
    app.UseDeveloperExceptionPage();
}

// Ensure static files are served properly for development
app.UseStaticFiles();
app.UseDefaultFiles();

// app.UseHttpsRedirection(); // Removed as HTTPS is handled by external proxy (e.g., Railway)

app.UseAntiforgery();

app.MapStaticAssets();

// Add authentication middleware before authorization
app.UseAuthentication();
app.UseAuthorization();

// Add AdminApiConnection middleware to detect API connection issues
app.UseMiddleware<ConduitLLM.WebUI.Middleware.AdminApiConnectionMiddleware>();

// Add IP Filtering, Virtual Key Authentication, and LLM Request Tracking middleware
app.UseIpFiltering();
app.UseVirtualKeyAuthentication();
app.UseLlmRequestTracking();

// Chat page workaround middleware removed - now using proper Blazor component with Admin API

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Ensure controllers are mapped for our API endpoints
app.MapControllers();
Console.WriteLine("[Conduit WebUI] Controllers registered");

// --- Add Minimal API endpoint for Login ---
// Changed rememberMe to nullable bool (bool?)
app.MapPost("/account/login", async (HttpContext context, [FromForm] string masterKey, [FromForm] bool? rememberMe, [FromForm] string? returnUrl, ILogger<Program> logger, ConduitLLM.WebUI.Interfaces.IGlobalSettingService globalSettingService) =>
{
    logger.LogInformation("POST /account/login received.");
    logger.LogInformation("Remember me checkbox value: {RememberMe}", rememberMe);
    string? envMasterKey = Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY");
    bool isValid = false;

    if (!string.IsNullOrEmpty(envMasterKey))
    {
        // Use the same comparison logic as before
        isValid = string.Equals(masterKey?.Trim(), envMasterKey.Trim(), StringComparison.OrdinalIgnoreCase);
        logger.LogInformation("Environment variable key comparison result: {IsValid}", isValid);
    }
    else
    {
        // Optional: Add fallback to database hash check here if needed in the future
        logger.LogWarning("CONDUIT_MASTER_KEY environment variable not set during POST /account/login.");
    }

    if (isValid)
    {
        logger.LogInformation("Login successful via POST /account/login.");
        
        // Save the auto-login preference
        if (rememberMe.HasValue)
        {
            await globalSettingService.SetSettingAsync("AutoLogin", rememberMe.Value.ToString());
            logger.LogInformation("Auto-login preference saved: {AutoLogin}", rememberMe.Value);
        }
        
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Admin"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Administrator"),
            new System.Security.Claims.Claim("MasterKeyAuthenticated", "true")
        };
        var claimsIdentity = new System.Security.Claims.ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            // Handle nullable rememberMe, default to false if null (unchecked)
            IsPersistent = rememberMe ?? false, 
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays((rememberMe ?? false) ? 7 : 1) 
        };

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new System.Security.Claims.ClaimsPrincipal(claimsIdentity),
            authProperties);

        return Results.Redirect(returnUrl ?? "/");
    }
    else
    {
        logger.LogWarning("Login failed via POST /account/login.");
        // Redirect back to login page with an error indicator
        var redirectUrl = $"/login?error=InvalidKey{(string.IsNullOrEmpty(returnUrl) ? "" : $"&returnUrl={Uri.EscapeDataString(returnUrl)}")}";
        return Results.Redirect(redirectUrl);
    }
});
// --- End Minimal API endpoint ---

app.Run();