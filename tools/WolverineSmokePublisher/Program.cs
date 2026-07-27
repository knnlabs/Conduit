using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Messaging.Wolverine;
using ConduitLLM.Core.Data;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Messaging;

using JasperFx;
using JasperFx.CodeGeneration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;
using Wolverine.Postgresql;

var probeId = args.FirstOrDefault();
if (string.IsNullOrWhiteSpace(probeId))
{
    Console.Error.WriteLine("Usage: WolverineSmokePublisher <probe-id>");
    return 2;
}

var (_, connectionString) = new ConnectionStringManager()
    .GetProviderAndConnectionString("CoreAPI");

using var host = Host.CreateDefaultBuilder()
    .UseWolverine(options =>
    {
        options.ServiceName = "conduit-smoke-publisher";
        options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;
        options.Discovery.DisableConventionalDiscovery();
        options.Durability.Mode = DurabilityMode.Solo;

        options.UsePostgresqlPersistenceAndTransport(
            connectionString,
            "wolverine_conduit_smoke_publisher",
            transportSchema: WolverineMessagingExtensions.TransportSchemaName);
        options.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;
        options.ApplyConduitPublishRouting();
    })
    .Build();

await host.StartAsync();
try
{
    var bus = host.Services.GetRequiredService<IMessageBus>();

    await bus.PublishAsync(new SpendUpdateRequested
    {
        KeyId = int.MaxValue,
        Amount = 0.000001m,
        RequestId = $"static-codegen-spend-{probeId}",
        CorrelationId = probeId,
    });

    await bus.PublishAsync(new BatchSpendFlushRequestedEvent
    {
        RequestId = $"static-codegen-batch-{probeId}",
        RequestedBy = "Wolverine two-host smoke",
        Source = "CI",
        Reason = "Verify committed static handler delivery",
        IncludeStatistics = false,
    });

    await bus.PublishAsync(new ImageGenerationCancelled
    {
        TaskId = $"static-codegen-image-{probeId}",
        VirtualKeyId = int.MaxValue,
        Reason = "Static codegen delivery probe",
        CorrelationId = probeId,
    });

    await bus.PublishAsync(new VideoGenerationCancelled
    {
        RequestId = $"static-codegen-video-{probeId}",
        Reason = "Static codegen delivery probe",
        CorrelationId = probeId,
    });

    Console.WriteLine($"Published Wolverine delivery probes with id {probeId}.");
}
finally
{
    await host.StopAsync();
}

return 0;
