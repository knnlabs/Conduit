---
sidebar_position: 1
title: Installation
description: How to install and set up Conduit in your environment
---

# Installation Guide

This guide will walk you through the process of installing and setting up Conduit on your system.

## Prerequisites

Before you begin, ensure you have the following prerequisites installed:

- [Docker](https://www.docker.com/get-started) (version 20.10.0 or higher)
- [Docker Compose](https://docs.docker.com/compose/install/) (version 2.0.0 or higher)
- For local development:
  - [.NET SDK](https://dotnet.microsoft.com/download) (version 8.0 or higher)
  - [Node.js](https://nodejs.org/) (version 18.0 or higher)

## Option 1: Using Docker (Recommended)

The easiest way to get started with Conduit is using Docker and Docker Compose.

### Step 1: Clone the Repository

```bash
git clone https://github.com/knnlabs/conduit.git
cd conduit
```

### Step 2: Configure Environment Variables

Create a `.env` file in the root directory with your configuration:

```bash
# Example .env file
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your_secure_master_key
CONDUIT_HOST=0.0.0.0
CONDUIT_PORT=5000
CONDUIT_DATABASE_PATH=/data/conduit.db
CONDUIT_REDIS_ENABLED=true
CONDUIT_REDIS_CONNECTION=redis:6379
```

See the [Environment Variables](../guides/environment-variables) guide for a complete list of available options.

### Step 3: Start Conduit Using Docker Compose

```bash
docker compose up -d
```

This command will start:
- Conduit HTTP API on port 5000
- Conduit Web UI on port 5001
- Redis cache server on port 6379
- SQLite database (persisted in a Docker volume)

### Step 4: Verify Installation

Open your browser and navigate to `http://localhost:5001` to access the Conduit Web UI.

## Option 2: Manual Installation

For development purposes or custom deployments, you can install Conduit manually.

### Step 1: Clone the Repository

```bash
git clone https://github.com/knnlabs/conduit.git
cd conduit
```

### Step 2: Build the Solution

```bash
dotnet build
```

### Step 3: Run the HTTP API

```bash
dotnet run --project ConduitLLM.Http
```

### Step 4: Run the Web UI (in a separate terminal)

```bash
dotnet run --project ConduitLLM.WebUI
```

## Initial Configuration

Once Conduit is running, you'll need to perform some initial configuration:

1. Log in to the Web UI using the master key you configured
2. Add provider credentials for the LLM services you plan to use
3. Create model mappings to define which models are available
4. Set up virtual keys for applications that will connect to Conduit

For detailed configuration steps, see the [Configuration Guide](configuration).

## Next Steps

- [Quick Start Guide](quick-start): Learn how to make your first request
- [Configuration Guide](configuration): Detailed information about configuring Conduit
- [Environment Variables](../guides/environment-variables): Reference for all available environment variables
- [Virtual Keys](../features/virtual-keys): Learn how to create and manage virtual keys

## Troubleshooting

If you encounter any issues during installation:

1. Check the [Common Issues](../troubleshooting/common-issues) page
2. Review Docker logs: `docker compose logs -f`
3. For API issues: `docker compose logs -f conduit-api`
4. For UI issues: `docker compose logs -f conduit-ui`
5. Open an issue on the [GitHub repository](https://github.com/knnlabs/conduit/issues)