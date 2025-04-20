using System;
using System;
using System.IO;
using ConduitLLM.Configuration;
using ConduitLLM.WebUI;
using Microsoft.AspNetCore.Authentication; // <-- Add this using directive
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
using ConduitLLM.WebUI.Data;
using ConduitLLM.WebUI.Extensions;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Middleware;
using ConduitLLM.WebUI.Services;
using ConduitLLM.Providers.Extensions;
using ConduitLLM.Providers.Configuration;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.StaticWebAssets;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Get database provider configuration from environment variables
string dbProvider = Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "sqlite";
string? dbConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

// Default connection string for SQLite if not specified and SQLite is selected
if (string.IsNullOrEmpty(dbConnectionString) && dbProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
{
    dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=ConduitConfig.db";
}
else if (string.IsNullOrEmpty(dbConnectionString) && dbProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("DB_CONNECTION_STRING environment variable must be set when using PostgreSQL provider");
}

// Add the custom EF configuration provider
if (dbProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
{
    // Default to SQLite
    builder.Configuration.AddEntityFrameworkConfiguration(options => options.UseSqlite(dbConnectionString));
}
#if POSTGRES
// PostgreSQL configuration will be handled by the Dockerfile and runtime configuration
else if (dbProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
{
    // This will be set up at runtime with the proper dependencies
    builder.Configuration.AddEntityFrameworkConfiguration(options => {
        // This requires the Npgsql EF Core package to be referenced correctly
        options.UseNpgsql(dbConnectionString);
    });
}
#endif

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
    // Define policy that requires master key authentication
    options.AddPolicy("MasterKeyPolicy", policy =>
        policy.RequireClaim("MasterKeyAuthenticated", "true"));
    
    // Configure a fallback policy that allows anonymous access by default
    // This allows public pages like Login and AccessDenied to be accessed without authentication
    // Individual pages will use [Authorize] attribute as needed
    options.FallbackPolicy = null; // Allow anonymous access by default
});

// Add HttpContextAccessor - required for authentication in Razor components
builder.Services.AddHttpContextAccessor();

// Configure ConduitSettings to be bound from the application's configuration
builder.Services.Configure<ConduitSettings>(builder.Configuration.GetSection(nameof(ConduitSettings)));

// Configure DbContext Factory based on the provider
if (dbProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContextFactory<ConduitLLM.WebUI.Data.ConfigurationDbContext>(options =>
    {
        options.UseSqlite(dbConnectionString);
        if (builder.Environment.IsDevelopment())
        {
            // Suppress the pending model changes warning in development
            options.ConfigureWarnings(warnings => 
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }
    });
    
    builder.Services.AddDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>(options =>
    {
        options.UseSqlite(dbConnectionString);
        if (builder.Environment.IsDevelopment())
        {
            // Suppress the pending model changes warning in development
            options.ConfigureWarnings(warnings => 
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }
        
        // Configure entity types that need explicit configuration
        options.UseModel(new ConduitLLM.Configuration.ConfigurationDbContext(
            new DbContextOptionsBuilder<ConduitLLM.Configuration.ConfigurationDbContext>()
                .UseSqlite(dbConnectionString)
                .Options).Model);
    });
}
#if POSTGRES
else if (dbProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContextFactory<ConduitLLM.WebUI.Data.ConfigurationDbContext>(options =>
        options.UseNpgsql(dbConnectionString));
    builder.Services.AddDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>(options =>
        options.UseNpgsql(dbConnectionString));
}
#endif

// Register HttpClient for calling the API proxy
builder.Services.AddHttpClient();

// Register default HttpClient for Razor components with BaseAddress set to correct HTTP URI
builder.Services.AddHttpClient("", client =>
{
    client.BaseAddress = new Uri("https://localhost:5002/"); // Use your actual dev/prod base URL as needed
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

// Register HTTP retry configuration services
builder.Services.AddOptions<RetryOptions>()
    .Bind(builder.Configuration.GetSection(RetryOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddScoped<HttpRetryConfigurationService>();
builder.Services.AddTransient<IStartupFilter, HttpRetryConfigurationStartupFilter>();

// Register HTTP timeout configuration services
builder.Services.AddOptions<TimeoutOptions>()
    .Bind(builder.Configuration.GetSection(TimeoutOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddScoped<HttpTimeoutConfigurationService>();
builder.Services.AddTransient<IStartupFilter, HttpTimeoutConfigurationStartupFilter>();

// Register HttpClient with retry policies for LLM providers
builder.Services.AddLLMProviderHttpClients();

// Register Services
builder.Services.AddScoped<ConduitLLM.WebUI.Services.ProviderStatusService>();
builder.Services.AddScoped<ConduitLLM.WebUI.Services.ConfigurationChangeNotifier>();
builder.Services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, ConduitLLM.WebUI.Services.DatabaseSettingsStartupFilter>();
builder.Services.AddScoped<ConduitLLM.Configuration.IProviderCredentialService, ConduitLLM.Configuration.ProviderCredentialService>();

// Model costs tracking service
builder.Services.AddScoped<ConduitLLM.Configuration.Services.IModelCostService, ConduitLLM.Configuration.Services.ModelCostService>();
builder.Services.AddScoped<ConduitLLM.Configuration.IModelProviderMappingService, ConduitLLM.Configuration.ModelProviderMappingService>();

builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IVirtualKeyService, ConduitLLM.WebUI.Services.VirtualKeyService>();
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IGlobalSettingService, ConduitLLM.WebUI.Services.GlobalSettingService>(); 
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.ICacheStatusService, ConduitLLM.WebUI.Services.CacheStatusService>();
builder.Services.AddTransient<ConduitLLM.WebUI.Services.InitialSetupService>(); 
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.NotificationService>(); 
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.RequestLogService>();
builder.Services.AddScoped<ConduitLLM.WebUI.Services.ICostDashboardService, ConduitLLM.WebUI.Services.CostDashboardService>();

// Register Cache Metrics Service
builder.Services.AddSingleton<ICacheMetricsService, CacheMetricsService>();

// Register Cost Calculation Service
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.ICostCalculationService, ConduitLLM.Core.Services.CostCalculationService>();

// Add Conduit related services
builder.Services.AddSingleton<ConduitLLM.Core.ConduitRegistry>();

// Add Virtual Key maintenance background service
builder.Services.AddHostedService<ConduitLLM.WebUI.Services.VirtualKeyMaintenanceService>();

// Add Razor Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Controllers
builder.Services.AddControllers();

// Register context management services
builder.Services.AddConduitContextManagement(builder.Configuration);

var app = builder.Build();

// Print master key for debugging purposes
var masterKey = Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY");
Console.WriteLine("==============================================");
Console.WriteLine($"Access Key: {masterKey}");
Console.WriteLine("==============================================");

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
            var routerService = services.GetRequiredService<IRouterService>();
            // Initialize the router with the current configuration
            await routerService.InitializeRouterAsync();
            
            var logger = services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
            logger.LogInformation("LLM Router initialized successfully");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
        logger.LogError(ex, "An error occurred during router initialization.");
    }
}

// Apply migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<ConduitLLM.WebUI.Data.ConfigurationDbContext>>();
    using var dbContext = dbContextFactory.CreateDbContext();
    dbContext.Database.Migrate();
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

app.UseAntiforgery();

app.MapStaticAssets();

// Add authentication middleware before authorization
app.UseAuthentication();
app.UseAuthorization();

// Add Virtual Key Authentication and LLM Request Tracking middleware
app.UseVirtualKeyAuthentication();
app.UseLlmRequestTracking();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Ensure controllers are mapped for our API endpoints
app.MapControllers();

// --- Add Minimal API endpoint for Login ---
// Changed rememberMe to nullable bool (bool?)
app.MapPost("/account/login", async (HttpContext context, [FromForm] string masterKey, [FromForm] bool? rememberMe, [FromForm] string? returnUrl, ILogger<Program> logger) =>
{
    logger.LogInformation("POST /account/login received.");
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
