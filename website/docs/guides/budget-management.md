---
sidebar_position: 3
title: Budget Management
description: Learn how to control and monitor costs in Conduit
---

# Budget Management

Conduit provides comprehensive tools for managing and controlling LLM spending through budget limits, usage tracking, and cost optimization.

## Cost Tracking

Conduit automatically tracks costs for all requests:

- **Per Virtual Key**: Track spending by application or user
- **Per Provider**: Monitor costs across different LLM services
- **Per Model**: See which models are most expensive
- **Time-Based**: View costs by day, week, month, or custom periods

## Accessing Cost Data

### Via Web UI

1. Navigate to **Cost Dashboard**
2. View cost breakdowns by different dimensions
3. Filter by date range, provider, model, or virtual key
4. Export data as CSV for further analysis

### Via API

```bash
curl http://localhost:5000/admin/costs \
  -H "Authorization: Bearer your-master-key" \
  -G --data-urlencode "start_date=2023-01-01" \
  --data-urlencode "end_date=2023-01-31"
```

## Budget Limits

Conduit provides several levels of budget controls:

### Virtual Key Budgets

Set spending limits on individual virtual keys:

1. Navigate to **Virtual Keys**
2. Create or edit a virtual key
3. Expand the **Budget Controls** section
4. Configure:
   - **Daily Limit**: Maximum spending per day
   - **Monthly Limit**: Maximum spending per month
   - **Total Limit**: Maximum lifetime spending
5. Save the configuration

### Global Budget Limits

Set organization-wide spending limits:

1. Navigate to **Configuration > Budget**
2. Configure:
   - **Daily Global Limit**: Maximum total spending per day
   - **Monthly Global Limit**: Maximum total spending per month
   - **Provider Limits**: Maximum spending per provider
3. Save the configuration

## Budget Alerts

Configure notifications when spending reaches certain thresholds:

1. Navigate to **Configuration > Notifications**
2. Add a budget alert
3. Configure:
   - **Threshold**: Percentage or absolute amount
   - **Scope**: Global, provider, or virtual key
   - **Notification Method**: Email, webhook, or UI
4. Save the configuration

## Cost Optimization Strategies

### Routing for Cost

Use the Least Cost routing strategy to automatically select the most economical provider:

1. Navigate to **Configuration > Routing**
2. Select **Least Cost** as the routing strategy
3. Save the configuration

### Caching

Enable caching to avoid paying for repeated identical requests:

1. Navigate to **Configuration > Caching**
2. Enable caching
3. Configure cache settings
4. Save the configuration

See the [Cache Configuration](cache-configuration) guide for details.

### Model Cost Definition

Define and update model costs to ensure accurate budget management:

1. Navigate to **Configuration > Model Costs**
2. View or edit existing model costs
3. Add missing models with their costs
4. Save the configuration

## Spend History

Access detailed spending history to analyze trends:

1. Navigate to **Virtual Keys > (select key) > Spend History**
2. View spending over time
3. Identify usage patterns and cost drivers
4. Export data for reporting

## Implementing a Cost Control Pipeline

For comprehensive cost control, implement this pipeline:

1. **Track**: Enable detailed cost tracking
2. **Analyze**: Regularly review the Cost Dashboard
3. **Optimize**: Use caching and least-cost routing
4. **Limit**: Set appropriate budget limits
5. **Alert**: Configure notifications for threshold breaches
6. **Adjust**: Modify limits and settings based on actual usage

## Next Steps

- Learn about [Cache Configuration](cache-configuration) for cost optimization
- Explore [Model Routing](../features/model-routing) for cost-based routing
- See the [Virtual Keys](../features/virtual-keys) guide for per-key budget controls