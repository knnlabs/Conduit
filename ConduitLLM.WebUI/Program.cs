using System;
using System.IO;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core;
using ConduitLLM.Core.Caching;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Routing;
using ConduitLLM.Providers.Configuration;
using ConduitLLM.Providers.Extensions;
using ConduitLLM.WebUI;
using ConduitLLM.WebUI.Authorization;
using ConduitLLM.WebUI.Components;
using ConduitLLM.WebUI.Extensions;
// Data directory has been removed
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Middleware;
using ConduitLLM.WebUI.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticWebAssets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
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

// Validate insecure mode is only enabled in development environments
if (insecureMode)
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException("SECURITY VIOLATION: Insecure mode cannot be enabled in production environment. Remove CONDUIT_INSECURE environment variable.");
    }
    
    if (builder.Environment.IsStaging())
    {
        throw new InvalidOperationException("SECURITY VIOLATION: Insecure mode cannot be enabled in staging environment. Remove CONDUIT_INSECURE environment variable.");
    }
    
    // Log prominent warning for development environment
    Console.WriteLine("🚨 ==========================================");
    Console.WriteLine("🚨 WARNING: INSECURE MODE ENABLED");
    Console.WriteLine("🚨 Authentication is DISABLED!");
    Console.WriteLine("🚨 This mode is ONLY for development.");
    Console.WriteLine("🚨 Environment: " + builder.Environment.EnvironmentName);
    Console.WriteLine("🚨 ==========================================");
}

// Configure Redis connection string early for security services
var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
var redisConnectionString = Environment.GetEnvironmentVariable("CONDUIT_REDIS_CONNECTION_STRING");

if (!string.IsNullOrEmpty(redisUrl))
{
    try
    {
        redisConnectionString = ConduitLLM.Configuration.Utilities.RedisUrlParser.ParseRedisUrl(redisUrl);
    }
    catch
    {
        // Failed to parse REDIS_URL, will use legacy connection string if available
    }
}

// Add memory cache for failed login tracking
builder.Services.AddMemoryCache();

// Configure distributed cache
if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = builder.Configuration["CONDUIT_REDIS_INSTANCE_NAME"] ?? "conduit:";
    });
    Console.WriteLine($"[Conduit WebUI] Redis distributed cache configured: {redisConnectionString}");
}
else
{
    // Fall back to in-memory distributed cache
    builder.Services.AddDistributedMemoryCache();
    Console.WriteLine("[Conduit WebUI] Using in-memory distributed cache");
}

// Configure security options from environment variables
builder.Services.ConfigureSecurityOptions(builder.Configuration);

// Register unified security service
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.ISecurityService, ConduitLLM.WebUI.Services.SecurityService>();

// Register IP filter service adapter (for compatibility with Admin API)
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IIpFilterService, ConduitLLM.WebUI.Services.Adapters.IpFilterServiceAdapter>();

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

// Helper method for API base URL
static string GetApiBaseUrl() => Environment.GetEnvironmentVariable("CONDUIT_API_BASE_URL") ??
    (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ? "http://api:8080" : "http://localhost:5000");

// Register HttpClient for calling the API proxy
builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(GetApiBaseUrl());
    Console.WriteLine($"[Conduit WebUI] Configuring ApiClient with BaseAddress: {client.BaseAddress}");
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

// Register HTTP configuration services
builder.Services.AddOptions<RetryOptions>().Bind(builder.Configuration.GetSection(RetryOptions.SectionName)).ValidateDataAnnotations();
builder.Services.AddOptions<TimeoutOptions>().Bind(builder.Configuration.GetSection(TimeoutOptions.SectionName)).ValidateDataAnnotations();
builder.Services.AddSingleton<IHttpRetryConfigurationService, HttpRetryConfigurationService>();
builder.Services.AddSingleton<IHttpTimeoutConfigurationService, HttpTimeoutConfigurationService>();
builder.Services.AddTransient<IStartupFilter, HttpRetryConfigurationStartupFilter>();
builder.Services.AddTransient<IStartupFilter, HttpTimeoutConfigurationStartupFilter>();

// LLM Provider clients are registered in the HTTP API layer, not here
// WebUI communicates with providers through ConduitApiClient -> HTTP API -> Conduit -> Provider Clients
builder.Services.AddScoped<ConduitLLM.WebUI.Services.ConfigurationChangeNotifier>();

// Register controllers and routing
builder.Services.AddControllers();
builder.Services.AddRouting(options => options.ConstraintMap.Add("controller", typeof(string)));
Console.WriteLine("[Conduit WebUI] Registering controllers");

// Register core services
builder.Services.AddTransient<ConduitLLM.WebUI.Services.InitialSetupService>();
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.NotificationService>();
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.IToastNotificationService, ConduitLLM.WebUI.Services.ToastNotificationService>();
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.MarkdownService>();
builder.Services.AddSingleton<ConduitLLM.WebUI.Interfaces.INavigationStateService, ConduitLLM.WebUI.Services.SignalRNavigationStateService>();
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.VersionCheckService>();
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.IFileVersionService, ConduitLLM.WebUI.Services.FileVersionService>();
builder.Services.AddSingleton<ICacheMetricsService, CacheMetricsService>();
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.ICostCalculationService, ConduitLLM.Core.Services.CostCalculationService>();
builder.Services.AddSingleton<ConduitLLM.Core.ConduitRegistry>();
builder.Services.AddHttpClient("GithubApi", client => client.DefaultRequestHeaders.Add("User-Agent", "Conduit-Version-Check"));


// Conditional Redis service registration
if (builder.Configuration.GetSection("Caching")?.GetValue<string>("CacheType")?.ToLowerInvariant() == "redis")
    builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IRedisCacheMetricsService, ConduitLLM.WebUI.Services.RedisCacheMetricsService>();

// Provider and admin services
builder.Services.AddScoped<ConduitLLM.WebUI.Services.ProviderModelsService>();

// Register Conduit API clients with resilience policies
builder.Services.AddHttpClient("ConduitAPI", client => client.BaseAddress = new Uri(GetApiBaseUrl()))
    .AddAdminApiResiliencePolicies();

builder.Services.AddHttpClient<IConduitApiClient, ConduitApiClient>(client =>
{
    client.BaseAddress = new Uri(GetApiBaseUrl());
    Console.WriteLine($"[Conduit WebUI] Configuring ConduitApiClient with BaseAddress: {client.BaseAddress}");
})
.AddResiliencePolicies(options =>
{
    options.RetryCount = 3;
    options.CircuitBreakerThreshold = 5;
    options.TimeoutSeconds = 60; // Increased timeout for image generation
})
.ConfigureHttpClient(client =>
{
    // Set a default timeout on the HttpClient itself as a safety net
    client.Timeout = TimeSpan.FromSeconds(120); // Even longer timeout for the HTTP client
});

// Register Admin API client and compatibility services
builder.Services.AddAdminApiClient(builder.Configuration);
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IGlobalSettingService>(sp => sp.GetRequiredService<AdminApiClient>());
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IVirtualKeyService>(sp => sp.GetRequiredService<AdminApiClient>());
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IProviderHealthService>(sp => sp.GetRequiredService<AdminApiClient>());
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IProviderCredentialService>(sp => sp.GetRequiredService<AdminApiClient>());
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IProviderStatusService, ConduitLLM.WebUI.Services.ProviderStatusService>();
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IModelCostService>(sp => sp.GetRequiredService<AdminApiClient>());
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IModelProviderMappingService>(sp => sp.GetRequiredService<AdminApiClient>());

// Register global setting repository adapter for CacheStatusService
builder.Services.AddScoped<ConduitLLM.Configuration.Repositories.IGlobalSettingRepository, ConduitLLM.WebUI.Services.AdminApiGlobalSettingRepositoryAdapter>();

// Register cache status service (required by CachingSettings page)
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.ICacheStatusService, ConduitLLM.WebUI.Services.CacheStatusService>();
builder.Services.AddSingleton<ConduitLLM.WebUI.Interfaces.IAdminApiHealthService, ConduitLLM.WebUI.Services.AdminApiHealthService>();

// Background services
builder.Services.AddHostedService<ConduitLLM.WebUI.Services.VirtualKeyMaintenanceService>();
builder.Services.AddHostedService<ConduitLLM.WebUI.Services.ProviderHealthMonitorService>();

// Add SignalR services explicitly
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 512 * 1024; // 512 KB
});

// Add antiforgery services
builder.Services.AddAntiforgery();

// Configure Data Protection with Redis persistence
// Redis connection string was already configured earlier
builder.Services.AddRedisDataProtection(redisConnectionString, "Conduit");

// Add standardized health checks
// Note: WebUI doesn't directly access the database, so we don't need database or provider health checks
builder.Services.AddConduitHealthChecks(connectionString: null, redisConnectionString, includeProviderCheck: false)
    .AddCheck<ConduitLLM.WebUI.HealthChecks.AdminApiHealthCheck>("admin_api", tags: new[] { "api", "critical", "ready" });

// Add Razor Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register context management services
builder.Services.AddConduitContextManagement(builder.Configuration);

var app = builder.Build();

// Log usage mode, deprecation warnings, and validate Redis URL
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Using Admin API mode. All services are using provider implementations.");
    ConduitLLM.Configuration.Extensions.DeprecationWarnings.LogEnvironmentVariableDeprecations(logger);
    
    // Validate Redis URL if provided
    var envRedisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
    if (!string.IsNullOrEmpty(envRedisUrl))
    {
        ConduitLLM.Configuration.Services.RedisUrlValidator.ValidateAndLog(envRedisUrl, logger, "WebUI Service");
    }

    // Log security configuration
    var securityOptions = scope.ServiceProvider.GetRequiredService<IOptions<ConduitLLM.WebUI.Options.SecurityOptions>>().Value;
    
    logger.LogInformation("=== Security Configuration ===");
    logger.LogInformation("IP Filtering: {Status}", securityOptions.IpFiltering.Enabled ? "Enabled" : "Disabled");
    if (securityOptions.IpFiltering.Enabled)
    {
        logger.LogInformation("  - Mode: {Mode}", securityOptions.IpFiltering.Mode);
        logger.LogInformation("  - Allow Private IPs: {AllowPrivate}", securityOptions.IpFiltering.AllowPrivateIps);
        logger.LogInformation("  - Whitelist Rules: {Count}", securityOptions.IpFiltering.Whitelist.Count);
        logger.LogInformation("  - Blacklist Rules: {Count}", securityOptions.IpFiltering.Blacklist.Count);
    }
    logger.LogInformation("Rate Limiting: {Status}", securityOptions.RateLimiting.Enabled ? "Enabled" : "Disabled");
    if (securityOptions.RateLimiting.Enabled)
    {
        logger.LogInformation("  - Max Requests: {MaxRequests} per {Window} seconds", 
            securityOptions.RateLimiting.MaxRequests, securityOptions.RateLimiting.WindowSeconds);
    }
    logger.LogInformation("Failed Login Protection: Enabled");
    logger.LogInformation("  - Max Attempts: {MaxAttempts}", securityOptions.FailedLogin.MaxAttempts);
    logger.LogInformation("  - Ban Duration: {Minutes} minutes", securityOptions.FailedLogin.BanDurationMinutes);
    logger.LogInformation("==============================");
    
    // Log insecure mode warning if enabled
    if (insecureMode)
    {
        logger.LogWarning("🚨 INSECURE MODE IS ENABLED - Authentication is bypassed!");
        logger.LogWarning("🚨 This mode should ONLY be used in development environments.");
        logger.LogWarning("🚨 Current environment: {Environment}", app.Environment.EnvironmentName);
    }
}

// Initialize Master Key and WebUI Virtual Key using InitialSetupService
using (var scope = app.Services.CreateScope())
{
    var initialSetupService = scope.ServiceProvider.GetRequiredService<ConduitLLM.WebUI.Services.InitialSetupService>();
    await initialSetupService.EnsureMasterKeyExistsAsync();
    
    // Also ensure WebUI virtual key exists for API authentication
    try
    {
        await initialSetupService.EnsureWebUIVirtualKeyExistsAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to ensure WebUI virtual key exists. This may impact API authentication.");
        // Don't throw - allow the app to start even if virtual key creation fails
    }

    // Verify the authentication keys are set and accessible
    var envWebUIKey = Environment.GetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY");
    var envMasterKey = Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY");
    
    if (string.IsNullOrEmpty(envWebUIKey))
    {
        if (string.IsNullOrEmpty(envMasterKey))
        {
            Console.WriteLine("WARNING: Neither CONDUIT_WEBUI_AUTH_KEY nor CONDUIT_MASTER_KEY environment variables are set!");
            Console.WriteLine("         WebUI authentication will not work without at least one of these keys.");
        }
        else
        {
            Console.WriteLine("INFO: CONDUIT_WEBUI_AUTH_KEY is not set, falling back to CONDUIT_MASTER_KEY for WebUI authentication.");
            Console.WriteLine("      Consider setting CONDUIT_WEBUI_AUTH_KEY for better security separation.");
        }
    }
    else
    {
        Console.WriteLine($"CONDUIT_WEBUI_AUTH_KEY environment variable is set. Length: {envWebUIKey.Length}");
    }

    // Get global settings
    var globalSettingService = scope.ServiceProvider.GetRequiredService<ConduitLLM.WebUI.Interfaces.IGlobalSettingService>();
    var storedHash = await globalSettingService.GetMasterKeyHashAsync();
    Console.WriteLine($"Master key hash from database: {storedHash ?? "NOT FOUND"}");
}

// Initialize services
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        // Initialize Router if enabled
        var routerOptions = new RouterOptions();
        app.Configuration.GetSection(RouterOptions.SectionName).Bind(routerOptions);
        if (routerOptions.Enabled)
        {
            logger.LogInformation("Initializing LLM Router...");
            var routerService = services.GetRequiredService<IRouterService>();
            await routerService.InitializeRouterAsync();
            logger.LogInformation("LLM Router initialized successfully");
        }

        // Initialize VersionCheckService
        var versionCheckService = services.GetRequiredService<ConduitLLM.WebUI.Services.VersionCheckService>();
        versionCheckService.Initialize();
        logger.LogInformation("Version check service initialized successfully");
        await versionCheckService.CheckForNewVersionAsync(forceCheck: true);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during service initialization: {Message}", ex.Message);
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

// Ensure static files are served properly
app.UseDefaultFiles();
app.UseStaticFiles();

// app.UseHttpsRedirection(); // Removed as HTTPS is handled by external proxy (e.g., Railway)

app.UseRouting();

// Add security headers middleware (should be early in pipeline)
app.UseSecurityHeaders();

// Add unified security middleware (includes rate limiting, IP filtering, and failed login protection)
app.UseSecurity();

// Add resilience logging middleware
app.UseMiddleware<ConduitLLM.WebUI.Middleware.ResilienceLoggingMiddleware>();

app.UseAntiforgery();

// Add authentication middleware before authorization
app.UseAuthentication();
app.UseAuthorization();

// IP filtering is now handled by the unified security middleware

// Middleware simplified - deprecated middleware removed as API endpoints moved to ConduitLLM.Http project

// Map standardized health check endpoints
app.MapConduitHealthChecks();

// Map controllers first
app.MapControllers();

// Then map Blazor components with explicit render mode
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

Console.WriteLine("[Conduit WebUI] Blazor components and controllers registered");

// --- Add Minimal API endpoint for Login ---
// Changed rememberMe to nullable bool (bool?)
app.MapPost("/account/login", async (HttpContext context, [FromForm] string masterKey, [FromForm] bool? rememberMe, [FromForm] string? returnUrl, ILogger<Program> logger, ConduitLLM.WebUI.Interfaces.IGlobalSettingService globalSettingService, ConduitLLM.WebUI.Services.ISecurityService securityService) =>
{
    logger.LogInformation("POST /account/login received.");
    logger.LogInformation("Remember me checkbox value: {RememberMe}", rememberMe);
    
    // Get client IP
    var clientIp = GetClientIpAddress(context);
    
    // Log IP classification for debugging
    var ipClassification = securityService.ClassifyIpAddress(clientIp);
    logger.LogInformation("Login attempt from {IpAddress} (Classification: {Classification})", clientIp, ipClassification);
    
    // Check if IP is banned
    if (await securityService.IsIpBannedAsync(clientIp))
    {
        logger.LogWarning("Login attempt from banned IP: {IpAddress}", clientIp);
        context.Response.StatusCode = 429;
        return Results.Redirect("/login?error=TooManyAttempts");
    }
    
    // Check WebUI auth key first
    string? envWebUIKey = Environment.GetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY");
    string? envMasterKey = Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY");
    bool isValid = false;

    if (!string.IsNullOrEmpty(envWebUIKey))
    {
        isValid = string.Equals(masterKey?.Trim(), envWebUIKey.Trim(), StringComparison.OrdinalIgnoreCase);
        if (isValid)
        {
            logger.LogInformation("WebUI auth key validated successfully");
        }
    }
    else if (!string.IsNullOrEmpty(envMasterKey))
    {
        // Fall back to master key for backward compatibility
        isValid = string.Equals(masterKey?.Trim(), envMasterKey.Trim(), StringComparison.OrdinalIgnoreCase);
        if (isValid)
        {
            logger.LogInformation("Master key validated successfully (backward compatibility)");
        }
    }
    else
    {
        logger.LogWarning("Neither CONDUIT_WEBUI_AUTH_KEY nor CONDUIT_MASTER_KEY environment variables are set during POST /account/login.");
    }

    if (isValid)
    {
        logger.LogInformation("Login successful via POST /account/login.");

        // Clear failed login attempts on successful login
        await securityService.ClearFailedLoginAttemptsAsync(clientIp);

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

        return Results.Redirect(!string.IsNullOrEmpty(returnUrl) ? returnUrl : "/");
    }
    else
    {
        // Record failed login attempt
        await securityService.RecordFailedLoginAsync(clientIp);
        logger.LogWarning("Login failed via POST /account/login from IP: {IpAddress}", clientIp);
        
        // Redirect back to login page with an error indicator
        var redirectUrl = $"/login?error=InvalidKey{(string.IsNullOrEmpty(returnUrl) ? "" : $"&returnUrl={Uri.EscapeDataString(returnUrl)}")}";
        return Results.Redirect(redirectUrl);
    }
});

// Helper function to get client IP address
static string GetClientIpAddress(HttpContext context)
{
    // Check X-Forwarded-For header first (for reverse proxies)
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrEmpty(forwardedFor))
    {
        // Take the first IP in the chain
        var ip = forwardedFor.Split(',').First().Trim();
        if (System.Net.IPAddress.TryParse(ip, out _))
        {
            return ip;
        }
    }

    // Check X-Real-IP header
    var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
    if (!string.IsNullOrEmpty(realIp) && System.Net.IPAddress.TryParse(realIp, out _))
    {
        return realIp;
    }

    // Fall back to direct connection IP
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
// --- End Minimal API endpoint ---

app.Run();

// Make the implicit Program class accessible to tests
namespace ConduitLLM.WebUI
{
    public partial class Program { }
}
