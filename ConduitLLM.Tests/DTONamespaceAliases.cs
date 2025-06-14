// This file defines namespace aliases to help resolve ambiguous references
// between DTOs in the Configuration and WebUI projects.

// These aliases can be used in test files to disambiguate references
// Example: ConfigDTOs.VirtualKeyDto vs WebUIDTOs.VirtualKeyDto

global using ConfigDTOs = ConduitLLM.Configuration.DTOs;
// ConfigServiceDtos is deprecated as the DTOs have been consolidated into ConduitLLM.Configuration.DTOs
global using ConfigEntities = ConduitLLM.Configuration.Entities;
global using WebUIDTOs = ConduitLLM.WebUI.DTOs;
