using Microsoft.AspNetCore.Authorization;

namespace ConduitLLM.Admin.Security;

/// <summary>
/// Authorizes identities validated by the master-key authentication scheme.
/// </summary>
public class MasterKeyAuthorizationHandler : AuthorizationHandler<MasterKeyRequirement>
{
    /// <summary>
    /// Handles the authorization requirement.
    /// </summary>
    /// <param name="context">The authorization context</param>
    /// <param name="requirement">The requirement to be validated</param>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MasterKeyRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim("MasterKey", "true"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
