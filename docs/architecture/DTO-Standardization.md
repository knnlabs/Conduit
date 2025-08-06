# DTO Standardization Guide

## Overview

This document explains the DTO (Data Transfer Object) standardization approach used in the ConduitLLM application. As part of the architectural improvements to break circular dependencies between projects, DTOs have been centralized in the Configuration project to provide a clean separation of concerns.

## Known Issues and Next Steps

The DTO standardization work requires careful migration to avoid breaking existing code. Several issues have been identified that need to be addressed:

1. **Ambiguous DTO References**: There are cases where the same DTO class exists in both WebUI and Configuration projects, causing ambiguous reference errors. These should be resolved by:
   - Using fully qualified names in the short term (e.g., `ConduitLLM.Configuration.DTOs.LogsSummaryDto`)
   - Gradually replacing WebUI DTOs with Configuration DTOs
   - Adding clear XML documentation to highlight potential conflicts

2. **Interface Implementation Updates**: Some service adapters need to be updated to properly implement their interfaces with the new DTOs.

3. **Namespace Organization**: Consider reorganizing DTOs in the Configuration project into domain-specific subdirectories and namespaces to avoid conflicts.

4. **Project Reference Updates**: Ensure all projects reference the Configuration project correctly.

## DTO Location and Namespace

All DTOs should be defined in the `ConduitLLM.Configuration.DTOs` namespace, organized into appropriate subdirectories based on their domain. 

For example:
- `/ConduitLLM.Configuration/DTOs/VirtualKey/` - DTOs related to virtual keys
- `/ConduitLLM.Configuration/DTOs/IpFilter/` - DTOs related to IP filtering

## NO Backward Compatibility!

When moving DTOs from other projects (such as WebUI or Http) to the Configuration project, revise the consuming code to use the new DTOs. DO NOT allow backward compatibility to proliferate through the codebase. We do not want tech debt! 

## Naming Conventions

DTOs should follow these naming conventions:

1. Suffix all DTOs with `Dto` (e.g., `VirtualKeyDto`, `ModelCostDto`)
2. Use PascalCase for all property names
3. Use descriptive names that indicate the purpose of the DTO
4. Use request/response suffixes for DTOs that represent API requests or responses (e.g., `CreateVirtualKeyRequestDto`, `CreateVirtualKeyResponseDto`)

## Documentation Requirements

All DTOs must include:

1. XML documentation comments for the class and all properties
2. Clear descriptions of each property's purpose and valid values
3. Indication of required vs. optional properties
4. Notes about backward compatibility aliases where applicable

## DTO Categories

The ConduitLLM application uses several categories of DTOs:

### 1. Entity DTOs

These DTOs represent database entities and are used for data retrieval and persistence.

Example: `GlobalSettingDto`, `VirtualKeyDto`, `ModelCostDto`

### 2. Request/Response DTOs

These DTOs are specifically designed for API endpoints and represent the data sent to (request) or returned from (response) these endpoints.

Example: `CreateVirtualKeyRequestDto`, `CreateVirtualKeyResponseDto`

### 3. Specialized DTOs

These DTOs serve specific purposes within the application, such as providing dashboard data or statistics.

Example: `CostDashboardDto`, `DailyUsageStatsDto`, `LogsSummaryDto`

## Migration Strategy

When migrating existing DTOs from other projects to the Configuration project:

1. Create the DTO in the Configuration project with appropriate namespace
2. Add backward compatibility properties as needed
3. Update references in the consuming code to use the new namespace
4. Once all references are updated, consider removing the backward compatibility properties in a future release

## Examples of Standardized DTOs

### Cost Dashboard DTOs

The following cost-related DTOs have been standardized and moved to the Configuration project:

- `CostDashboardDto`: Provides summarized cost data for displaying on the dashboard
- `CostTrendDataDto`: Represents a single data point in cost trend charts
- `ModelCostDataDto`: Provides cost data broken down by model
- `DetailedCostDataDto`: Detailed cost data intended for export and detailed analysis
- `VirtualKeyCostDataDto`: Cost data broken down by virtual key

### IP Filtering DTOs

- `IpFilterDto`: Represents an IP filter rule
- `CreateIpFilterDto`: Used to create a new IP filter
- `UpdateIpFilterDto`: Used to update an existing IP filter

### Virtual Key DTOs

- `VirtualKeyDto`: Represents a virtual API key
- `CreateVirtualKeyRequestDto`: Used to create a new virtual key
- `CreateVirtualKeyResponseDto`: Returned when a new virtual key is created
- `UpdateVirtualKeyRequestDto`: Used to update an existing virtual key