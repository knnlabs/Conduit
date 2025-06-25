# Gemini Project Configuration: Conduit

This document provides project-specific guidance for Gemini to ensure its actions align with the existing architecture, conventions, and procedures of the Conduit project.

## 1. Project Overview

Conduit appears to be a Large Language Model (LLM) gateway or proxy. Its purpose is to provide a unified interface to various underlying LLM providers, managing models, credentials, and routing requests. The system is composed of several .NET projects, including a core logic library, an HTTP API, an admin interface, and a Web UI, all containerized with Docker.

## 2. Key Technologies

- **Backend:** .NET (C#)
- **Frontend:** Node.js/TypeScript (inferred from `package.json` and `WebUI` project)
- **Database:** SQL (inferred from `.sql` scripts)
- **Containerization:** Docker, Docker Compose
- **Architecture:** Layered or microservices-based architecture

## 3. Project Structure

The solution is organized into several distinct projects:

- `Conduit.sln`: The main solution file for the .NET projects.
- `ConduitLLM.Core`: Contains shared domain logic, interfaces, data models, and core services.
- `ConduitLLM.Http`: The primary public-facing API endpoint. It handles incoming requests and routes them through the core services.
- `ConduitLLM.Admin`: A separate application for administrative tasks, likely for managing users, providers, and models.
- `ConduitLLM.WebUI`: The frontend user interface.
- `ConduitLLM.Providers`: Contains the specific implementations for interacting with different third-party LLM providers.
- `ConduitLLM.Configuration`: Manages configuration loading, validation, and access.
- `*.Tests`: Various test projects corresponding to the application projects.
- `Clients/`: Contains pre-generated client libraries for accessing the Conduit API.
- `docker/`: Contains Docker-related resources.
- `scripts/`: Contains utility and automation shell scripts.

## 4. Common Commands

Before running any commands, ensure that all necessary dependencies are installed (e.g., .NET SDK, Docker).

- **Build the entire solution:**
  ```bash
  dotnet build Conduit.sln
  ```

- **Run all tests:**
  ```bash
  dotnet test Conduit.sln
  ```

- **Run the application stack (Development):**
  The `docker-compose.dev.yml` file is configured for local development.
  ```bash
  docker-compose -f docker-compose.dev.yml up --build
  ```

- **Format C# Code:**
  To ensure consistent code style, format the solution using `dotnet format`.
  ```bash
  dotnet format Conduit.sln
  ```

## 5. Coding Conventions

- **Style:** Adhere strictly to the styles defined in `.editorconfig` and `.stylelintrc.json`.
- **Architecture:** Maintain the existing separation of concerns. New features should be implemented in the appropriate project layer (e.g., provider-specific logic in `ConduitLLM.Providers`, core business logic in `ConduitLLM.Core`, API endpoints in `ConduitLLM.Http`).
- **Dependencies:** Before adding a new dependency, check if a similar one is already in use. Add new NuGet packages or npm packages only when necessary.
- **Testing:** All new features or bug fixes should be accompanied by corresponding unit or integration tests in the relevant test project.
