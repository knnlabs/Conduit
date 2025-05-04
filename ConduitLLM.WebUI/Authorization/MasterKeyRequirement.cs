using Microsoft.AspNetCore.Authorization;

namespace ConduitLLM.WebUI.Authorization;

/// <summary>
/// Represents an authorization requirement that validates access using a master key.
/// </summary>
/// <remarks>
/// <para>
/// This requirement is used as part of the ASP.NET Core authorization system to restrict
/// access to administrative routes and actions. It works with <see cref="MasterKeyAuthorizationHandler"/>
/// to verify that the caller has provided a valid master key in their request.
/// </para>
/// <para>
/// The requirement itself is a marker class with no properties or methods. All validation logic
/// is implemented in the corresponding authorization handler.
/// </para>
/// <para>
/// Usage example:
/// <code>
/// services.AddAuthorization(options =>
/// {
///     options.AddPolicy("RequireMasterKey", policy =>
///         policy.Requirements.Add(new MasterKeyRequirement()));
/// });
/// </code>
/// </para>
/// </remarks>
public class MasterKeyRequirement : IAuthorizationRequirement
{
    // No specific properties needed for this simple requirement
    // The handler will contain the logic
}
