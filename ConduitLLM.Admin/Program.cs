using ConduitLLM.Admin.Extensions;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core.Extensions;
using Microsoft.OpenApi.Models;
using System.Reflection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConduitLLM.Admin;

/// <summary>
/// Entry point for the Admin API application
/// </summary>
public class Program
{
    /// <summary>
    /// Application entry point that configures and starts the web application
    /// </summary>
    /// <param name="args">Command line arguments</param>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        
        // Add HttpClient factory for provider connection testing
        builder.Services.AddHttpClient();
        
        // Configure Swagger with XML comments
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo 
            { 
                Title = "ConduitLLM Admin API", 
                Version = "v1",
                Description = "Administrative API for ConduitLLM",
                Contact = new OpenApiContact
                {
                    Name = "ConduitLLM Team"
                }
            });

            // Add XML comments
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
            
            // Add security definition for API Key
            c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-API-Key",
                Description = "API Key Authentication"
            });

            // Add security requirement for API Key
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "ApiKey"
                        }
                    },
                    new string[] { }
                }
            });
        });

        // Add Core services
        builder.Services.AddCoreServices(builder.Configuration);
        
        // Add Configuration services
        builder.Services.AddConfigurationServices(builder.Configuration);
        
        // Add Admin services
        builder.Services.AddAdminServices(builder.Configuration);

        // Add health checks
        builder.Services.AddHealthChecks()
            .AddAudioProviderHealthChecks("audio");

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        // Add middleware for authentication and request tracking
        app.UseAdminMiddleware();

        app.UseAuthorization();

        app.MapControllers();
        
        // Map health checks
        app.MapHealthChecks("/health/ready");
        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/audio", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("audio")
        });

        app.Run();
    }
}