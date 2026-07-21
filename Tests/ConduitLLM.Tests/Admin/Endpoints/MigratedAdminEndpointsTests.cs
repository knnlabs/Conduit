using System.Net;
using System.Net.Http.Json;

using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.DTOs.VirtualKey;

using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace ConduitLLM.Tests.Admin.Endpoints;

public sealed class MigratedAdminEndpointsTests
{
    [Fact]
    public async Task VirtualKeyValidation_RemainsAnonymous_WhileManagementRequiresMasterKey()
    {
        var service = new Mock<IAdminVirtualKeyService>();
        service.Setup(x => x.ValidateVirtualKeyAsync("condt_test", null))
            .ReturnsAsync(new VirtualKeyValidationResult { IsValid = true, VirtualKeyId = 7 });

        using var host = AdminEndpointTestHost.Create(services =>
        {
            services.AddHttpContextAccessor();
            services.AddSingleton(service.Object);
            services.AddScoped<VirtualKeysEndpoints>();
        }, endpoints => VirtualKeysEndpoints.MapVirtualKeysEndpoints(endpoints));
        host.Client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var validation = await host.Client.PostAsJsonAsync("/api/VirtualKeys/validate",
            new ValidateVirtualKeyRequest { Key = "condt_test" });
        var management = await host.Client.GetAsync("/api/VirtualKeys");

        Assert.Equal(HttpStatusCode.OK, validation.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, management.StatusCode);
    }

    [Fact]
    public async Task IpFilter_OnlyCheckRouteIsAnonymous()
    {
        var service = new Mock<IAdminIpFilterService>();
        service.Setup(x => x.CheckIpAddressAsync("127.0.0.1"))
            .ReturnsAsync(new IpCheckResult { IsAllowed = true });

        using var host = AdminEndpointTestHost.Create(services =>
        {
            services.AddHttpContextAccessor();
            services.AddSingleton(service.Object);
            services.AddScoped<IpFilterEndpoints>();
        }, endpoints => IpFilterEndpoints.MapIpFilterEndpoints(endpoints));
        host.Client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var check = await host.Client.GetAsync("/api/IpFilter/check/127.0.0.1");
        var settings = await host.Client.GetAsync("/api/IpFilter/settings");

        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, settings.StatusCode);
    }
}
