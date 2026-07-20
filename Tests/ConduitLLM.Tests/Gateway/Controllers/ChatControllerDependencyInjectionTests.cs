using System.Text.Json;

using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Gateway.Controllers;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Gateway.Controllers;

[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
public class ChatControllerDependencyInjectionTests
{
    [Fact]
    public void ContextManagementRegistrations_AllowChatControllerActivation()
    {
        var builder = WebApplication.CreateBuilder();
        Program.ConfigureContextManagementServices(builder);

        // The production Gateway registers these elsewhere. Supplying test doubles keeps
        // this regression test focused on the context-management dependency graph.
        builder.Services.Replace(ServiceDescriptor.Scoped(
            _ => Mock.Of<IModelCapabilityService>()));
        builder.Services.AddSingleton(new Conduit(
            Mock.Of<ILLMClientFactory>(),
            Mock.Of<ILogger<Conduit>>()));
        builder.Services.AddSingleton(Mock.Of<IModelProviderMappingService>());
        builder.Services.AddSingleton(new JsonSerializerOptions());
        builder.Services.AddSingleton(Mock.Of<IEventBus>());
        builder.Services.AddSingleton(Mock.Of<IGlobalSettingsCacheService>());
        builder.Services.AddScoped<ChatController>();

        using var provider = builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        var controller = scope.ServiceProvider.GetRequiredService<ChatController>();

        Assert.NotNull(controller);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUsageEstimationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IImageTokenCalculator>());
    }
}
