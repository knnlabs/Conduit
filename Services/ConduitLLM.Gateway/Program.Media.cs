using ConduitLLM.Core.Extensions;

public partial class Program
{
    public static void ConfigureMediaServices(WebApplicationBuilder builder)
    {
        Console.WriteLine("[Conduit] ConfigureMediaServices - Using shared media configuration");

        // Use the shared media services configuration from ConduitLLM.Core
        // This provides IMediaStorageService for storing generated images/videos
        builder.Services.AddMediaServices(builder.Configuration);

        // Note: Media lifecycle management (cleanup scheduler, retention policies)
        // has been moved to Admin API. See ConduitLLM.Admin.Services.MediaCleanupSchedulerService
    }
}
