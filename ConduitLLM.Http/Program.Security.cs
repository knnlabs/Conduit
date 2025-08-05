using ConduitLLM.Http.Authentication;
using ConduitLLM.Http.Security;

public partial class Program
{
    public static void ConfigureSecurityServices(WebApplicationBuilder builder)
    {
        // Add CORS support for WebUI requests
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(
                        "http://localhost:5001",  // WebUI access
                        "http://webui:8080",      // Docker internal
                        "http://localhost:8080",  // Alternative local access
                        "http://127.0.0.1:5001"   // Alternative localhost format
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();  // Enable credentials for auth headers
            });
        });

        // Add Authentication and Authorization
        builder.Services.AddAuthentication("VirtualKey")
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, VirtualKeyAuthenticationHandler>(
                "VirtualKey", options => { })
            .AddScheme<BackendAuthenticationSchemeOptions, BackendAuthenticationHandler>(
                "Backend", options => { });

        builder.Services.AddAuthorization(options =>
        {
            // No default policy - let each controller specify its own requirements
            options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
            
            // Allow endpoints to opt out of authentication with [AllowAnonymous]
            options.FallbackPolicy = null;
            
            // Create a basic authentication-only policy for VirtualKey (same as default)
            options.AddPolicy("VirtualKeyAuthentication", policy =>
            {
                policy.AuthenticationSchemes.Add("VirtualKey");
                policy.RequireAuthenticatedUser();
                // No specific claims required - just authentication
            });
            
            // Add policy for SignalR hubs requiring virtual key authentication with claims
            options.AddPolicy("RequireVirtualKey", policy =>
            {
                policy.AuthenticationSchemes.Add("VirtualKey");
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("VirtualKeyId");
            });
            
            // Add policy for SignalR hubs requiring admin privileges
            options.AddPolicy("RequireAdminVirtualKey", policy =>
            {
                policy.AuthenticationSchemes.Add("VirtualKey");
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("VirtualKeyId");
                policy.RequireClaim("IsAdmin", "true");
            });
            
            // Add policy for admin-only access using backend authentication
            options.AddPolicy("AdminOnly", policy =>
            {
                policy.AuthenticationSchemes.Add("Backend");
                policy.RequireAuthenticatedUser();
            });
        });
    }
}