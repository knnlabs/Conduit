using ConduitLLM.Core.Interfaces;
using ConduitLLM.Providers.Configuration;
using ConduitLLM.Providers.Http;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Providers.Extensions;

/// <summary>
/// Extension methods for registering LLM provider HttpClient instances with the resilience
/// pipeline (total timeout → retry → circuit breaker → per-attempt timeout).
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// Registers the named HttpClients that provider clients request at runtime (via
    /// <see cref="ProviderHttpClientNames"/>) with the provider resilience pipeline.
    /// </summary>
    /// <remarks>
    /// Three named clients are registered per provider type — chat (<c>*LLMClient</c>), auth
    /// verification (<c>*AuthVerification</c>) and video (<c>*VideoClient</c>) — each with the
    /// operation-class budget matching its role (see <see cref="ProviderResilienceOptions"/>).
    /// Circuit breaker state is partitioned per named client × request URI authority, so
    /// multiple Provider rows sharing an upstream host share failure signal, while
    /// OpenAI-compatible providers pointing at different hosts get independent breakers.
    /// </remarks>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddLLMProviderHttpClients(this IServiceCollection services)
    {
        // Idempotency guard: AddProviderServices calls this, but hosts historically called it
        // directly too. Registering the resilience handlers twice would stack pipelines.
        if (services.Any(d => d.ServiceType == typeof(ProviderHttpClientsMarker)))
        {
            return services;
        }
        services.AddSingleton<ProviderHttpClientsMarker>();

        services.AddOptions<ProviderResilienceOptions>()
            .BindConfiguration(ProviderResilienceOptions.SectionName)
            .ValidateDataAnnotations();

        foreach (var providerType in ProviderHttpClientNames.RegisteredTypes)
        {
            AddResilientNamedClient(services, ProviderHttpClientNames.Chat(providerType), ConduitHttpOptions.Chat);
            AddResilientNamedClient(services, ProviderHttpClientNames.Auth(providerType), ConduitHttpOptions.Auth);
            AddResilientNamedClient(services, ProviderHttpClientNames.Video(providerType), ConduitHttpOptions.Video);
        }

        return services;
    }

    private static void AddResilientNamedClient(
        IServiceCollection services, string clientName, string defaultOperationClass)
    {
        services.AddHttpClient(clientName)
            .AddResilienceHandler("conduit-provider", (builder, context) =>
            {
                var options = context.ServiceProvider
                    .GetRequiredService<IOptionsMonitor<ProviderResilienceOptions>>().CurrentValue;

                // Initialize the streaming idle watchdog from config. Set here (not at
                // registration time) because options need a built ServiceProvider; every
                // streaming response first passes through a named-client pipeline, so this runs
                // before the first watchdog-guarded read.
                ConduitLLM.Core.Utilities.StreamHelper.DefaultIdleReadTimeout =
                    TimeSpan.FromSeconds(options.Streaming.IdleReadTimeoutSeconds);

                var errorTracker = context.ServiceProvider.GetService<IProviderErrorTrackingService>();
                var hook = errorTracker == null
                    ? null
                    : new ProviderErrorTrackingRetryHook(
                        errorTracker,
                        context.ServiceProvider.GetService<IHttpContextAccessor>(),
                        context.ServiceProvider.GetService<ILoggerFactory>()
                            ?.CreateLogger("ConduitLLM.Providers.Http.ErrorTracking"));

                var logger = context.ServiceProvider.GetService<ILoggerFactory>()
                    ?.CreateLogger($"ConduitLLM.Providers.Http.Resilience.{clientName}");

                ProviderResiliencePipeline.Configure(builder, defaultOperationClass, options, hook, logger);
            })
            .SelectPipelineByAuthority();
    }

    private sealed class ProviderHttpClientsMarker
    {
    }
}
