using ConduitLLM.WebUI.Components;
using ConduitLLM.WebUI.Data;
using ConduitLLM.WebUI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ConduitLLM.WebUI.Middleware;
using ConduitLLM.Core;
using ConduitLLM.WebUI.Authorization;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Routing;
using ConduitLLM.WebUI.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.AspNetCore.StaticWebAssets;
using ConduitLLM.Core.Caching;

var builder = WebApplication.CreateBuilder(args);

// Add the custom EF configuration provider
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=ConduitConfig.db";
builder.Configuration.AddEntityFrameworkConfiguration(options => options.UseSqlite(connectionString));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure ConduitSettings to be bound from the application's configuration
builder.Services.Configure<ConduitSettings>(builder.Configuration.GetSection(nameof(ConduitSettings)));

// Configure SQLite DbContext Factory
builder.Services.AddDbContextFactory<ConfigurationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register HttpClient for calling the API proxy
builder.Services.AddHttpClient();

// Register Cache Service
builder.Services.AddCacheService(builder.Configuration);

// Register LLM Caching Components
builder.Services.AddLLMCaching();

// Configure router options
builder.Services.Configure<RouterOptions>(
    builder.Configuration.GetSection(RouterOptions.SectionName));

// Register Router services using the extension method
builder.Services.AddRouterServices(builder.Configuration);

// Register Services
builder.Services.AddScoped<ConduitLLM.WebUI.Services.ProviderStatusService>();
builder.Services.AddScoped<ConduitLLM.WebUI.Services.ConfigurationChangeNotifier>();
builder.Services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, ConduitLLM.WebUI.Services.DatabaseSettingsStartupFilter>();
builder.Services.AddScoped<ConduitLLM.Configuration.IProviderCredentialService, ConduitLLM.Configuration.ProviderCredentialService>();
builder.Services.AddScoped<ConduitLLM.Configuration.IModelProviderMappingService, ConduitLLM.Configuration.ModelProviderMappingService>();
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IVirtualKeyService, ConduitLLM.WebUI.Services.VirtualKeyService>();
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.IGlobalSettingService, ConduitLLM.WebUI.Services.GlobalSettingService>(); 
builder.Services.AddScoped<ConduitLLM.WebUI.Interfaces.ICacheStatusService, ConduitLLM.WebUI.Services.CacheStatusService>();
builder.Services.AddTransient<ConduitLLM.WebUI.Services.InitialSetupService>(); 
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.NotificationService>(); 
builder.Services.AddSingleton<ConduitLLM.WebUI.Services.RequestLogService>();

// Register Cache Metrics Service
builder.Services.AddSingleton<ICacheMetricsService, CacheMetricsService>();

// Required for accessing HttpContext in handlers/services
builder.Services.AddHttpContextAccessor(); 

// Add Authorization services and the custom handler
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, ConduitLLM.WebUI.Authorization.MasterKeyAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("MasterKeyPolicy", policy =>
        policy.Requirements.Add(new ConduitLLM.WebUI.Authorization.MasterKeyRequirement()));
});

// Add Conduit related services
builder.Services.AddSingleton<ConduitLLM.Core.ConduitRegistry>();

// Add Virtual Key maintenance background service
builder.Services.AddHostedService<ConduitLLM.WebUI.Services.VirtualKeyMaintenanceService>();

builder.Services.AddControllers();

var app = builder.Build();

// Initialize Master Key using InitialSetupService
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var initialSetupService = services.GetRequiredService<ConduitLLM.WebUI.Services.InitialSetupService>();
        await initialSetupService.EnsureMasterKeyExistsAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
        logger.LogError(ex, "An error occurred during application initialization.");
    }
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
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<ConfigurationDbContext>>();
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
app.UseAuthorization();

// Add Virtual Key Authentication and LLM Request Tracking middleware
app.UseVirtualKeyAuthentication();
app.UseLlmRequestTracking();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();
