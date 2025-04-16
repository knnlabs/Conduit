using Microsoft.AspNetCore.Authorization;

namespace ConduitLLM.WebUI.Authorization;

public class MasterKeyRequirement : IAuthorizationRequirement
{
    // No specific properties needed for this simple requirement
    // The handler will contain the logic
}
