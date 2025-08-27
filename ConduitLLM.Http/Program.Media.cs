using ConduitLLM.Core.Extensions;
using ConduitLLM.Http.Services;
using ConduitLLM.Configuration.Options;

public partial class Program
{
    public static void ConfigureMediaServices(WebApplicationBuilder builder)
    {
        Console.WriteLine("[Conduit] ConfigureMediaServices - Using shared media configuration");
        
        // Use the shared media services configuration from ConduitLLM.Core
        builder.Services.AddMediaServices(builder.Configuration);

        // Configure media lifecycle options
        builder.Services.Configure<MediaLifecycleOptions>(
            builder.Configuration.GetSection(MediaLifecycleOptions.SectionName));

        // Add distributed media scheduler service for lifecycle management
        builder.Services.AddHostedService<DistributedMediaSchedulerService>();

        // Legacy: Media maintenance background service (will be removed after migration)
        // builder.Services.AddHostedService<MediaMaintenanceBackgroundService>();
        
        Console.WriteLine("[Conduit] Media lifecycle management configured:");
        
        var mediaOptions = builder.Configuration
            .GetSection(MediaLifecycleOptions.SectionName)
            .Get<MediaLifecycleOptions>() ?? new MediaLifecycleOptions();
        
        Console.WriteLine($"  - Scheduler Mode: {mediaOptions.SchedulerMode}");
        Console.WriteLine($"  - Dry Run Mode: {mediaOptions.DryRunMode}");
        Console.WriteLine($"  - Schedule Interval: {mediaOptions.ScheduleIntervalMinutes} minutes");
        Console.WriteLine($"  - Max Batch Size: {mediaOptions.MaxBatchSize} items");
        Console.WriteLine($"  - Monthly Delete Budget: {mediaOptions.MonthlyDeleteBudget:N0} operations");
        
        if (mediaOptions.TestVirtualKeyGroups.Any())
        {
            Console.WriteLine($"  - Test Groups: {string.Join(", ", mediaOptions.TestVirtualKeyGroups)}");
        }
    }
}