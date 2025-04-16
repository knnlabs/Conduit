# ConduitLLM API Documentation

## Authentication

ConduitLLM uses virtual API keys for authentication. These keys allow you to authenticate requests to the API while maintaining fine-grained control over permissions, spending limits, and model access.

### Virtual Key Authentication

To authenticate your requests, include one of the following:

1. **Bearer Authentication Header** (Recommended)
   ```
   Authorization: Bearer condt_your_virtual_key_here
   ```

2. **X-Api-Key Header**
   ```
   X-Api-Key: condt_your_virtual_key_here
   ```

3. **Query Parameter** (Not recommended for production use due to security concerns)
   ```
   https://your-conduit-instance.com/api/v1/chat/completions?api_key=condt_your_virtual_key_here
   ```

### Virtual Key Management

Virtual keys are managed through the ConduitLLM web interface under the "Virtual Keys" section. Administrators can:

- Create new virtual keys
- Set spending limits and budgets
- Restrict access to specific models
- Set expiration dates
- Monitor usage and spending
- Reset key spending
- Disable or delete keys

## API Endpoints

### Chat Completions

Generate chat completions from an LLM provider.

```
POST /api/v1/chat/completions
```

#### Example Request

```json
{
  "model": "gpt-3.5-turbo",
  "messages": [
    {
      "role": "system",
      "content": "You are a helpful assistant."
    },
    {
      "role": "user",
      "content": "Hello, how are you?"
    }
  ],
  "temperature": 0.7,
  "max_tokens": 150
}
```

### Completions

Generate text completions from an LLM provider.

```
POST /api/v1/completions
```

### Embeddings

Generate embeddings for provided text.

```
POST /api/v1/embeddings
```

#### Example Request

```json
{
  "model": "text-embedding-ada-002",
  "input": "The food was delicious and the waiter..."
}
```

### Models

List available models.

```
GET /api/v1/models
```

### Virtual Key Management

ConduitLLM provides a robust set of endpoints to manage virtual keys programmatically.

```
BASE URL: /api/v1/virtualkeys
```

#### Create a Virtual Key

Create a new virtual key with customized permissions and limits.

```
POST /api/v1/virtualkeys
```

**Authentication Required:** Master Key (`X-Master-Key` header)

**Request Body:**

```json
{
  "name": "Production Key",
  "description": "API key for production environment",
  "expiresAt": "2026-04-13T00:00:00Z",
  "dailyBudget": 10.00,
  "monthlyBudget": 200.00,
  "totalBudget": 1000.00,
  "allowedModels": ["gpt-4", "claude-3-opus"],
  "isEnabled": true
}
```

**Response:**

```json
{
  "id": "1234abcd-5678-efgh-9012-ijklmnopqrst",
  "key": "condt_abcdefghijklmnopqrstuvwxyz123456",
  "name": "Production Key",
  "description": "API key for production environment",
  "createdAt": "2025-04-13T17:17:30Z",
  "expiresAt": "2026-04-13T00:00:00Z",
  "dailyBudget": 10.00,
  "monthlyBudget": 200.00,
  "totalBudget": 1000.00,
  "spend": {
    "total": 0.00,
    "daily": 0.00,
    "monthly": 0.00
  },
  "allowedModels": ["gpt-4", "claude-3-opus"],
  "isEnabled": true
}
```

#### List All Virtual Keys

Retrieve a list of all virtual keys.

```
GET /api/v1/virtualkeys
```

#### Get a Specific Virtual Key

Retrieve details for a specific virtual key.

```
GET /api/v1/virtualkeys/{id}
```

#### Update a Virtual Key

Update an existing virtual key's properties.

```
PUT /api/v1/virtualkeys/{id}
```

**Authentication Required:** Master Key (`X-Master-Key` header)

**Request Body:**
Same format as the create endpoint, but fields are optional.

#### Delete a Virtual Key

Permanently delete a virtual key.

```
DELETE /api/v1/virtualkeys/{id}
```

**Authentication Required:** Master Key (`X-Master-Key` header)

#### Reset Key Spend

Reset the spend counters for a virtual key.

```
POST /api/v1/virtualkeys/{id}/reset-spend
```

**Authentication Required:** Master Key (`X-Master-Key` header)

**Request Body:**

```json
{
  "resetDaily": true,
  "resetMonthly": true,
  "resetTotal": false
}
```

### Request Logs

ConduitLLM tracks detailed information about API usage through the request logging system.

```
BASE URL: /api/v1/requestlogs
```

#### List Request Logs

Retrieve a list of request logs, optionally filtered by virtual key.

```
GET /api/v1/requestlogs
```

**Query Parameters:**
- `virtualKeyId` (optional): Filter logs by virtual key
- `startDate` (optional): Start date for filtering logs
- `endDate` (optional): End date for filtering logs
- `page` (optional): Page number for pagination
- `pageSize` (optional): Number of items per page

**Response:**

```json
{
  "items": [
    {
      "id": "1234abcd-5678-efgh-9012-ijklmnopqrst",
      "virtualKeyId": "5678efgh-9012-ijkl-mnop-qrstuvwxyz12",
      "timestamp": "2025-04-13T15:30:45Z",
      "endpoint": "/api/v1/chat/completions",
      "model": "gpt-4",
      "inputTokens": 150,
      "outputTokens": 420,
      "cost": 0.0325,
      "statusCode": 200,
      "latencyMs": 2450
    },
    // Additional log entries...
  ],
  "totalCount": 250,
  "pageCount": 13,
  "currentPage": 1
}
```

#### Get Request Log Details

Retrieve detailed information about a specific request log.

```
GET /api/v1/requestlogs/{id}
```

### Notifications

ConduitLLM can send notifications for important events related to virtual keys.

```
BASE URL: /api/v1/notifications
```

#### List Notifications

Retrieve a list of notifications.

```
GET /api/v1/notifications
```

**Query Parameters:**
- `read` (optional): Filter by read/unread status
- `page` (optional): Page number for pagination
- `pageSize` (optional): Number of items per page

**Response:**

```json
{
  "items": [
    {
      "id": "1234abcd-5678-efgh-9012-ijklmnopqrst",
      "type": "BudgetWarning",
      "message": "Virtual Key 'Production Key' has reached 80% of its daily budget",
      "timestamp": "2025-04-13T14:25:10Z",
      "read": false,
      "virtualKeyId": "5678efgh-9012-ijkl-mnop-qrstuvwxyz12"
    },
    // Additional notifications...
  ],
  "totalCount": 45,
  "pageCount": 3,
  "currentPage": 1
}
```

#### Mark Notification as Read

Mark a notification as read.

```
PUT /api/v1/notifications/{id}/read
```

## Error Responses

### Authentication Errors

- **401 Unauthorized**: Missing or invalid API key
  ```json
  {
    "error": {
      "message": "Invalid API key",
      "type": "auth_error",
      "code": 401
    }
  }
  ```

- **403 Forbidden**: Key is disabled, expired, or has insufficient permissions
  ```json
  {
    "error": {
      "message": "API key budget exceeded",
      "type": "auth_error",
      "code": 403
    }
  }
  ```

### Resource Errors

- **400 Bad Request**: Invalid input
- **404 Not Found**: Resource not found
- **500 Internal Server Error**: Server error

## Usage Tracking and Budgets

ConduitLLM tracks usage for all API calls made with virtual keys. Usage is measured in dollars based on token consumption and model costs. Administrators can set budget limits:

- **Total Budget**: Maximum lifetime spending for the key
- **Monthly Budget**: Spending limit that resets monthly
- **Daily Budget**: Spending limit that resets daily

When a budget limit is reached, further API calls will be rejected with a 403 Forbidden error.

### Automatic Budget Management

The system includes automatic budget management features:

- **Automatic Resets**: Daily and monthly budgets automatically reset at the beginning of each day/month
- **Budget Alerts**: The system generates notifications when keys approach their budget limits (typically at 80% and 95%)
- **Expiration Handling**: Keys automatically disable when they reach their expiration date

### Request Tracking Metrics

The request tracking system captures the following metrics for each API call:

- **Input Tokens**: Number of tokens in the request
- **Output Tokens**: Number of tokens in the response
- **Cost**: Calculated cost of the request based on token usage and model pricing
- **Latency**: Time taken to process the request in milliseconds
- **Status Code**: HTTP status code of the response
- **Model**: LLM model used for the request
- **Timestamp**: When the request was made
- **Endpoint**: Which API endpoint was called

This detailed tracking enables comprehensive usage analysis and budget enforcement.

## Master Key Authentication

Administrative operations on virtual keys require authentication with a master key. This provides an additional layer of security for sensitive operations.

To authenticate with the master key, include the following header:

```
X-Master-Key: your_master_key_here
```

The following operations require master key authentication:

- Creating new virtual keys
- Updating existing virtual keys
- Deleting virtual keys
- Resetting key spending

## Notification System

The notification system alerts administrators about important events related to virtual keys:

### Notification Types

- **Budget Warnings**: Alerts when keys approach or exceed budget limits
- **Expiration Notices**: Notifications about keys nearing or reaching expiration
- **Security Alerts**: Notifications about suspicious usage patterns
- **System Notices**: Information about system maintenance or updates

Notifications can be viewed and managed through both the API and the web interface.
