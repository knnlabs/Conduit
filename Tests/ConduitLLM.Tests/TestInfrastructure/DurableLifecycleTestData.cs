using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Tests.TestInfrastructure;

internal static class DurableLifecycleTestData
{
    public const int VirtualKeyGroupId = 8101;
    public const int VirtualKeyId = 8102;
    public const int FunctionConfigurationId = 8103;

    public static async Task SeedRequiredGraphAsync(ConduitDbContext context)
    {
        context.VirtualKeyGroups.Add(new VirtualKeyGroup
        {
            Id = VirtualKeyGroupId,
            GroupName = "Durable lifecycle test group"
        });
        context.VirtualKeys.Add(new VirtualKey
        {
            Id = VirtualKeyId,
            VirtualKeyGroupId = VirtualKeyGroupId,
            KeyName = "Durable lifecycle test key",
            KeyHash = $"durable-lifecycle-{Guid.NewGuid():N}",
            IsEnabled = true
        });
        context.FunctionConfigurations.Add(new FunctionConfiguration
        {
            Id = FunctionConfigurationId,
            ConfigurationName = "Durable lifecycle test function",
            ProviderType = FunctionProviderType.Exa,
            Purpose = FunctionPurpose.Search,
            DefaultExecutionMode = ExecutionMode.Asynchronous,
            IsEnabled = true
        });
        await context.SaveChangesAsync();
    }

    public static AsyncTask NewAsyncTask(
        string id,
        DateTime requestedAt,
        string type = "image_generation",
        int state = 0)
    {
        return new AsyncTask
        {
            Id = id,
            Type = type,
            State = state,
            VirtualKeyId = VirtualKeyId,
            CreatedAt = requestedAt,
            UpdatedAt = requestedAt,
            Payload = """{"prompt":"test"}"""
        };
    }

    public static FunctionExecution NewFunctionExecution(
        DateTime requestedAt,
        ExecutionState state = ExecutionState.Pending,
        Guid? id = null)
    {
        return new FunctionExecution
        {
            Id = id ?? Guid.NewGuid(),
            FunctionConfigurationId = FunctionConfigurationId,
            VirtualKeyId = VirtualKeyId,
            ExecutionMode = ExecutionMode.Asynchronous,
            State = state,
            RequestedAt = requestedAt,
            RequestJson = """{"query":"test"}"""
        };
    }
}
