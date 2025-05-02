using System;
using System.IO;
using ConduitLLM.Configuration;
using ConduitLLM.WebUI;
using Microsoft.AspNetCore.Authentication; 
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
using MudBlazor;
using MudBlazor.Services;

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

// Database configuration
var (dbProvider, dbConnectionString) = DbConnectionHelper.GetProviderAndConnectionString();
if (dbProvider == "sqlite")
{
    builder.Services.AddDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>(options =>
        options.UseSqlite(dbConnectionString));
}
else if (dbProvider == "postgres")
{
    builder.Services.AddDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>(options =>
        options.UseNpgsql(dbConnectionString));
}
else
{
    throw new InvalidOperationException($"Unsupported database provider: {dbProvider}. Supported values are 'sqlite' and 'postgres'.");
}

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

// Register MudBlazor services
builder.Services.AddMudServices(config => {
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 8000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
});

var app = builder.Build();

// Log database configuration ONCE, avoid duplicate logger declarations
var dbLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DbConnection");
DbConnectionHelper.GetProviderAndConnectionString(msg => dbLogger.LogInformation(msg));

// Check if using EnsureCreated mode
bool useEnsureCreated = Environment.GetEnvironmentVariable("CONDUIT_DATABASE_ENSURE_CREATED") == "true";

// Initialize the database FIRST - before any services try to use it
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>>();
    
    logger.LogInformation("Initializing database...");
    
    // Retry pattern to wait for database to be ready
    const int maxRetries = 20;
    bool connected = false;
    for (int retry = 0; retry < maxRetries; retry++)
    {
        try
        {
            // Initialize database context
            using var dbContext = dbContextFactory.CreateDbContext();
            
            // Just check if we can connect
            if (dbContext.Database.CanConnect())
            {
                logger.LogInformation("Connected to database. Checking pending migrations...");
                
                try {
                    // ALWAYS try to create tables even if migrations don't report as pending
                    logger.LogInformation("Ensuring database schema is created...");
                    dbContext.Database.EnsureCreated();
                    logger.LogInformation("Database schema created successfully");
                    
                    // Also try migrations just to be sure
                    var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();
                    if (pendingMigrations.Any())
                    {
                        logger.LogInformation("Found {Count} pending migrations: {Migrations}", 
                            pendingMigrations.Count, 
                            string.Join(", ", pendingMigrations));
                        
                        // Apply migrations
                        dbContext.Database.Migrate();
                        logger.LogInformation("Database migrations applied successfully");
                    }
                    else
                    {
                        logger.LogInformation("No pending migrations found");
                        
                        // Ensure the GlobalSettings table exists by checking for it explicitly
                        try {
                            var tableExists = false;
                            
                            if (dbProvider == "postgres")
                            {
                                // For PostgreSQL, check if the table exists in the public schema using raw SQL
                                try
                                {
                                    var command = dbContext.Database.GetDbConnection().CreateCommand();
                                    command.CommandText = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'GlobalSettings');";
                                    
                                    if (dbContext.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
                                    {
                                        dbContext.Database.GetDbConnection().Open();
                                    }
                                    
                                    var result = command.ExecuteScalar();
                                    tableExists = result != null && (result is bool boolResult ? boolResult : Convert.ToBoolean(result));
                                    
                                    logger.LogInformation("GlobalSettings table exists: {TableExists}", tableExists);
                                    
                                    if (!tableExists)
                                    {
                                        // Try to create the GlobalSettings table directly for Postgres
                                        logger.LogWarning("GlobalSettings table doesn't exist. Attempting to create it directly...");
                                        command = dbContext.Database.GetDbConnection().CreateCommand();
                                        command.CommandText = @"
                                            CREATE TABLE IF NOT EXISTS ""GlobalSettings"" (
                                                ""Id"" SERIAL PRIMARY KEY,
                                                ""Key"" VARCHAR(100) NOT NULL,
                                                ""Value"" VARCHAR(2000) NOT NULL,
                                                ""Description"" VARCHAR(500) NULL,
                                                ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                                ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                                            );
                                            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_GlobalSettings_Key"" ON ""GlobalSettings"" (""Key"");";
                                        command.ExecuteNonQuery();
                                        logger.LogInformation("GlobalSettings table created directly");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.LogError(ex, "Error checking if GlobalSettings table exists in PostgreSQL");
                                    tableExists = false;
                                }
                            }
                            else if (dbProvider == "sqlite")
                            {
                                // For SQLite, check if the table exists using raw SQL
                                try
                                {
                                    var command = dbContext.Database.GetDbConnection().CreateCommand();
                                    command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='GlobalSettings';";
                                    
                                    if (dbContext.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
                                    {
                                        dbContext.Database.GetDbConnection().Open();
                                    }
                                    
                                    var result = command.ExecuteScalar();
                                    tableExists = result != null && Convert.ToInt32(result) > 0;
                                }
                                catch (Exception ex)
                                {
                                    logger.LogError(ex, "Error checking if GlobalSettings table exists in SQLite");
                                    tableExists = false;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error checking for GlobalSettings table. Using EnsureCreated instead.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error applying migrations. Trying to ensure created as fallback...");
                    dbContext.Database.EnsureCreated();
                    logger.LogInformation("Database schema created successfully with EnsureCreated fallback");
                }
                
                connected = true;
                break;
            }
            
            logger.LogWarning("Cannot connect to database. Attempt {Retry}/{MaxRetries}. Retrying in 3 seconds...", 
                retry + 1, maxRetries);
                
            Thread.Sleep(3000);
        }
        catch (Exception ex) when (retry < maxRetries - 1)
        {
            logger.LogWarning(ex, "Database connection failed. Attempt {Retry}/{MaxRetries}. Retrying in 3 seconds...", 
                retry + 1, maxRetries);
                
            Thread.Sleep(3000);
        }
    }
    
    if (!connected)
    {
        logger.LogError("Failed to connect to the database after {MaxRetries} attempts. Starting application anyway, but functionality may be limited.", maxRetries);
    }
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
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
        logger.LogError(ex, "An error occurred during router initialization.");
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
