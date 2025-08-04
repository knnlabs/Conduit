# LLM Client Factory Usage Guide

**IMPORTANT: This guide clarifies which factory to use when developing new LLM providers or features.**

## Factory Implementations Overview

| Factory | Purpose | Credentials Source | Real Clients | When to Use |
|---------|---------|-------------------|--------------|-------------|
| **`DatabaseAwareLLMClientFactory`** | Production use | Database | ✅ Yes | **PRIMARY CHOICE** - Use for all production services |
| **`DefaultLLMClientFactory`** | Routing/Testing | Database | ❌ Placeholders only | Only for routing health checks |
| **`LLMClientFactory`** | Legacy/Examples | Config files | ✅ Yes | Only for examples/demos |

## 🎯 **Recommended Usage by Service Type**

### **Core API & Admin API** ✅
```csharp
// Use this for production services that need real LLM calls
services.AddScoped<ILLMClientFactory, DatabaseAwareLLMClientFactory>();
```

### **WebUI** ✅ 
```csharp
// WebUI should use HTTP calls to Core API, not direct factories
// Exception: Health checks can use DefaultLLMClientFactory
services.AddScoped<ILLMClientFactory, DefaultLLMClientFactory>();
```

### **Examples/Demos** ✅
```csharp
// Only for standalone examples without database
services.AddScoped<ILLMClientFactory, LLMClientFactory>();
```

## 🚨 **Critical Notes for New Provider Development**

### **Adding a New LLM Provider:**

1. **Add client implementation** in `ConduitLLM.Providers/YourProviderClient.cs`
2. **Update `DatabaseAwareLLMClientFactory`** to create your client:
   ```csharp
   // In DatabaseAwareLLMClientFactory.cs CreateClientForProvider method
   case "yourprovider":
       var logger = _loggerFactory.CreateLogger<YourProviderClient>();
       client = new YourProviderClient(credentials, modelId, logger, _httpClientFactory, defaultModels);
       break;
   ```
3. **DO NOT** modify `DefaultLLMClientFactory` - it's for routing only
4. **DO NOT** use `LLMClientFactory` - it's legacy

### **Provider Credentials:**
- ✅ **Always stored in database** via Admin UI
- ❌ **Never use appsettings.json** for production credentials
- ✅ **Use `IProviderCredentialService`** to access credentials

### **Current Architecture Issues:**
- ⚠️ `DatabaseAwareLLMClientFactory` is misnamed (should be `ProductionLLMClientFactory`)
- ⚠️ Multiple factories exist for historical reasons, but only one should be used for production
- ⚠️ WebUI health checks use placeholder clients (known limitation)

## 📋 **Quick Decision Tree**

```
Are you building a production service that needs real LLM calls?
├─ YES → Use DatabaseAwareLLMClientFactory
└─ NO
   ├─ Is it for routing/health checks? → Use DefaultLLMClientFactory  
   ├─ Is it an example/demo? → Use LLMClientFactory
   └─ Is it WebUI chat? → Use HTTP API (ConduitApiClient)
```

## 🔧 **Future Cleanup Plan**

1. Rename `DatabaseAwareLLMClientFactory` → `ProductionLLMClientFactory`
2. Fix `DefaultLLMClientFactory` to use real clients for health checks
3. Deprecate `LLMClientFactory` after moving examples to use HTTP API
4. Consolidate to single factory pattern

---
*Last updated: When the architecture is simplified*