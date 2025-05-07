#!/bin/bash

echo "Checking all tables:"
docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c "SELECT schemaname, tablename FROM pg_tables WHERE schemaname='public';"

echo "Checking ModelProviderMappings details:"
docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c "\d+ \"ModelProviderMappings\";"

echo "Checking ModelProviderMappings data:"
docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c "SELECT * FROM \"ModelProviderMappings\";"