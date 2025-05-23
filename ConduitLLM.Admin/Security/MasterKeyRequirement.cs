using Microsoft.AspNetCore.Authorization;

namespace ConduitLLM.Admin.Security;

/// <summary>
/// Authorization requirement for master key authentication
/// </summary>
public class MasterKeyRequirement : IAuthorizationRequirement
{
}