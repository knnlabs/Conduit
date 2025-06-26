---
sidebar_position: 1
title: Introduction
description: Introduction to Conduit - A unified API gateway for multiple LLM providers
---

# Introduction to Conduit

Conduit is a unified API gateway for multiple Large Language Model (LLM) providers that simplifies the integration of various AI services into your applications. By providing a consistent interface, Conduit enables you to switch between different LLM providers seamlessly without changing your application code.

## What is Conduit?

Conduit serves as a middleware layer between your applications and various LLM providers such as OpenAI, Anthropic, Cohere, and many others. It offers:

- **Unified API**: A consistent OpenAI-compatible API interface for all supported providers
- **Provider Abstraction**: Easy switching between different LLM providers
- **Smart Routing**: Route requests to different models based on various strategies
- **Budget Management**: Control and monitor spending across multiple providers
- **Virtual Keys**: Create API keys with specific permissions and rate limits
- **Caching**: Reduce costs and improve response times with optional response caching
- **Multimodal Support**: Handle text, images, and other modalities through a single interface

## Why Use Conduit?

- **Avoid Vendor Lock-in**: Switch between providers without changing your application code
- **Cost Optimization**: Route requests to the most cost-effective providers
- **Enhanced Security**: Hide provider keys behind Conduit's virtual key system
- **High Availability**: Fall back to alternative providers if a service is unavailable
- **Centralized Monitoring**: Track usage, costs, and performance across providers

## Supported Providers

Conduit supports a growing list of LLM providers, including:

- OpenAI
- Anthropic
- Azure OpenAI
- Google Gemini
- Cohere
- Mistral
- AWS Bedrock
- Groq
- Replicate
- HuggingFace
- and many more...

## Key Components

Conduit is built with a modular architecture comprising several key components:

- **API Gateway**: Processes incoming requests and routes them to appropriate services
- **Router**: Determines which provider and model to use for each request
- **Provider Clients**: Communicate with specific LLM providers
- **Configuration Service**: Manages system settings and provider credentials
- **Virtual Key System**: Handles authentication and permission management
- **Monitoring & Analytics**: Tracks usage metrics and system performance

## Getting Started

To start using Conduit, check out our [Installation Guide](getting-started/installation) and [Quick Start Tutorial](getting-started/quick-start).

## Architecture

Conduit is built with a modern .NET architecture that emphasizes maintainability, extensibility, and performance. Learn more about the [architecture](architecture/overview) and [components](architecture/components).

## Contributing

Conduit is an open-source project, and contributions are welcome! Check out our [contribution guidelines](https://github.com/knnlabs/conduit/blob/main/CONTRIBUTING.md) to get started.