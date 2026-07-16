using ConduitLLM.Core.Extensions;

public partial class Program
{
    public static void ConfigureMediaServices(WebApplicationBuilder builder)
    {
        // Use the shared media services configuration from ConduitLLM.Core
        // This provides IMediaStorageService for storing generated images/videos
        builder.Services.AddMediaServices(builder.Configuration);

    }
}
