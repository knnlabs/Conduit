using System.Reflection;

using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Messaging.Wolverine;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.OpenApi;
using ConduitLLM.Core.Services;

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
