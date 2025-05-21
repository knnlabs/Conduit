#!/bin/bash
# Script to find all direct database access code in the WebUI project

echo "=== Searching for DbContext usage ==="
echo
grep -r --include="*.cs" "ConfigurationDbContext" ./ConduitLLM.WebUI/ | grep -v "// Legacy" | grep -v "/\*" 
echo

echo "=== Searching for EF Core namespaces ==="
echo
grep -r --include="*.cs" "using Microsoft.EntityFrameworkCore" ./ConduitLLM.WebUI/
echo

echo "=== Searching for repository interfaces ==="
echo
grep -r --include="*.cs" "IVirtualKeyRepository\|IGlobalSettingRepository\|IRequestLogRepository\|IModelCostRepository\|IProviderCredentialRepository\|IRouterConfigRepository" ./ConduitLLM.WebUI/ | grep -v "// Legacy" | grep -v "/\*"
echo

echo "=== Searching for repository registration ==="
echo
grep -r --include="*.cs" "AddRepositories\|AddRepositoryServices" ./ConduitLLM.WebUI/
echo

echo "=== Searching for CONDUIT_USE_ADMIN_API usage ==="
echo
grep -r --include="*.cs" "CONDUIT_USE_ADMIN_API" ./ConduitLLM.WebUI/
echo

echo "=== Checking for legacy service registration ==="
echo
grep -r --include="*.cs" "services.AddScoped<.*VirtualKeyService>" ./ConduitLLM.WebUI/ | grep -v "Adapter"
grep -r --include="*.cs" "services.AddScoped<.*GlobalSettingService>" ./ConduitLLM.WebUI/ | grep -v "Adapter"
grep -r --include="*.cs" "services.AddScoped<.*RequestLogService>" ./ConduitLLM.WebUI/ | grep -v "Adapter"
grep -r --include="*.cs" "services.AddScoped<.*IpFilterService>" ./ConduitLLM.WebUI/ | grep -v "Adapter"
echo

echo "=== Searching for direct DB initialization code ==="
echo
grep -r --include="*.cs" "InitializeDatabaseAsync\|EnsureTablesExistAsync" ./ConduitLLM.WebUI/
echo

echo "=== Complete audit results saved to db-access-audit.txt ==="
{
  echo "# Direct Database Access Audit Results"
  echo "## Timestamp: $(date)"
  echo 
  echo "### DbContext Usage"
  echo '```'
  grep -r --include="*.cs" "ConfigurationDbContext" ./ConduitLLM.WebUI/ | grep -v "// Legacy" | grep -v "/\*"
  echo '```'
  echo
  echo "### EF Core Namespaces"
  echo '```'
  grep -r --include="*.cs" "using Microsoft.EntityFrameworkCore" ./ConduitLLM.WebUI/
  echo '```'
  echo
  echo "### Repository Interfaces"
  echo '```'
  grep -r --include="*.cs" "IVirtualKeyRepository\|IGlobalSettingRepository\|IRequestLogRepository\|IModelCostRepository\|IProviderCredentialRepository\|IRouterConfigRepository" ./ConduitLLM.WebUI/ | grep -v "// Legacy" | grep -v "/\*"
  echo '```'
  echo
  echo "### Repository Registration"
  echo '```'
  grep -r --include="*.cs" "AddRepositories\|AddRepositoryServices" ./ConduitLLM.WebUI/
  echo '```'
  echo
  echo "### CONDUIT_USE_ADMIN_API Usage"
  echo '```'
  grep -r --include="*.cs" "CONDUIT_USE_ADMIN_API" ./ConduitLLM.WebUI/
  echo '```'
  echo
  echo "### Legacy Service Registration"
  echo '```'
  grep -r --include="*.cs" "services.AddScoped<.*VirtualKeyService>" ./ConduitLLM.WebUI/ | grep -v "Adapter"
  grep -r --include="*.cs" "services.AddScoped<.*GlobalSettingService>" ./ConduitLLM.WebUI/ | grep -v "Adapter"
  grep -r --include="*.cs" "services.AddScoped<.*RequestLogService>" ./ConduitLLM.WebUI/ | grep -v "Adapter"
  grep -r --include="*.cs" "services.AddScoped<.*IpFilterService>" ./ConduitLLM.WebUI/ | grep -v "Adapter"
  echo '```'
  echo
  echo "### DB Initialization Code"
  echo '```'
  grep -r --include="*.cs" "InitializeDatabaseAsync\|EnsureTablesExistAsync" ./ConduitLLM.WebUI/
  echo '```'
  echo
  echo "## Next Steps"
  echo "Review these findings and create tickets to remove direct database access code."
} > db-access-audit.txt

chmod +x find-db-access.sh