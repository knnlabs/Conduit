using System.ComponentModel.DataAnnotations;
using System.Reflection;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Messaging.Wolverine;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.OpenApi;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Authentication;
using ConduitLLM.Gateway.Endpoints;
using ConduitLLM.Gateway.EventHandlers;
using ConduitLLM.Gateway.Extensions;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Persistence.Interfaces;
using ConduitLLM.Persistence.Npgsql;

using Microsoft.EntityFrameworkCore;
#if CONDUIT_NATIVE_AOT
using Microsoft.Extensions.DependencyInjection;
#endif

namespace ConduitLLM.Tests.Architecture;

/// <summary>
/// Guards the optional dependency seams used by trimmed and Native AOT hosts.
/// These assertions intentionally inspect emitted assembly references: a package
/// may exist in the repository without becoming part of a service's runtime graph.
/// </summary>
public sealed class AotDependencyBoundaryTests
{
    [Fact]
    public void ContractsRemainTransportAndPersistenceNeutral()
    {
        AssertDoesNotReference(
            typeof(IEventBus).Assembly,
            "ConduitLLM.Functions",
            "Microsoft.EntityFrameworkCore",
            "Wolverine",
            "Microsoft.AspNetCore.SignalR",
            "AWSSDK",
            "Amazon.",
            "Microsoft.OpenApi");
    }

    [Fact]
    public void CoreDoesNotOwnOptionalRuntimeAdapters()
    {
        AssertDoesNotReference(
            typeof(MediaLifecycleService).Assembly,
            "Wolverine",
            "Microsoft.AspNetCore.SignalR",
            "Microsoft.AspNetCore.SignalR.Protocols.MessagePack",
            "MessagePack",
            "AWSSDK",
            "Amazon.",
            "Microsoft.OpenApi",
            "Microsoft.ML.Tokenizers");
    }

    [Fact]
    public void PersistenceDoesNotOwnMessagingOrBuildTooling()
    {
        AssertDoesNotReference(
            typeof(ConduitLLM.Configuration.ConduitDbContext).Assembly,
            "Wolverine",
            "RabbitMQ.Client",
            "Microsoft.Build");
    }

    [Fact]
    public void PersistenceAbstractionsRemainBackendNeutral()
    {
        AssertDoesNotReference(
            typeof(IGlobalSettingRepository).Assembly,
            "ConduitLLM.Configuration",
            "ConduitLLM.Functions",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Wolverine",
            "Microsoft.AspNetCore.SignalR");
    }

    [Fact]
    public void TypedNpgsqlPersistenceDoesNotRootEfOrServiceImplementations()
    {
        AssertDoesNotReference(
            typeof(NpgsqlGlobalSettingRepository).Assembly,
            "ConduitLLM.Configuration",
            "ConduitLLM.Functions",
            "ConduitLLM.Admin",
            "ConduitLLM.Gateway",
            "Microsoft.EntityFrameworkCore",
            "Wolverine");
    }

#if !CONDUIT_NATIVE_AOT
    [Fact]
    public void JitGatewayDoesNotRootTypedNpgsqlRuntimeAdapter()
    {
        AssertDoesNotReference(
            Assembly.Load("ConduitLLM.Gateway"),
            "ConduitLLM.Persistence.Npgsql");
    }
#endif

#if CONDUIT_NATIVE_AOT
    [Fact]
    public void NativeGatewayReplacesEveryExtractedRuntimeRepository()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGlobalSettingRepository>(_ => null!);
        services.AddScoped<IIpFilterRepository>(_ => null!);
        services.AddScoped<IProviderRepository>(_ => null!);
        services.AddScoped<IProviderKeyCredentialRepository>(_ => null!);
        services.AddScoped<IVirtualKeyRuntimeStore>(_ => null!);

        services.UseNativeRuntimePersistence();

        AssertNativeSingleton<IGlobalSettingRepository, NpgsqlGlobalSettingRepository>(services);
        AssertNativeSingleton<IIpFilterRepository, NpgsqlIpFilterRepository>(services);
        AssertNativeSingleton<IProviderRepository, NpgsqlProviderRepository>(services);
        AssertNativeSingleton<IProviderKeyCredentialRepository, NpgsqlProviderKeyCredentialRepository>(services);
        AssertNativeSingleton<IVirtualKeyRuntimeStore, NpgsqlVirtualKeyRuntimeStore>(services);
    }
#endif

    [Fact]
    public void AdminDoesNotDirectlyOwnGatewayOnlyAdapters()
    {
        AssertDoesNotReference(
            typeof(ConduitLLM.Admin.Services.MediaCleanupStatusService).Assembly,
            "ConduitLLM.SignalR",
            "ConduitLLM.Tokenization",
            "Microsoft.AspNetCore.SignalR",
            "MessagePack",
            "Microsoft.ML.Tokenizers",
            "AWSSDK",
            "Amazon.");
    }

    [Fact]
    public void OptionalFeaturesAreOwnedByDedicatedAssemblies()
    {
        Assert.Equal("ConduitLLM.Messaging.Wolverine", typeof(WolverineEventBus).Assembly.GetName().Name);
        Assert.Equal("ConduitLLM.SignalR", typeof(SignalRConfigurationExtensions).Assembly.GetName().Name);
        Assert.Equal("ConduitLLM.OpenApi", typeof(OperationMetadataTransformer).Assembly.GetName().Name);
        Assert.Equal("ConduitLLM.Tokenization", typeof(TiktokenCounter).Assembly.GetName().Name);
        Assert.Equal("ConduitLLM.Media", typeof(S3MediaStorageService).Assembly.GetName().Name);
    }

    [Fact]
    public void WebServicesCannotReferenceSchemaMutationExecutable()
    {
        AssertDoesNotReference(typeof(ConduitLLM.Admin.Program).Assembly, "ConduitLLM.Migrator");
        AssertDoesNotReference(Assembly.Load("ConduitLLM.Gateway"), "ConduitLLM.Migrator");
        Assert.Equal("ConduitLLM.Migrator", typeof(ConduitLLM.Configuration.Data.SimpleMigrationService).Assembly.GetName().Name);
    }

    [Fact]
    public void PersistenceAbstractionsDoNotExposeQueryable()
    {
        var repositoryInterfaces = new[]
            {
                typeof(IProviderRepository).Assembly,
                typeof(IGlobalSettingRepository).Assembly
            }
            .Distinct()
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.IsInterface &&
                (type.Name.EndsWith("Repository", StringComparison.Ordinal) ||
                 type.Name.EndsWith("Store", StringComparison.Ordinal)));

        foreach (var repositoryInterface in repositoryInterfaces)
        {
            foreach (var method in repositoryInterface.GetMethods())
            {
                Assert.False(ContainsQueryable(method.ReturnType), $"{repositoryInterface.Name}.{method.Name} returns IQueryable");
                Assert.DoesNotContain(method.GetParameters(), parameter => ContainsQueryable(parameter.ParameterType));
            }
        }
    }

    [Fact]
    public void GatewayRequestPathUsesRuntimeVirtualKeyContract()
    {
        Assert.True(typeof(IVirtualKeyRuntimeService).IsAssignableFrom(typeof(IVirtualKeyService)));

        var runtimeMethods = typeof(IVirtualKeyRuntimeService).GetMethods()
            .Select(method => method.Name)
            .OrderBy(name => name)
            .ToArray();
        Assert.Equal(
            [
                nameof(IVirtualKeyRuntimeService.GetVirtualKeyInfoForValidationAsync),
                nameof(IVirtualKeyRuntimeService.UpdateSpendAsync),
                nameof(IVirtualKeyRuntimeService.ValidateVirtualKeyAsync),
                nameof(IVirtualKeyRuntimeService.ValidateVirtualKeyForAuthenticationAsync)
            ],
            runtimeMethods);

        var requestPathTypes = new[]
        {
            typeof(VirtualKeyAuthenticationHandler),
            typeof(VirtualKeySignalRAuthenticationHandler),
            typeof(VirtualKeyHubFilter),
            typeof(RequireBalanceEndpointFilter),
            typeof(DiscoveryEndpoints),
            typeof(UsageTrackingMiddleware),
            typeof(SpendUpdatedHandler)
        };

        foreach (var requestPathType in requestPathTypes)
        {
            var parameters = requestPathType.GetConstructors()
                .Cast<MethodBase>()
                .Concat(requestPathType.GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly))
                .SelectMany(method => method.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .ToArray();

            Assert.Contains(typeof(IVirtualKeyRuntimeService), parameters);
            Assert.DoesNotContain(typeof(IVirtualKeyService), parameters);
        }
    }

    [Fact]
    public void StoreBackedVirtualKeyRuntimeServiceUsesOnlyFixedShapePersistence()
    {
        var constructorParameters = typeof(StoreBackedVirtualKeyRuntimeService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(typeof(IVirtualKeyRuntimeStore), constructorParameters);
        Assert.DoesNotContain(typeof(IVirtualKeyRepository), constructorParameters);
        Assert.DoesNotContain(typeof(IVirtualKeyGroupRepository), constructorParameters);
        Assert.DoesNotContain(typeof(IVirtualKeySpendHistoryRepository), constructorParameters);
    }

    [Fact]
    public void CompiledSchemaVersionMatchesLatestMigration()
    {
        var options = new DbContextOptionsBuilder<ConduitLLM.Configuration.ConduitDbContext>()
            .UseNpgsql("Host=localhost;Database=schema_inventory;Username=unused;Password=unused")
            .Options;
        using var context = new ConduitLLM.Configuration.ConduitDbContext(options);

        Assert.Equal(ConduitLLM.Configuration.Data.ConduitSchemaVersion.Current, context.Database.GetMigrations().Last());
    }

    [Fact]
    public void ProductionAssembliesDoNotUseReflectionBasedMaxLengthValidation()
    {
        var assemblyNames = new[]
        {
            "ConduitLLM.Admin",
            "ConduitLLM.Configuration",
            "ConduitLLM.Core",
            "ConduitLLM.Functions",
            "ConduitLLM.Gateway",
            "ConduitLLM.Providers",
            "ConduitLLM.Security"
        };
        var offenders = assemblyNames
            .Select(Assembly.Load)
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetMembers(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(member => member.CustomAttributes.Any(attribute =>
                attribute.AttributeType == typeof(MaxLengthAttribute)))
            .Select(member => $"{member.DeclaringType?.FullName}.{member.Name}")
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void StringLengthValidationPreservesEfMaximumLengthMetadata()
    {
        var options = new DbContextOptionsBuilder<ConduitLLM.Configuration.ConduitDbContext>()
            .UseNpgsql("Host=localhost;Database=schema_inventory;Username=unused;Password=unused")
            .Options;
        using var context = new ConduitLLM.Configuration.ConduitDbContext(options);

        var keyName = context.Model.FindEntityType(typeof(VirtualKey))!
            .FindProperty(nameof(VirtualKey.KeyName));

        Assert.NotNull(keyName);
        Assert.Equal(100, keyName.GetMaxLength());
    }

    private static bool ContainsQueryable(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>))
        {
            return true;
        }

        return type.HasElementType && ContainsQueryable(type.GetElementType()!)
            || type.IsGenericType && type.GetGenericArguments().Any(ContainsQueryable);
    }

#if CONDUIT_NATIVE_AOT
    private static void AssertNativeSingleton<TService, TImplementation>(IServiceCollection services)
    {
        var descriptor = Assert.Single(services, value => value.ServiceType == typeof(TService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
    }
#endif

    private static void AssertDoesNotReference(Assembly assembly, params string[] forbiddenPrefixes)
    {
        var references = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        foreach (var forbiddenPrefix in forbiddenPrefixes)
        {
            Assert.DoesNotContain(
                references,
                reference => reference.StartsWith(forbiddenPrefix, StringComparison.OrdinalIgnoreCase));
        }
    }
}
