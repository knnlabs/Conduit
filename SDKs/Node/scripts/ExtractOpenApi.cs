using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Swashbuckle.AspNetCore.Swagger;

// Simple program to extract OpenAPI specs without running the full application
public class Program
{
    public static async Task Main(string[] args)
    {
        var basePath = Path.GetFullPath("../../..");
        
        // Extract Core API spec
        Console.WriteLine("Extracting Core API OpenAPI spec...");
        await ExtractOpenApiSpec<ConduitLLM.Http.Program>(
            Path.Combine(basePath, "ConduitLLM.Http/openapi-generated.json"),
            "v1"
        );
        
        // Extract Admin API spec
        Console.WriteLine("Extracting Admin API OpenAPI spec...");
        await ExtractOpenApiSpec<ConduitLLM.Admin.Program>(
            Path.Combine(basePath, "ConduitLLM.Admin/openapi-generated.json"),
            "v1"
        );
        
        Console.WriteLine("✅ OpenAPI specs extracted successfully!");
    }
    
    static async Task ExtractOpenApiSpec<TProgram>(string outputPath, string documentName) where TProgram : class
    {
        var factory = new WebApplicationFactory<TProgram>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    // Override problematic services for extraction only
                    services.AddSingleton<IHostedService, NoOpHostedService>();
                });
            });
        
        using (var scope = factory.Services.CreateScope())
        {
            var swaggerProvider = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>();
            var swagger = swaggerProvider.GetSwagger(documentName);
            
            var json = swagger.SerializeAsJson(Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0);
            await File.WriteAllTextAsync(outputPath, json);
        }
    }
    
    class NoOpHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}