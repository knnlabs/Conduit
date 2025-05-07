#!/bin/bash

# Create a test model mapping and provider credential

cat <<EOF | docker exec -i conduit-postgres-1 psql -U conduit -d conduitdb
-- Insert a test model mapping
INSERT INTO "ModelProviderMappings" ("ModelAlias", "ProviderModelName", "ProviderCredentialId", "IsEnabled", "CreatedAt", "UpdatedAt") 
VALUES ('gpt-3.5-turbo', 'gpt-3.5-turbo', 2, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
EOF

# Verify the inserts
echo "Checking ProviderCredentials:"
docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c "SELECT * FROM \"ProviderCredentials\";"

echo "Checking ModelProviderMappings:"
docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c "SELECT * FROM \"ModelProviderMappings\";"