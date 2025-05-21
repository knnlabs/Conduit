#!/bin/bash
# Script to remove deprecated direct database access code from the WebUI project
# This script should be run in October 2025 as part of the migration completion

# Color definitions
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
NC='\033[0m' # No Color

echo -e "${BLUE}=== Direct Database Access Removal Tool ===${NC}"
echo "This tool removes all deprecated direct database access code from the WebUI project."
echo "Use this tool only after confirming that all users have migrated to Admin API mode."
echo ""

# Ensure we're in the right directory
cd "$(dirname "$0")"

# Check if we're on the correct branch
CURRENT_BRANCH=$(git branch --show-current)
if [ "$CURRENT_BRANCH" != "feature/remove-legacy-db-access" ]; then
    echo -e "${YELLOW}You're not on the feature/remove-legacy-db-access branch.${NC}"
    read -p "Do you want to create and switch to this branch now? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        git checkout -b feature/remove-legacy-db-access
        echo -e "${GREEN}Created and switched to feature/remove-legacy-db-access branch.${NC}"
    else
        echo -e "${RED}Aborting. Please switch to the correct branch and run this script again.${NC}"
        exit 1
    fi
fi

# Create a backup directory
BACKUP_DIR="deprecated-code-backup-$(date +%Y%m%d%H%M%S)"
mkdir -p "$BACKUP_DIR/Services"
mkdir -p "$BACKUP_DIR/Extensions"
mkdir -p "$BACKUP_DIR/Components/Shared"
echo -e "${GREEN}Created backup directory: $BACKUP_DIR${NC}"

echo -e "${YELLOW}Backing up deprecated files before removal...${NC}"

# Step 1: Back up and remove deprecated service implementations
DEPRECATED_SERVICES=(
    "VirtualKeyService.cs"
    "GlobalSettingService.cs"
    "IpFilterService.cs"
    "RequestLogService.cs"
    "CostDashboardService.cs"
    "RouterService.cs"
    "ModelCostService.cs"
    "ModelProviderMappingService.cs"
    "ProviderCredentialService.cs"
    "ProviderHealthService.cs"
    "DatabaseBackupService.cs"
    "DbRouterConfigRepository.cs"
)

for service in "${DEPRECATED_SERVICES[@]}"; do
    if [ -f "ConduitLLM.WebUI/Services/$service" ]; then
        echo -e "Backing up $service..."
        cp "ConduitLLM.WebUI/Services/$service" "$BACKUP_DIR/Services/"
        echo -e "Removing $service..."
        rm "ConduitLLM.WebUI/Services/$service"
    else
        echo -e "${YELLOW}$service already removed or not found.${NC}"
    fi
done

# Step 2: Back up and remove deprecated extension methods
if [ -f "ConduitLLM.WebUI/Extensions/DbContextRegistrationExtensions.cs" ]; then
    echo -e "Backing up DbContextRegistrationExtensions.cs..."
    cp "ConduitLLM.WebUI/Extensions/DbContextRegistrationExtensions.cs" "$BACKUP_DIR/Extensions/"
    echo -e "Removing DbContextRegistrationExtensions.cs..."
    rm "ConduitLLM.WebUI/Extensions/DbContextRegistrationExtensions.cs"
else
    echo -e "${YELLOW}DbContextRegistrationExtensions.cs already removed or not found.${NC}"
fi

if [ -f "ConduitLLM.WebUI/Extensions/RepositoryServiceExtensions.cs" ]; then
    echo -e "Backing up RepositoryServiceExtensions.cs..."
    cp "ConduitLLM.WebUI/Extensions/RepositoryServiceExtensions.cs" "$BACKUP_DIR/Extensions/"
    echo -e "Removing RepositoryServiceExtensions.cs..."
    rm "ConduitLLM.WebUI/Extensions/RepositoryServiceExtensions.cs"
else
    echo -e "${YELLOW}RepositoryServiceExtensions.cs already removed or not found.${NC}"
fi

# Step 3: Back up and remove deprecation warning component
if [ -f "ConduitLLM.WebUI/Components/Shared/DeprecationWarning.razor" ]; then
    echo -e "Backing up DeprecationWarning.razor..."
    cp "ConduitLLM.WebUI/Components/Shared/DeprecationWarning.razor" "$BACKUP_DIR/Components/Shared/"
    echo -e "Removing DeprecationWarning.razor..."
    rm "ConduitLLM.WebUI/Components/Shared/DeprecationWarning.razor"
else
    echo -e "${YELLOW}DeprecationWarning.razor already removed or not found.${NC}"
fi

# Step 4: Update Program.cs to remove conditional logic
echo -e "${YELLOW}Updating Program.cs to remove conditional logic...${NC}"

# Create a backup of Program.cs
cp "ConduitLLM.WebUI/Program.cs" "$BACKUP_DIR/"

# Update Program.cs - Remove conditional DbContext registration
echo -e "Removing DbContext registration and feature flag checks from Program.cs..."

# Perform the replacements using awk for complex multi-line patterns
awk '
BEGIN { skip = 0; skipLegacyWarn = 0; }
{
    # Skip DbContext registration section
    if ($0 ~ /Check if we should use direct database access/ ||
        $0 ~ /var useAdminApiStr =/) {
        skip = 1;
    }
    
    # Skip feature flag checks
    if ($0 ~ /Check if direct database access is completely disabled/ ||
        $0 ~ /var disableDirectDatabaseStr =/) {
        skip = 1;
    }
    
    # Skip conditional DbContext registration
    if ($0 ~ /Only register DbContext if using direct database access/ ||
        $0 ~ /if \(useDirectDatabaseAccess\)/) {
        skip = 1;
    }
    
    # Skip legacy warning section
    if ($0 ~ /Log deprecation warning/) {
        skipLegacyWarn = 1;
    }
    
    # Determine if we should print the current line
    if (skip == 0 && skipLegacyWarn == 0) {
        print $0;
    }
    
    # Stop skipping after certain patterns
    if (skip == 1 && $0 ~ /\}/ && $0 !~ /\{/) {
        skip = 0;
    }
    
    # Stop skipping legacy warning after certain patterns
    if (skipLegacyWarn == 1 && $0 ~ /\}/) {
        skipLegacyWarn = 0;
    }
    
    # Replace conditional Admin API block with unconditional
    if ($0 ~ /else/ && $0 !~ /else if/) {
        if (prevLine ~ /\}/ && skip == 0) {
            # Replace "else" with simplified code
            print "// Using Admin API client mode (default)";
            print "Console.WriteLine(\"[Conduit WebUI] Using Admin API client mode\");";
            skip = 1; # Skip the opening brace
        }
    }
    
    # Save the previous line for context
    prevLine = $0;
}' "ConduitLLM.WebUI/Program.cs" > "ConduitLLM.WebUI/Program.cs.new"

# Replace the old file with the updated one
mv "ConduitLLM.WebUI/Program.cs.new" "ConduitLLM.WebUI/Program.cs"

# Step 5: Update AdminApiOptions to remove legacy mode
echo -e "${YELLOW}Updating AdminApiOptions to remove legacy mode...${NC}"

# Create a backup
cp "ConduitLLM.WebUI/Options/AdminApiOptions.cs" "$BACKUP_DIR/"

# Replace UseAdminApi property to always return true
sed -i 's/public bool UseAdminApi { get; set; } = true;/public bool UseAdminApi { get; } = true; \/\/ Always uses Admin API/' "ConduitLLM.WebUI/Options/AdminApiOptions.cs"

# Step 6: Update AdminClientExtensions to remove conditional logic
echo -e "${YELLOW}Updating AdminClientExtensions to remove conditional logic...${NC}"

# Create a backup
cp "ConduitLLM.WebUI/Extensions/AdminClientExtensions.cs" "$BACKUP_DIR/"

# Replace conditional extension methods with simplified versions
sed -i 's/CONDUIT_USE_ADMIN_API/CONDUIT_ADMIN_API_BASE_URL/g' "ConduitLLM.WebUI/Extensions/AdminClientExtensions.cs"
sed -i 's/bool useAdminApi = true;/bool useAdminApi = true; \/\/ Always true - legacy mode removed/g' "ConduitLLM.WebUI/Extensions/AdminClientExtensions.cs"
sed -i 's/bool.TryParse(useAdminApiStr, out useAdminApi)/useAdminApi = true/g' "ConduitLLM.WebUI/Extensions/AdminClientExtensions.cs"

# Step 7: Update VirtualKeyMaintenanceService to remove conditional logic
echo -e "${YELLOW}Updating VirtualKeyMaintenanceService to remove conditional logic...${NC}"

# Create a backup
cp "ConduitLLM.WebUI/Services/VirtualKeyMaintenanceService.cs" "$BACKUP_DIR/Services/"

# Update the service to only use Admin API
sed -i 's/_useAdminApi/true/g' "ConduitLLM.WebUI/Services/VirtualKeyMaintenanceService.cs"

# Step 8: Update ProviderHealthMonitorService to remove conditional logic
echo -e "${YELLOW}Updating ProviderHealthMonitorService to remove conditional logic...${NC}"

# Create a backup
cp "ConduitLLM.WebUI/Services/ProviderHealthMonitorService.cs" "$BACKUP_DIR/Services/"

# Update the service to only use Admin API
sed -i 's/_useAdminApi/true/g' "ConduitLLM.WebUI/Services/ProviderHealthMonitorService.cs"

# Step 9: Update MainLayout.razor to remove DeprecationWarning component
echo -e "${YELLOW}Updating MainLayout.razor to remove DeprecationWarning component...${NC}"

# Create a backup
cp "ConduitLLM.WebUI/Components/Layout/MainLayout.razor" "$BACKUP_DIR/"

# Remove the DeprecationWarning component
sed -i '/<ConduitLLM\.WebUI\.Components\.Shared\.DeprecationWarning \/>/d' "ConduitLLM.WebUI/Components/Layout/MainLayout.razor"

# Step 10: Update project dependencies
echo -e "${YELLOW}Updating project dependencies to remove EF Core...${NC}"

# Create a backup of csproj file
cp "ConduitLLM.WebUI/ConduitLLM.WebUI.csproj" "$BACKUP_DIR/"

# Remove Entity Framework Core dependencies
sed -i '/<PackageReference Include="Microsoft.EntityFrameworkCore/,/<\/PackageReference>/d' "ConduitLLM.WebUI/ConduitLLM.WebUI.csproj"
sed -i '/<PackageReference Include="Npgsql.EntityFrameworkCore/,/<\/PackageReference>/d' "ConduitLLM.WebUI/ConduitLLM.WebUI.csproj"

# Step 11: Update RouterExtensions.cs to remove conditional logic
if [ -f "ConduitLLM.WebUI/Extensions/RouterExtensions.cs" ]; then
    echo -e "${YELLOW}Updating RouterExtensions.cs to remove conditional logic...${NC}"
    
    # Create a backup
    cp "ConduitLLM.WebUI/Extensions/RouterExtensions.cs" "$BACKUP_DIR/Extensions/"
    
    # Update RouterExtensions.cs to remove conditional logic for DbRouterConfigRepository
    sed -i 's/if (useDirectDatabaseAccess)/if (false)/g' "ConduitLLM.WebUI/Extensions/RouterExtensions.cs"
fi

# Step 12: Update Imports in Program.cs
echo -e "${YELLOW}Removing unnecessary imports in Program.cs...${NC}"

# Remove EntityFrameworkCore import
sed -i '/using Microsoft.EntityFrameworkCore;/d' "ConduitLLM.WebUI/Program.cs"

echo -e "${GREEN}Removal of deprecated code completed!${NC}"
echo -e "A backup of all removed files has been created in the $BACKUP_DIR directory."
echo -e ""
echo -e "${BLUE}Next steps:${NC}"
echo -e "1. Build the project to check for any errors: dotnet build"
echo -e "2. Run tests to verify all functionality: dotnet test"
echo -e "3. Update docker-compose.yml to remove legacy environment variables"
echo -e "4. Commit changes to the feature branch"
echo -e "5. Create a pull request for review"