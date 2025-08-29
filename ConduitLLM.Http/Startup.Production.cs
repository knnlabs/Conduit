using System.Threading.RateLimiting;

using ConduitLLM.Http.Extensions;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// using Serilog; // Commented out - Serilog package not installed

namespace ConduitLLM.Http
{
    /// <summary>
    /// Production startup configuration for Conduit Audio Services.
    /// </summary>
    public class ProductionStartup
    {
        public ProductionStartup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Add core MVC services
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            // Add CORS for production domains
            services.AddCors(options =>
            {
                options.AddPolicy("ProductionCors", builder =>
                {
                    var allowedOrigins = Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "*" };
                    builder
                        .WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });

            // Add authentication and authorization
            services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    options.Authority = Configuration["Auth:Authority"];
                    options.TokenValidationParameters = new()
                    {
                        ValidateAudience = true,
                        ValidAudience = Configuration["Auth:Audience"]
                    };
                });
            services.AddAuthorization();

            // Add rate limiting
            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.User?.Identity?.Name ?? context.Request.Headers.Host.ToString(),
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = Configuration.GetValue<int>("RateLimiting:RequestsPerMinute", 1000),
                            Window = TimeSpan.FromMinutes(1)
                        }));
            });

            // Add response compression
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });

            // Audio services removed - system no longer supports audio functionality

            // Health checks are registered in Program.cs via AddConduitHealthChecks
            // Audio-specific health checks should be added there as well to avoid duplicate registrations

            // Add distributed tracing
            services.AddOpenTelemetry()
                .WithTracing(builder =>
                {
                    builder
                        .SetResourceBuilder(ResourceBuilder.CreateDefault()
                            .AddService("ConduitAudioService"))
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddSqlClientInstrumentation()
                        .AddRedisInstrumentation()
                        .AddOtlpExporter(options =>
                        {
                            var endpoint = Configuration["Telemetry:OtlpEndpoint"] ?? "http://localhost:4317";
                            options.Endpoint = new Uri(endpoint);
                        });
                });

            // Add application insights
            services.AddApplicationInsightsTelemetry(options =>
            {
                options.ConnectionString = Configuration["ApplicationInsights:ConnectionString"];
            });

            // Configure Serilog - commented out until Serilog package is installed
            // Log.Logger = new LoggerConfiguration()
            //     .ReadFrom.Configuration(Configuration)
            //     .Enrich.FromLogContext()
            //     .Enrich.WithMachineName()
            //     .Enrich.WithEnvironmentName()
            //     .CreateLogger();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime)
        {
            if (!env.IsProduction())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            // Security headers
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Append("X-Frame-Options", "DENY");
                context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
                context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
                await next();
            });

            app.UseHttpsRedirection();
            app.UseResponseCompression();
            
            app.UseRouting();
            app.UseCors("ProductionCors");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                
                // Map admin endpoints with authorization
                endpoints.MapGet("/admin/connection-pool/stats", async context =>
                {
                    // Implementation for admin endpoints
                    await context.Response.WriteAsync("Connection pool statistics");
                }).RequireAuthorization("AdminPolicy");
            });

            // Log application startup
            lifetime.ApplicationStarted.Register(() =>
            {
                // Log.Information("Conduit Audio Service started successfully in {Environment} environment", 
                //     env.EnvironmentName);
                Console.WriteLine($"Conduit Audio Service started successfully in {env.EnvironmentName} environment");
            });

            // Log application stopping
            lifetime.ApplicationStopping.Register(() =>
            {
                // Log.Information("Conduit Audio Service is shutting down");
                Console.WriteLine("Conduit Audio Service is shutting down");
            });
        }
    }
}