#!/bin/bash

# Make the script exit on error
set -e

echo "Adding frontier model costs to the database..."

# We'll use the PostgreSQL container from our Docker Compose setup
docker compose exec postgres psql -U conduit -d conduitdb -f /app/add-frontier-model-costs.sql

echo "Frontier model costs have been added to the database."

# Show a summary of the added model costs
echo "=== Model Costs Summary ==="
docker compose exec postgres psql -U conduit -d conduitdb -c 'SELECT "ModelIdPattern", "InputTokenCost" * 1000 AS "InputCostPer1K", "OutputTokenCost" * 1000 AS "OutputCostPer1K" FROM "ModelCosts" ORDER BY "ModelIdPattern"'

echo "=== Embedding and Image Generation Costs ==="
docker compose exec postgres psql -U conduit -d conduitdb -c 'SELECT "ModelIdPattern", "EmbeddingTokenCost" * 1000 AS "EmbeddingCostPer1K", "ImageCostPerImage" FROM "ModelCosts" WHERE "EmbeddingTokenCost" IS NOT NULL OR "ImageCostPerImage" IS NOT NULL ORDER BY "ModelIdPattern"'

echo "You can view and edit these costs in the Conduit Admin interface at http://localhost:5001/model-costs"