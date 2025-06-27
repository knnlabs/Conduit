using System;
using Microsoft.AspNetCore.Authorization;

namespace ConduitLLM.Http.Authentication
{
    /// <summary>
    /// Standardized authorization attribute for SignalR hubs requiring virtual key authentication.
    /// This attribute ensures consistent authorization requirements across all secure hubs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class VirtualKeyHubAuthorizationAttribute : AuthorizeAttribute
    {
        /// <summary>
        /// Initializes a new instance of the VirtualKeyHubAuthorizationAttribute class.
        /// </summary>
        public VirtualKeyHubAuthorizationAttribute()
        {
            // Set the authentication scheme to ensure virtual key authentication is used
            AuthenticationSchemes = "VirtualKey";
            
            // Set a policy that requires the virtual key claim
            Policy = "RequireVirtualKey";
        }
    }
    
    /// <summary>
    /// Authorization attribute for SignalR hubs that require admin privileges.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class AdminHubAuthorizationAttribute : AuthorizeAttribute
    {
        /// <summary>
        /// Initializes a new instance of the AdminHubAuthorizationAttribute class.
        /// </summary>
        public AdminHubAuthorizationAttribute()
        {
            // Set the authentication scheme to ensure virtual key authentication is used
            AuthenticationSchemes = "VirtualKey";
            
            // Set a policy that requires admin privileges
            Policy = "RequireAdminVirtualKey";
        }
    }
}