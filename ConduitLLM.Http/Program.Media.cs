using ConduitLLM.Core.Extensions;
using ConduitLLM.Http.Services;

public partial class Program
{
    public static void ConfigureMediaServices(WebApplicationBuilder builder)
    {
        Console.WriteLine("[Conduit] ConfigureMediaServices - Using shared media configuration");
        
        // Use the shared media services configuration from ConduitLLM.Core
        builder.Services.AddMediaServices(builder.Configuration);

        // Add media maintenance background service (Core API specific)
        builder.Services.AddHostedService<MediaMaintenanceBackgroundService>();
    }
}