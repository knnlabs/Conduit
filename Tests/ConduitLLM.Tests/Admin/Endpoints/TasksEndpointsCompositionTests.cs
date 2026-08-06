using ConduitLLM.Core.Interfaces;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConduitLLM.Tests.Admin.Endpoints;

[Collection("AdminCompositionEnvironment")]
public sealed class TasksEndpointsCompositionTests
{
    [Fact]
    public void AdminCompositionRoot_ResolvesAsyncTaskService()
    {
        var originalDatabaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        try
        {
            Environment.SetEnvironmentVariable(
                "DATABASE_URL",
                "postgresql://conduit:conduit@localhost:5432/conduit_tests");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(ConduitLLM.Admin.Program).Assembly.GetName().Name,
                EnvironmentName = "Testing"
            });
            ConduitLLM.Admin.Program.ConfigureCoreServices(builder, NullLogger.Instance);

            using var provider = builder.Services.BuildServiceProvider(validateScopes: true);
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAsyncTaskService>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", originalDatabaseUrl);
        }
    }
}

[CollectionDefinition("AdminCompositionEnvironment", DisableParallelization = true)]
public sealed class AdminCompositionEnvironmentCollection;
