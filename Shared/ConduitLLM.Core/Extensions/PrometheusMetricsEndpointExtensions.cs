using ConduitLLM.Core.Utilities;

using Microsoft.AspNetCore.Builder;

using Prometheus;

namespace ConduitLLM.Core.Extensions;

/// <summary>
/// Configures the shared Prometheus scrape endpoint used by Conduit services.
/// </summary>
public static class PrometheusMetricsEndpointExtensions
{
    private const string MetricsPath = "/metrics";

    /// <summary>
    /// Exposes prometheus-net's default registry to private-network or authenticated requests.
    /// The default registry also contains instruments bridged from the .NET Meter API.
    /// </summary>
    public static IApplicationBuilder UseConduitPrometheusMetricsEndpoint(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseWhen(
            context => context.Request.Path == MetricsPath &&
                       (IpAddressHelper.IsPrivateNetworkRequest(context) ||
                        context.User.Identity?.IsAuthenticated == true),
            metricsApp => metricsApp.UseMetricServer(MetricsPath));

        return app;
    }
}
