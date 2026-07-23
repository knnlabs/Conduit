using ConduitLLM.Functions.DTOs;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Utilities;

namespace ConduitLLM.Functions.Extensions;

/// <summary>
/// Maps persisted function executions to their canonical API resources.
/// </summary>
public static class FunctionExecutionMappingExtensions
{
    public static FunctionExecutionDto ToContractDto(this FunctionExecution entity) => new()
    {
        Id = entity.Id,
        FunctionId = entity.FunctionConfigurationId,
        Status = entity.State,
        Input = StructuredJson.ParseObject(entity.RequestJson),
        Output = StructuredJson.ParseObject(entity.ResponseJson),
        Error = entity.ErrorMessage,
        CreatedAt = entity.RequestedAt,
        StartedAt = entity.StartedAt,
        CompletedAt = entity.CompletedAt,
        DurationMs = entity.Duration is null
            ? null
            : Math.Max(
                0,
                (long)Math.Round(entity.Duration.Value.TotalMilliseconds, MidpointRounding.AwayFromZero)),
        Cost = new FunctionExecutionCostDto
        {
            Estimated = entity.EstimatedCost,
            Actual = entity.ActualCost,
            Breakdown = StructuredJson.ParseObject(entity.CostCalculationDetails)
        }
    };

    public static AdminFunctionExecutionDto ToAdminDto(this FunctionExecution entity)
    {
        var contract = entity.ToContractDto();
        return new AdminFunctionExecutionDto
        {
            Id = contract.Id,
            FunctionId = contract.FunctionId,
            Status = contract.Status,
            Input = contract.Input,
            Output = contract.Output,
            Error = contract.Error,
            CreatedAt = contract.CreatedAt,
            StartedAt = contract.StartedAt,
            CompletedAt = contract.CompletedAt,
            DurationMs = contract.DurationMs,
            Cost = contract.Cost,
            Admin = new FunctionExecutionAdminDetailsDto
            {
                VirtualKeyId = entity.VirtualKeyId,
                ExecutionMode = entity.ExecutionMode,
                RetryCount = entity.RetryCount,
                NextRetryAt = entity.NextRetryAt,
                LeasedBy = entity.LeasedBy,
                LeaseExpiresAt = entity.LeaseExpiryTime,
                Version = entity.Version,
                WebhookUrl = entity.WebhookUrl,
                WebhookDelivered = entity.WebhookDelivered,
                ProgressPercentage = entity.ProgressPercentage,
                StatusMessage = entity.StatusMessage
            }
        };
    }
}
