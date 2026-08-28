#if CONDUIT_NATIVE_AOT
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Persistence.Interfaces;
using ConduitLLM.Persistence.Npgsql;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConduitLLM.Gateway.Extensions;

/// <summary>
/// Selects fixed-shape persistence implementations for extracted native Gateway slices.
/// </summary>
public static class NativeRuntimePersistenceExtensions
{
    /// <summary>
    /// Replaces EF reference repositories only after the normal registration graph has
    /// been assembled. Unextracted contracts intentionally remain on EF and observable
    /// through the native feature matrix.
    /// </summary>
    public static IServiceCollection UseNativeRuntimePersistence(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IGlobalSettingRepository, NpgsqlGlobalSettingRepository>());
        services.Replace(ServiceDescriptor.Singleton<IIpFilterRepository, NpgsqlIpFilterRepository>());
        services.Replace(ServiceDescriptor.Singleton<IProviderRepository, NpgsqlProviderRepository>());
        services.Replace(ServiceDescriptor.Singleton<IProviderKeyCredentialRepository, NpgsqlProviderKeyCredentialRepository>());
        services.Replace(ServiceDescriptor.Singleton<IVirtualKeyRuntimeStore, NpgsqlVirtualKeyRuntimeStore>());
        return services;
    }
}
#endif
