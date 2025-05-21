#!/bin/bash
# Script to find direct database access code in the WebUI project
# This script will help identify code that needs to be removed during the legacy mode removal phase

# Color definitions
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
NC='\033[0m' # No Color

echo -e "${BLUE}=== Direct Database Access Audit Tool ===${NC}"
echo "This tool identifies direct database access code in the WebUI project."
echo ""

OUTPUT_FILE="db-access-audit.txt"
WEB_UI_PATH="ConduitLLM.WebUI"
CONFIG_PATH="ConduitLLM.Configuration"

echo -e "Running audit and saving results to ${OUTPUT_FILE}..."
echo "" > ${OUTPUT_FILE}

echo -e "${BLUE}=== Direct Database Access Audit ===${NC}" >> ${OUTPUT_FILE}
echo "Generated on $(date)" >> ${OUTPUT_FILE}
echo "" >> ${OUTPUT_FILE}

# Check for DbContext and EF Core usage
echo -e "${YELLOW}Checking for EntityFrameworkCore usage...${NC}"
echo -e "${MAGENTA}## EntityFrameworkCore Usage${NC}" >> ${OUTPUT_FILE}
echo -e "Files containing EntityFrameworkCore references:\n" >> ${OUTPUT_FILE}
grep -r "Microsoft.EntityFrameworkCore" --include="*.cs" ${WEB_UI_PATH} | tee -a ${OUTPUT_FILE}
echo -e "\n\n" >> ${OUTPUT_FILE}

# Check for DbContext injection
echo -e "${YELLOW}Checking for DbContext usage...${NC}"
echo -e "${MAGENTA}## DbContext Usage${NC}" >> ${OUTPUT_FILE}
echo -e "Files containing ConfigurationDbContext references:\n" >> ${OUTPUT_FILE}
grep -r "ConfigurationDbContext" --include="*.cs" ${WEB_UI_PATH} | tee -a ${OUTPUT_FILE}
echo -e "\n\n" >> ${OUTPUT_FILE}

# Check for repository interfaces usage
echo -e "${YELLOW}Checking for repository interface usage...${NC}"
echo -e "${MAGENTA}## Repository Interface Usage${NC}" >> ${OUTPUT_FILE}
echo -e "Files containing repository interface references:\n" >> ${OUTPUT_FILE}
grep -r "I[A-Z][a-z]*Repository" --include="*.cs" ${WEB_UI_PATH} | tee -a ${OUTPUT_FILE}
echo -e "\n\n" >> ${OUTPUT_FILE}

# Check for database-specific operations
echo -e "${YELLOW}Checking for database operations...${NC}"
echo -e "${MAGENTA}## Database Operations${NC}" >> ${OUTPUT_FILE}
echo -e "Files containing database operations:\n" >> ${OUTPUT_FILE}
grep -r -E "(Add\(|Remove\(|SaveChanges|Where\(|FirstOrDefault|ToList\(\))" --include="*.cs" ${WEB_UI_PATH} | grep "DbSet\|Context" | tee -a ${OUTPUT_FILE}
echo -e "\n\n" >> ${OUTPUT_FILE}

# Check for Obsolete attribute (these should be removed)
echo -e "${YELLOW}Checking for obsolete classes...${NC}"
echo -e "${MAGENTA}## Obsolete Classes${NC}" >> ${OUTPUT_FILE}
echo -e "Files marked with Obsolete attribute:\n" >> ${OUTPUT_FILE}
grep -r "\[Obsolete" --include="*.cs" ${WEB_UI_PATH} | tee -a ${OUTPUT_FILE}
echo -e "\n\n" >> ${OUTPUT_FILE}

# Check for direct database registration
echo -e "${YELLOW}Checking for database context registration...${NC}"
echo -e "${MAGENTA}## Database Registration${NC}" >> ${OUTPUT_FILE}
echo -e "Files containing database context registration:\n" >> ${OUTPUT_FILE}
grep -r "AddDbContext" --include="*.cs" ${WEB_UI_PATH} | tee -a ${OUTPUT_FILE}
echo -e "\n\n" >> ${OUTPUT_FILE}

# Check for specific services with database access
echo -e "${YELLOW}Checking known services with database access...${NC}"
echo -e "${MAGENTA}## Known Services With Database Access${NC}" >> ${OUTPUT_FILE}
echo -e "Checking for specific service implementations:\n" >> ${OUTPUT_FILE}

# Define services to check
services=(
    "VirtualKeyService"
    "GlobalSettingService"
    "IpFilterService"
    "RequestLogService"
    "CostDashboardService"
    "RouterService"
    "ModelCostService"
    "ModelProviderMappingService"
    "ProviderCredentialService"
    "ProviderHealthService"
    "DatabaseBackupService"
    "DbRouterConfigRepository"
)

# Check for each service
for service in "${services[@]}"; do
    if [ -f "${WEB_UI_PATH}/Services/${service}.cs" ]; then
        echo -e "${RED}Found ${service}.cs${NC}" | tee -a ${OUTPUT_FILE}
    else
        echo -e "${GREEN}${service}.cs not found (already removed)${NC}" | tee -a ${OUTPUT_FILE}
    fi
done
echo -e "\n\n" >> ${OUTPUT_FILE}

# Check for feature flag usage
echo -e "${YELLOW}Checking for legacy mode feature flags...${NC}"
echo -e "${MAGENTA}## Legacy Mode Feature Flags${NC}" >> ${OUTPUT_FILE}
echo -e "Files containing legacy mode feature flags:\n" >> ${OUTPUT_FILE}
grep -r "CONDUIT_USE_ADMIN_API\|CONDUIT_DISABLE_DIRECT_DB_ACCESS" --include="*.cs" ${WEB_UI_PATH} | tee -a ${OUTPUT_FILE}
echo -e "\n\n" >> ${OUTPUT_FILE}

echo -e "${GREEN}Audit completed. Results saved to ${OUTPUT_FILE}${NC}"
echo -e "Review the findings to plan the removal of direct database access code."

# Count the number of files with direct database access
total_db_files=$(grep -l "Microsoft.EntityFrameworkCore\|ConfigurationDbContext\|I[A-Z][a-z]*Repository\|\[Obsolete" --include="*.cs" ${WEB_UI_PATH} | sort -u | wc -l)
echo -e "${YELLOW}Found ${total_db_files} files with potential direct database access.${NC}"

echo -e "${BLUE}=== Removal Recommendations ===${NC}"

# Check if deprecation warning is still present
if [ -f "${WEB_UI_PATH}/Components/Shared/DeprecationWarning.razor" ]; then
    echo -e "${RED}• Remove DeprecationWarning.razor component${NC}"
else
    echo -e "${GREEN}✓ DeprecationWarning.razor component already removed${NC}"
fi

# Check if database context registration is still present
if [ -f "${WEB_UI_PATH}/Extensions/DbContextRegistrationExtensions.cs" ]; then
    echo -e "${RED}• Remove DbContextRegistrationExtensions.cs${NC}"
else
    echo -e "${GREEN}✓ DbContextRegistrationExtensions.cs already removed${NC}"
fi

# Check if repository extensions are still present
if [ -f "${WEB_UI_PATH}/Extensions/RepositoryServiceExtensions.cs" ]; then
    echo -e "${RED}• Remove RepositoryServiceExtensions.cs${NC}"
else
    echo -e "${GREEN}✓ RepositoryServiceExtensions.cs already removed${NC}"
fi

# Check for entity framework packages in csproj
if grep -q "EntityFrameworkCore" "${WEB_UI_PATH}/ConduitLLM.WebUI.csproj"; then
    echo -e "${RED}• Remove EntityFrameworkCore packages from ConduitLLM.WebUI.csproj${NC}"
else
    echo -e "${GREEN}✓ EntityFrameworkCore packages already removed${NC}"
fi

# Final message
echo -e "${BLUE}=== Next Steps ===${NC}"
echo -e "1. Remove all files and code identified in this audit"
echo -e "2. Run the application to verify no errors occur"
echo -e "3. Run tests to ensure all functionality works correctly"
echo -e "4. Update documentation to reflect the changes"