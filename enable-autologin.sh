#!/bin/bash

# Script to enable AutoLogin in development environments
# This is useful for bypassing the login screen when CONDUIT_MASTER_KEY is set

echo "Enabling AutoLogin for development environment..."

# Get the database provider from environment
DB_PROVIDER=${DB_PROVIDER:-sqlite}

if [ "$DB_PROVIDER" = "sqlite" ]; then
    # SQLite
    SQLITE_PATH=${CONDUIT_SQLITE_PATH:-./data/conduit.db}
    if [ -f "$SQLITE_PATH" ]; then
        sqlite3 "$SQLITE_PATH" < initialize-autologin.sql
        echo "AutoLogin enabled in SQLite database: $SQLITE_PATH"
    else
        echo "Error: SQLite database not found at $SQLITE_PATH"
        exit 1
    fi
elif [ "$DB_PROVIDER" = "postgres" ]; then
    # PostgreSQL
    if [ -z "$CONDUIT_POSTGRES_CONNECTION_STRING" ]; then
        echo "Error: CONDUIT_POSTGRES_CONNECTION_STRING not set"
        exit 1
    fi
    
    # Parse connection string for psql
    # This is a simplified version - in production use proper parsing
    psql "$CONDUIT_POSTGRES_CONNECTION_STRING" -f initialize-autologin.sql
    echo "AutoLogin enabled in PostgreSQL database"
else
    echo "Error: Unknown database provider: $DB_PROVIDER"
    exit 1
fi

echo "Done! AutoLogin is now enabled."
echo "Make sure CONDUIT_MASTER_KEY environment variable is set for automatic login."