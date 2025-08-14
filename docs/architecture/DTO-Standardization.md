# DTO Standardization Guide

## Overview

This document explains the DTO (Data Transfer Object) standardization approach used in the ConduitLLM application. As part of the architectural improvements to break circular dependencies between projects, DTOs have been centralized in the Configuration project to provide a clean separation of concerns.

## ✅ Implementation Status: COMPLETED

The DTO standardization has been successfully completed with the following achievements:

1. **✅ Centralized Location**: All 136+ DTOs are now properly located in the `ConduitLLM.Configuration.DTOs` namespace
2. **✅ Domain Organization**: DTOs are organized into domain-specific subdirectories for clean separation:
   - `Audio/` - Audio-related DTOs (6 DTOs)
   - `BatchOperations/` - Batch operation DTOs (8 DTOs)
   - `Cache/` - Cache management DTOs (15 DTOs)
   - `Costs/` - Cost tracking DTOs (5 DTOs)
   - `HealthMonitoring/` - Health monitoring DTOs (13 DTOs)
   - `Metrics/` - System metrics DTOs (20 DTOs)
   - `Security/` - Security-related DTOs (6 DTOs)
   - `VirtualKey/` - Virtual key management DTOs (11 DTOs)
   - Plus additional domain-specific folders

3. **✅ Eliminated Duplicates**: Removed all duplicate DTOs from other projects
4. **✅ Zero Technical Debt**: No backward compatibility properties or legacy aliases maintained
5. **✅ Clean Build**: Solution builds with 0 warnings and 0 errors
6. **✅ Proper References**: All projects correctly reference the Configuration project

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

## ✅ Completed Migration

The migration of all DTOs to the Configuration project has been completed using the following approach:

1. ✅ **Created standardized DTOs** in the Configuration project with domain-specific namespaces
2. ✅ **Eliminated backward compatibility** - No legacy properties or aliases maintained 
3. ✅ **Updated all references** - All consuming code now uses the new standardized namespaces
4. ✅ **Removed original DTOs** - Cleaned up all duplicate and embedded DTOs from other projects
5. ✅ **Verified functionality** - All builds pass and tests are green

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