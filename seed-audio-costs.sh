#!/bin/bash

# Script to seed audio cost data into the database

echo "Seeding audio cost data..."

# Check if PostgreSQL container is running
if ! docker ps | grep -q "conduit-postgres"; then
    echo "Error: PostgreSQL container is not running. Please start it with 'docker compose up -d postgres'"
    exit 1
fi

# Run the SQL script
docker exec -i conduit-postgres-1 psql -U postgres -d ConduitConfiguration << EOF
$(cat seed-audio-costs.sql)
EOF

if [ $? -eq 0 ]; then
    echo "Audio cost data seeded successfully!"
else
    echo "Error: Failed to seed audio cost data"
    exit 1
fi

# Verify the data was inserted
echo -e "\nVerifying seeded data..."
docker exec conduit-postgres-1 psql -U postgres -d ConduitConfiguration -c "SELECT \"Provider\", \"OperationType\", \"Model\", \"CostPerUnit\" FROM \"AudioCosts\" ORDER BY \"Provider\", \"OperationType\" LIMIT 10;"