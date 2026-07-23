using System.Reflection;

using ConduitLLM.Security.Models;
using ConduitLLM.Security.Services;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace ConduitLLM.Tests.Security;

public sealed class SecurityEventMonitoringCleanupTests
{
    [Fact]
    public void PerformCleanup_RemovesStaleAndOrphanedAnomalyStates()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var service = new SecurityEventMonitoringService(
            cache,
            Mock.Of<ILogger<SecurityEventMonitoringService>>(),
            Options.Create(new SecurityMonitoringOptions { DataRetentionHours = 1 }),
            Mock.Of<IServiceScopeFactory>());
        var serviceType = typeof(SecurityEventMonitoringService);
        var securityAssembly = serviceType.Assembly;
        var ipProfileType = securityAssembly.GetType("ConduitLLM.Security.Models.IpActivityProfile")!;
        var anomalyStateType = securityAssembly.GetType("ConduitLLM.Security.Models.AnomalyDetectionState")!;
        var ipProfiles = GetField(service, "_ipProfiles");
        var anomalyStates = GetField(service, "_anomalyStates");

        Add(ipProfiles, "198.51.100.1", Create(ipProfileType,
            ("IpAddress", "198.51.100.1"), ("LastActivity", DateTime.UtcNow)));
        Add(anomalyStates, "ip:198.51.100.1", Create(anomalyStateType,
            ("Identifier", "ip:198.51.100.1"), ("WindowStart", DateTime.UtcNow)));
        Add(anomalyStates, "ip:198.51.100.2", Create(anomalyStateType,
            ("Identifier", "ip:198.51.100.2"), ("WindowStart", DateTime.UtcNow)));
        Add(anomalyStates, "ip:198.51.100.3", Create(anomalyStateType,
            ("Identifier", "ip:198.51.100.3"), ("WindowStart", DateTime.UtcNow.AddHours(-2))));

        serviceType.GetMethod("PerformCleanup", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(service, [null]);

        Assert.True(ContainsKey(anomalyStates, "ip:198.51.100.1"));
        Assert.False(ContainsKey(anomalyStates, "ip:198.51.100.2"));
        Assert.False(ContainsKey(anomalyStates, "ip:198.51.100.3"));
    }

    private static object GetField(object instance, string name) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;

    private static object Create(Type type, params (string Name, object Value)[] properties)
    {
        var instance = Activator.CreateInstance(type, nonPublic: true)!;
        foreach (var (name, value) in properties)
        {
            type.GetProperty(name)!.SetValue(instance, value);
        }
        return instance;
    }

    private static void Add(object dictionary, string key, object value) =>
        dictionary.GetType().GetMethod("TryAdd")!.Invoke(dictionary, [key, value]);

    private static bool ContainsKey(object dictionary, string key) =>
        (bool)dictionary.GetType().GetMethod("ContainsKey")!.Invoke(dictionary, [key])!;
}
