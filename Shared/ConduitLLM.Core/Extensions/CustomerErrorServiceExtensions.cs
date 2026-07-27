using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Extensions;

/// <summary>
/// Registration for customer-facing provider-error translation.
/// Behavior is governed by CONDUIT_CUSTOMER_MODE — see <see cref="CustomerErrorMode"/>.
/// </summary>
public static class CustomerErrorServiceExtensions
{
    public static IServiceCollection AddCustomerErrorTranslation(this IServiceCollection services)
    {
        services.AddSingleton(sp => CustomerErrorOptions.FromEnvironment(
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("Conduit.CustomerErrorMode")));
        services.AddSingleton<IProviderErrorTranslator, ProviderErrorTranslator>();
        return services;
    }
}
