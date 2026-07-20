using ConduitLLM.Admin.Filters;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.ModelCatalogs;
using ConduitLLM.Core.Events;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>Administrative operations for the provider catalog bundled with this release.</summary>
[ApiController]
[Route("api/Model/bundled-catalog")]
[Authorize(Policy = "MasterKeyPolicy")]
[ServiceFilter(typeof(OperationLoggingFilter))]
public sealed class BundledModelCatalogController : AdminControllerBase
{
    private readonly IBundledModelCatalogImporter _importer;
    private readonly IEventBus _eventBus;

    public BundledModelCatalogController(
        IBundledModelCatalogImporter importer,
        IEventBus eventBus,
        ILogger<BundledModelCatalogController> logger)
        : base(eventBus, logger)
    {
        _importer = importer;
        _eventBus = eventBus;
    }

    /// <summary>Merges all bundled provider models while preserving every matched database record.</summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(BundledModelCatalogImportResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<BundledModelCatalogImportResult>> Import(
        CancellationToken cancellationToken)
    {
        var result = await _importer.ImportAsync(
            onlyWhenIdentifierCatalogIsEmpty: false,
            cancellationToken)
            ?? throw new InvalidOperationException("A forced bundled catalog import unexpectedly returned no result.");

        if (result.Created.Identifiers > 0)
        {
            await _eventBus.PublishAsync(new DiscoveryCacheInvalidationRequested
            {
                Reason = "Bundled provider model catalog imported",
                RequestedBy = "Admin User",
                CorrelationId = Guid.NewGuid().ToString()
            });
        }

        LogAdminAudit(
            "Imported",
            "BundledModelCatalog",
            detail: $"Providers: {result.ProvidersProcessed}, identifiers created: {result.Created.Identifiers}, " +
                     $"skipped: {result.SkippedExistingIdentifiers}, conflicts: {result.Conflicts.Count}");
        return Ok(result);
    }
}
