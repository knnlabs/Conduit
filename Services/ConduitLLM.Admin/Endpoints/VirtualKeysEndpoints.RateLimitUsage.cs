using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Services;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

public partial class VirtualKeysEndpoints
{
    /// <summary>
    /// Reports what a key is currently consuming against each of its ceilings.
    /// </summary>
    /// <remarks>
    /// The limits themselves live in the database, but what has been spent against them lives
    /// only in the shared window store — so "is this key near its limit right now?" is a
    /// question nothing else can answer. Reads only; it never admits or rejects anything.
    /// </remarks>
    public async Task<IResult> GetRateLimitUsage(
        int id,
        [FromServices] IVirtualKeyRepository keyRepository,
        [FromServices] IVirtualKeyGroupRepository groupRepository,
        [FromServices] IVirtualKeyRateLimitService? rateLimitService = null)
    {
        var key = await keyRepository.GetByIdAsync(id);
        if (key is null)
        {
            return AdminResults.NotFoundEntity("Virtual key", id);
        }

        var group = await groupRepository.GetByIdAsync(key.VirtualKeyGroupId);
        var groupHasLimits = group is not null &&
            (group.RateLimitRpm > 0 || group.RateLimitRpd > 0 ||
             group.RateLimitTpm > 0 || group.MaxParallelRequests > 0);

        var dto = new VirtualKeyRateLimitUsageDto
        {
            VirtualKeyId = key.Id,
            RateLimitRpm = key.RateLimitRpm,
            RateLimitRpd = key.RateLimitRpd,
            RateLimitTpm = key.RateLimitTpm,
            MaxParallelRequests = key.MaxParallelRequests,
            VirtualKeyGroupId = groupHasLimits ? key.VirtualKeyGroupId : null,
            GroupRateLimitRpm = group?.RateLimitRpm,
            GroupRateLimitRpd = group?.RateLimitRpd,
            GroupRateLimitTpm = group?.RateLimitTpm,
            GroupMaxParallelRequests = group?.MaxParallelRequests
        };

        if (rateLimitService is null)
        {
            // No shared store configured: report the ceilings but say plainly that the usage
            // figures are not measurements, rather than returning zeroes that look like idle.
            dto.Unavailable = true;
            return Ok(dto);
        }

        var usage = await rateLimitService.GetUsageAsync(
            key.KeyHash,
            groupHasLimits ? key.VirtualKeyGroupId : null);

        dto.RequestsThisMinute = usage.RequestsThisMinute;
        dto.RequestsToday = usage.RequestsToday;
        dto.TokensThisMinute = usage.TokensThisMinute;
        dto.RequestsInFlight = usage.RequestsInFlight;
        dto.GroupRequestsThisMinute = usage.GroupRequestsThisMinute;
        dto.GroupRequestsToday = usage.GroupRequestsToday;
        dto.GroupTokensThisMinute = usage.GroupTokensThisMinute;
        dto.GroupRequestsInFlight = usage.GroupRequestsInFlight;

        return Ok(dto);
    }
}
