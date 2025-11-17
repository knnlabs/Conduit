using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.IntegrationTests.Core;

public class TestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public TestConfiguration Configuration { get; }
    
    public TestFixture()
    {
        Configuration = ConfigurationLoader.LoadMainConfig();
        
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        // Add services
        services.AddSingleton(Configuration);
        services.AddTransient<ConduitApiClient>();
        
        ServiceProvider = services.BuildServiceProvider();
    }
    
    public void Dispose()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}