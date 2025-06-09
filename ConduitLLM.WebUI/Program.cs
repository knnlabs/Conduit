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
using Microsoft.Extensions.Diagnostics.HealthChecks;
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

// Helper method for API base URL
static string GetApiBaseUrl() => Environment.GetEnvironmentVariable("CONDUIT_API_BASE_URL") ?? 
    (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ? "http://api:8080" : "http://localhost:5000");

// Register HttpClient for calling the API proxy
builder.Services.AddHttpClient("ApiClient", client => {
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
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.IToastService, ConduitLLM.WebUI.Services.ToastService>();
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.VersionCheckService>();
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

builder.Services.AddHttpClient<IConduitApiClient, ConduitApiClient>(client => {
    client.BaseAddress = new Uri(GetApiBaseUrl());
    Console.WriteLine($"[Conduit WebUI] Configuring ConduitApiClient with BaseAddress: {client.BaseAddress}");
})
.AddAdminApiResiliencePolicies();

// Register Admin API client and compatibility services
builder.Services.AddAdminApiClient(builder.Configuration);
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IGlobalSettingService>(sp => sp.GetRequiredService<AdminApiClient>());
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IVirtualKeyService>(sp => sp.GetRequiredService<AdminApiClient>());
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IProviderHealthService>(sp => sp.GetRequiredService<AdminApiClient>());
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IProviderCredentialService>(sp => sp.GetRequiredService<AdminApiClient>());
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IProviderStatusService, ConduitLLM.WebUI.Services.ProviderStatusService>();

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

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<ConduitLLM.WebUI.HealthChecks.AdminApiHealthCheck>("admin_api", tags: new[] { "api", "critical" })
    .AddCheck("self", () => HealthCheckResult.Healthy("Application is running"), tags: new[] { "self" });

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

// Add resilience logging middleware
app.UseMiddleware<ConduitLLM.WebUI.Middleware.ResilienceLoggingMiddleware>();

app.UseAntiforgery();

// Add authentication middleware before authorization
app.UseAuthentication();
app.UseAuthorization();

// Middleware simplified - deprecated middleware removed as API endpoints moved to ConduitLLM.Http project

// Map health checks
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});

// Map specific health check for critical components only
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("critical")
});

// Map controllers first
app.MapControllers();

// Then map Blazor components with explicit render mode
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

Console.WriteLine("[Conduit WebUI] Blazor components and controllers registered");

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