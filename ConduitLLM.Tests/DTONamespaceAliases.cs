// This file defines namespace aliases to help resolve ambiguous references
// between DTOs in the Configuration and WebUI projects.

// These aliases can be used in test files to disambiguate references
// Example: ConfigDTOs.VirtualKeyDto vs WebUIDTOs.VirtualKeyDto

global using ConfigDTOs = ConduitLLM.Configuration.DTOs;
global using WebUIDTOs = ConduitLLM.WebUI.DTOs;
global using ConfigServiceDtos = ConduitLLM.Configuration.Services.Dtos;
global using ConfigEntities = ConduitLLM.Configuration.Entities;