using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Security;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

public static class FunctionCredentialsEndpoints
{
    public static IEndpointRouteBuilder MapFunctionCredentialsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/FunctionCredentials")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("FunctionCredentials");

        group.MapGet("/", List).WithName("FunctionCredentials_List").Produces<List<FunctionCredential>>();
        group.MapGet("/configuration/{functionConfigurationId:int}", GetByConfiguration)
            .WithName("FunctionCredentials_GetByConfiguration").Produces<List<FunctionCredential>>()
            .Produces<AdminProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");
        group.MapGet("/{id:int}", GetById).WithName("FunctionCredentials_GetById")
            .Produces<FunctionCredential>().Produces<AdminProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");
        group.MapPost("/", Create).WithName("FunctionCredentials_Create")
            .Produces<FunctionCredential>(StatusCodes.Status201Created)
            .Produces<AdminProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json");
        group.MapPut("/{id:int}", Update).WithName("FunctionCredentials_Update")
            .Produces<FunctionCredential>().Produces<AdminProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<AdminProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");
        group.MapDelete("/{id:int}", Delete).WithName("FunctionCredentials_Delete")
            .Produces(StatusCodes.Status204NoContent).Produces<AdminProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");
        group.MapPost("/test", Test).WithName("FunctionCredentials_Test")
            .Produces<FunctionCredentialTestResultDto>()
            .Produces<AdminProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json");
        return app;
    }

    private static async Task<IResult> List([FromServices] IFunctionCredentialRepository repository) =>
        Results.Ok(await repository.GetAllUnboundedAsync());

    private static async Task<IResult> GetByConfiguration(
        int functionConfigurationId,
        [FromServices] IFunctionCredentialRepository credentials,
        [FromServices] IFunctionConfigurationRepository configurations)
    {
        var configuration = await configurations.GetByIdAsync(functionConfigurationId)
            ?? throw new KeyNotFoundException();
        return Results.Ok(await credentials.GetByProviderTypeAsync(configuration.ProviderType));
    }

    private static async Task<IResult> GetById(int id, [FromServices] IFunctionCredentialRepository repository)
    {
        var credential = await repository.GetByIdAsync(id);
        return credential is null
            ? AdminResults.NotFoundEntity("Function credential", id)
            : Results.Ok(credential);
    }

    private static async Task<IResult> Create(
        [FromBody] FunctionCredential credential,
        [FromServices] IFunctionCredentialRepository repository,
        [FromServices] IFunctionCredentialProtector protector,
        HttpContext httpContext,
        ILoggerFactory loggerFactory)
    {
        EncryptScopedSecret(credential, protector);
        var id = await repository.CreateAsync(credential);
        var created = await repository.GetByIdAsync(id);
        Audit(httpContext, loggerFactory, "Created", id, credential);
        return Results.Created($"/api/FunctionCredentials/{id}", created);
    }

    private static async Task<IResult> Update(
        int id,
        [FromBody] FunctionCredential credential,
        [FromServices] IFunctionCredentialRepository repository,
        [FromServices] IFunctionCredentialProtector protector,
        HttpContext httpContext,
        ILoggerFactory loggerFactory)
    {
        if (id != credential.Id)
        {
            return AdminResults.BadRequest("ID mismatch");
        }
        EncryptScopedSecret(credential, protector);
        await repository.UpdateAsync(credential);
        var updated = await repository.GetByIdAsync(id) ?? throw new KeyNotFoundException();
        Audit(httpContext, loggerFactory, "Updated", id, credential);
        return Results.Ok(updated);
    }

    private static async Task<IResult> Delete(
        int id,
        [FromServices] IFunctionCredentialRepository repository,
        HttpContext httpContext,
        ILoggerFactory loggerFactory)
    {
        var credential = await repository.GetByIdAsync(id);
        await repository.DeleteAsync(id);
        if (credential is not null)
        {
            Audit(httpContext, loggerFactory, "Deleted", id, credential);
        }
        else
        {
            AdminAudit.Log(httpContext, Logger(loggerFactory), "Deleted", "FunctionCredential", id);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> Test(
        [FromBody] TestFunctionCredentialRequest testRequest,
        [FromServices] IFunctionCredentialRepository credentials,
        [FromServices] IFunctionConfigurationRepository configurations,
        [FromServices] IFunctionClientFactory clientFactory,
        [FromServices] IFunctionCredentialProtector protector)
    {
        var credential = await credentials.GetByIdAsync(testRequest.CredentialId)
            ?? throw new KeyNotFoundException();

        // Prefer the credential's own configuration (config-scoped/MCP); fall back to any config of
        // the provider type (provider-global Exa/Tavily).
        var configuration = (credential.FunctionConfigurationId is int scopedConfigId
                ? await configurations.GetByIdAsync(scopedConfigId)
                : null)
            ?? (await configurations.GetByProviderTypeAsync(credential.ProviderType)).FirstOrDefault()
            ?? throw new KeyNotFoundException();

        var client = await clientFactory.GetClientAsync(credential.ProviderType, configuration.Id);
        var apiKey = testRequest.ApiKeyOverride ?? protector.Reveal(credential.ApiKey);
        var result = await client.VerifyAuthenticationAsync(apiKey);
        return Results.Ok(new
        {
            success = result.IsSuccess,
            message = result.Message,
            details = result.Details,
            durationMs = result.ResponseTimeMs
        });
    }

    /// <summary>
    /// Encrypts the secret of a config-scoped credential (e.g. an MCP server token) at rest.
    /// Provider-global credentials (Exa/Tavily) are left as-is pending a separate encryption pass.
    /// <see cref="IFunctionCredentialProtector.Protect"/> is idempotent, so re-saving an already
    /// encrypted value does not double-encrypt.
    /// </summary>
    private static void EncryptScopedSecret(FunctionCredential credential, IFunctionCredentialProtector protector)
    {
        if (credential.FunctionConfigurationId.HasValue)
        {
            credential.ApiKey = protector.Protect(credential.ApiKey);
        }
    }

    private static void Audit(
        HttpContext context,
        ILoggerFactory loggerFactory,
        string operation,
        int id,
        FunctionCredential credential) =>
        AdminAudit.Log(context, Logger(loggerFactory), operation, "FunctionCredential", id,
            $"ProviderType: {credential.ProviderType}");

    private static ILogger Logger(ILoggerFactory factory) =>
        factory.CreateLogger("ConduitLLM.Admin.Endpoints.FunctionCredentials");
}

public sealed class TestFunctionCredentialRequest
{
    public int CredentialId { get; set; }
    public string? ApiKeyOverride { get; set; }
}
