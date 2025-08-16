using System.Net; // For HttpStatusCode
using System.Text.Json;
using System.Text.Json.Serialization; // Required for JsonNamingPolicy

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Data; // Added for database initialization
using ConduitLLM.Configuration.Extensions; // Added for DataProtectionExtensions and HealthCheckExtensions
using ConduitLLM.Configuration.Repositories; // Added for repository interfaces
using ConduitLLM.Core;
using ConduitLLM.Core.Exceptions; // Add namespace for custom exceptions
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces; // Added for IVirtualKeyCache
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Routing;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Authentication; // Added for VirtualKeyAuthenticationHandler
using ConduitLLM.Http.Controllers; // Added for RealtimeController
using ConduitLLM.Http.Extensions; // Added for AudioServiceExtensions
using ConduitLLM.Http.Middleware; // Added for Security middleware extensions
using ConduitLLM.Http.Security;
using ConduitLLM.Http.Services; // Added for ApiVirtualKeyService, RedisVirtualKeyCache, CachedApiVirtualKeyService
using ConduitLLM.Providers; // Assuming LLMClientFactory is here
using ConduitLLM.Providers.Extensions; // Add namespace for HttpClient extensions
// DatabaseAwareLLMClientFactory now in Providers namespace
using ConduitLLM.Configuration.DTOs.SignalR; // Added for NotificationBatchingOptions

using MassTransit; // Added for event bus infrastructure

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore; // Added for EF Core
using Microsoft.EntityFrameworkCore.Diagnostics; // Added for warning suppression
using Microsoft.Extensions.Options; // Added for IOptions
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql.EntityFrameworkCore.PostgreSQL; // Added for PostgreSQL
using StackExchange.Redis; // Added for Redis-based task service
using Polly;
using Polly.Extensions.Http;
using Prometheus; // Added for Prometheus metrics
using OpenTelemetry.Metrics; // Added for OpenTelemetry metrics
using OpenTelemetry.Resources; // Added for ResourceBuilder extensions

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
});

// Configure basic settings and environment
Program.ConfigureBasicSettings(builder);

// Configure all service registrations
Program.ConfigureCoreServices(builder);
Program.ConfigureSecurityServices(builder);
Program.ConfigureCachingServices(builder);
Program.ConfigureMessagingServices(builder);
Program.ConfigureSignalRServices(builder);
Program.ConfigureMediaServices(builder);
Program.ConfigureMonitoringServices(builder);

var app = builder.Build();

// Configure middleware pipeline
await Program.ConfigureMiddleware(app);

// Configure endpoints
Program.ConfigureEndpoints(app);

Console.WriteLine("[Conduit] All endpoints configured, starting application...");
app.Run();

// Make Program class accessible for testing
public partial class Program { }