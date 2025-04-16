using Microsoft.AspNetCore.Authorization;

namespace ConduitLLM.Http.Security
{
    /// <summary>
    /// Authorization requirement for master key
    /// </summary>
    public class MasterKeyRequirement : IAuthorizationRequirement
    {
        // Just a marker class
    }
}
