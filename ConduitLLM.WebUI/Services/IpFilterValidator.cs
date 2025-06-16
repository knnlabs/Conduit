using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.Entities;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service for validating IP filter rules
/// </summary>
public class IpFilterValidator
{
    private readonly ILogger<IpFilterValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IpFilterValidator"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    public IpFilterValidator(ILogger<IpFilterValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates an IP filter for proper format and checks for conflicts with existing filters
    /// </summary>
    /// <param name="filter">The filter to validate</param>
    /// <param name="existingFilters">Collection of existing filters to check for conflicts</param>
    /// <param name="isUpdate">Whether this is an update to an existing filter</param>
    /// <returns>A tuple with validation result and optional error message</returns>
    public (bool IsValid, string? ErrorMessage) ValidateFilter(
        CreateIpFilterDto filter,
        IEnumerable<IpFilterEntity> existingFilters,
        bool isUpdate = false,
        int? filterIdForUpdate = null)
    {
        // Validate filter type
        if (filter.FilterType != IpFilterConstants.WHITELIST &&
            filter.FilterType != IpFilterConstants.BLACKLIST)
        {
            return (false, $"Invalid filter type: {filter.FilterType}. Must be 'whitelist' or 'blacklist'.");
        }

        // Validate IP address or CIDR
        if (string.IsNullOrWhiteSpace(filter.IpAddressOrCidr))
        {
            return (false, "IP address or CIDR subnet cannot be empty.");
        }

        // If it's a plain IP address
        if (!filter.IpAddressOrCidr.Contains('/'))
        {
            if (!IpAddressValidator.IsValidIpAddress(filter.IpAddressOrCidr))
            {
                return (false, $"Invalid IP address format: {filter.IpAddressOrCidr}");
            }
        }
        // If it's a CIDR notation
        else
        {
            if (!IpAddressValidator.IsValidCidr(filter.IpAddressOrCidr))
            {
                return (false, $"Invalid CIDR notation: {filter.IpAddressOrCidr}");
            }
        }

        // Check for duplicates (same IP/CIDR and filter type)
        if (existingFilters != null && existingFilters.Any())
        {
            // For updates, exclude the filter being updated from duplicate check
            var filtersToCheck = existingFilters;
            int filterId = 0;

            if (isUpdate)
            {
                // First check if we have a direct ID passed
                if (filterIdForUpdate.HasValue && filterIdForUpdate.Value > 0)
                {
                    filterId = filterIdForUpdate.Value;
                }
                else
                {
                    // Try to get the ID using reflection
                    var idProperty = filter.GetType().GetProperty("Id");
                    if (idProperty != null)
                    {
                        var idValue = idProperty.GetValue(filter, null);
                        if (idValue != null)
                        {
                            filterId = Convert.ToInt32(idValue);
                        }
                    }
                }

                // If we have a valid ID, filter out the entity with that ID
                if (filterId > 0)
                {
                    filtersToCheck = existingFilters.Where(f => f.Id != filterId).ToList();
                }
            }

            var duplicate = filtersToCheck.FirstOrDefault(f =>
                f.FilterType.Equals(filter.FilterType, StringComparison.OrdinalIgnoreCase) &&
                f.IpAddressOrCidr.Equals(filter.IpAddressOrCidr, StringComparison.OrdinalIgnoreCase));

            if (duplicate != null)
            {
                return (false, $"A {filter.FilterType} rule already exists for {filter.IpAddressOrCidr}.");
            }
        }

        // Check for logical conflicts (same IP/CIDR with different filter types)
        if (existingFilters != null && existingFilters.Any())
        {
            // For updates, exclude the filter being updated from conflict check
            var filtersToCheck = existingFilters;
            int filterId = 0;

            if (isUpdate)
            {
                // First check if we have a direct ID passed
                if (filterIdForUpdate.HasValue && filterIdForUpdate.Value > 0)
                {
                    filterId = filterIdForUpdate.Value;
                }
                else
                {
                    // Try to get the ID using reflection
                    var idProperty = filter.GetType().GetProperty("Id");
                    if (idProperty != null)
                    {
                        var idValue = idProperty.GetValue(filter, null);
                        if (idValue != null)
                        {
                            filterId = Convert.ToInt32(idValue);
                        }
                    }
                }

                // If we have a valid ID, filter out the entity with that ID
                if (filterId > 0)
                {
                    filtersToCheck = existingFilters.Where(f => f.Id != filterId).ToList();
                }
            }

            var conflict = filtersToCheck.FirstOrDefault(f =>
                !f.FilterType.Equals(filter.FilterType, StringComparison.OrdinalIgnoreCase) &&
                f.IpAddressOrCidr.Equals(filter.IpAddressOrCidr, StringComparison.OrdinalIgnoreCase));

            if (conflict != null)
            {
                return (false, $"Conflict detected: {filter.IpAddressOrCidr} already exists as a {conflict.FilterType} rule.");
            }

            // Check for subnet conflicts
            if (filter.IpAddressOrCidr.Contains('/'))
            {
                foreach (var existing in filtersToCheck)
                {
                    // Skip if filter types are the same (not a conflict)
                    if (existing.FilterType.Equals(filter.FilterType, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Check if existing is a CIDR that overlaps with the new one
                    if (existing.IpAddressOrCidr.Contains('/'))
                    {
                        if (HaveCidrOverlap(filter.IpAddressOrCidr, existing.IpAddressOrCidr))
                        {
                            return (false, $"Conflict detected: {filter.IpAddressOrCidr} ({filter.FilterType}) overlaps with existing {existing.IpAddressOrCidr} ({existing.FilterType}).");
                        }
                    }
                    // Check if single IP existing is contained in new CIDR
                    else if (IpAddressValidator.IsIpInCidrRange(existing.IpAddressOrCidr, filter.IpAddressOrCidr))
                    {
                        return (false, $"Conflict detected: {existing.IpAddressOrCidr} ({existing.FilterType}) is contained within {filter.IpAddressOrCidr} ({filter.FilterType}).");
                    }
                }
            }
            // If adding a single IP, check if it's contained in any existing CIDR with different type
            else
            {
                foreach (var existing in filtersToCheck.Where(f => f.IpAddressOrCidr.Contains('/')))
                {
                    // Skip if filter types are the same (not a conflict)
                    if (existing.FilterType.Equals(filter.FilterType, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (IpAddressValidator.IsIpInCidrRange(filter.IpAddressOrCidr, existing.IpAddressOrCidr))
                    {
                        return (false, $"Conflict detected: {filter.IpAddressOrCidr} ({filter.FilterType}) is contained within existing {existing.IpAddressOrCidr} ({existing.FilterType}).");
                    }
                }
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Checks if two CIDR ranges have any overlap
    /// </summary>
    /// <param name="cidr1">First CIDR range</param>
    /// <param name="cidr2">Second CIDR range</param>
    /// <returns>True if the ranges overlap, false otherwise</returns>
    private bool HaveCidrOverlap(string cidr1, string cidr2)
    {
        // Simple implementation - check if either network address is contained in the other range
        var parts1 = cidr1.Split('/');
        var parts2 = cidr2.Split('/');

        var networkAddress1 = parts1[0];
        var networkAddress2 = parts2[0];

        return IpAddressValidator.IsIpInCidrRange(networkAddress1, cidr2) ||
               IpAddressValidator.IsIpInCidrRange(networkAddress2, cidr1);
    }
}
