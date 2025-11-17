#!/bin/bash
# Script to clear all blocked IPs from Redis and database

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=== Clear Blocked IPs Script ==="
echo "This will clear all blocked IPs from Redis and the database"
echo ""

# Clear Redis blocked IPs
echo "1. Clearing Redis blocked IPs..."
echo "   - Clearing blocked_ips set"
docker exec -it conduit-redis-1 redis-cli DEL "conduit:blocked_ips" 2>/dev/null || echo "   No blocked_ips found"

echo "   - Clearing IP-specific keys"
docker exec -it conduit-redis-1 redis-cli --scan --pattern "conduit:ip:*" | while read key; do
    echo "   - Deleting $key"
    docker exec -it conduit-redis-1 redis-cli DEL "$key"
done

echo "   - Clearing failed login attempts"
docker exec -it conduit-redis-1 redis-cli --scan --pattern "conduit:failed_login:*" | while read key; do
    echo "   - Deleting $key"
    docker exec -it conduit-redis-1 redis-cli DEL "$key"
done

echo "   - Clearing rate limit keys"
docker exec -it conduit-redis-1 redis-cli --scan --pattern "conduit:rate_limit:*" | while read key; do
    echo "   - Deleting $key"
    docker exec -it conduit-redis-1 redis-cli DEL "$key"
done

echo ""
echo "2. Checking database for blocked IPs table..."
# Check if there's a blocked_ips table (there might not be one)
HAS_TABLE=$(docker exec -it conduit-postgres-1 psql -U conduit -d conduitdb -t -c "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'blocked_ips');" 2>/dev/null | tr -d ' \r\n')

if [ "$HAS_TABLE" = "t" ]; then
    echo "   - Found blocked_ips table, clearing..."
    docker exec -it conduit-postgres-1 psql -U conduit -d conduitdb -c "DELETE FROM blocked_ips;" 2>/dev/null
    echo "   - Cleared blocked_ips table"
else
    echo "   - No blocked_ips table found in database (this is normal)"
fi

echo ""
echo "3. Restarting services to clear in-memory blocks..."
docker restart conduit-api-1 conduit-admin-1 conduit-webui-1

echo ""
echo "✅ All blocked IPs have been cleared!"
echo ""
echo "Note: If services are using different container names, update this script accordingly."