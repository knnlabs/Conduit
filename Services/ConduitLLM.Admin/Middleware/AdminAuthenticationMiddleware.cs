namespace ConduitLLM.Admin.Middleware;

/// <summary>
/// Middleware for handling Admin API authentication
/// </summary>
public class AdminAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AdminAuthenticationMiddleware> _logger;
    private readonly IConfiguration _configuration;
    private const string MASTER_KEY_CONFIG_KEY = "AdminApi:MasterKey";
    private const string MASTER_KEY_HEADER = "X-API-Key";

    /// <summary>
    /// Initializes a new instance of the AdminAuthenticationMiddleware class
    /// </summary>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <param name="logger">Logger</param>
    /// <param name="configuration">Application configuration</param>
    public AdminAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<AdminAuthenticationMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Processes the request
    /// </summary>
    /// <param name="context">The HTTP context</param>
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for Swagger endpoints
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        // Skip authentication for OPTIONS requests (CORS preflight)
        if (context.Request.Method == "OPTIONS")
        {
            await _next(context);
            return;
        }

        // Skip authentication for health check endpoint
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        // Check for CONDUIT_API_TO_API_BACKEND_AUTH_KEY first (new standard), then fall back to AdminApi:MasterKey
        string? masterKey = Environment.GetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY") 
                           ?? _configuration[MASTER_KEY_CONFIG_KEY];

        if (string.IsNullOrEmpty(masterKey))
        {
            _logger.LogWarning("Master key is not configured");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "API key authentication is not configured" });
            return;
        }

        if (!context.Request.Headers.TryGetValue(MASTER_KEY_HEADER, out var providedKey))
        {
_logger.LogWarning("No API key provided for {Path}", context.Request.Path.ToString().Replace(Environment.NewLine, ""));
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "API key is required" });
            return;
        }

        if (providedKey != masterKey)
        {
_logger.LogWarning("Invalid API key provided for {Path}", context.Request.Path.ToString().Replace(Environment.NewLine, ""));
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" });
            return;
        }

        await _next(context);
    }
}
