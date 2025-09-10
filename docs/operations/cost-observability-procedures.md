# Cost Observability Operational Procedures

## Table of Contents

1. [Daily Operations](#daily-operations)
2. [Weekly Procedures](#weekly-procedures)
3. [Monthly Procedures](#monthly-procedures)
4. [Cost Configuration Management](#cost-configuration-management)
5. [Budget Management](#budget-management)
6. [Provider Management](#provider-management)
7. [Maintenance Procedures](#maintenance-procedures)
8. [Backup and Recovery](#backup-and-recovery)
9. [Capacity Planning](#capacity-planning)
10. [Compliance and Auditing](#compliance-and-auditing)

---

## Daily Operations

### Morning Health Check (9:00 AM)

**Objective**: Ensure cost tracking systems are functioning correctly

**Procedure**:

1. **Check System Status**
   ```bash
   # Run morning health check script
   ./scripts/daily-health-check.sh
   
   # Expected output:
   # ✓ Prometheus: UP
   # ✓ Grafana: UP
   # ✓ AlertManager: UP
   # ✓ Redis Cache: UP (97% hit ratio)
   # ✓ Batch Processor: UP (lag: 5s)
   ```

2. **Review Overnight Alerts**
   ```bash
   # Check alert history
   curl -s http://alertmanager:9093/api/v1/alerts | jq '.data[] | {name: .labels.alertname, time: .startsAt}'
   
   # Review resolved incidents
   grep "RESOLVED" /var/log/conduit/alerts.log | tail -20
   ```

3. **Verify Cost Metrics**
   ```promql
   # Check last 24h cost
   sum(increase(conduit_cost_total_dollars[24h]))
   
   # Verify all providers reporting
   count(sum by (provider) (rate(conduit_cost_total_dollars[1h])))
   ```

4. **Check Budget Status**
   ```sql
   -- Keys approaching limits
   SELECT 
     id, 
     name, 
     budget_amount,
     current_period_spend,
     (current_period_spend / budget_amount * 100) as utilization_percent
   FROM virtual_keys
   WHERE (current_period_spend / budget_amount) > 0.7
   ORDER BY utilization_percent DESC;
   ```

5. **Document Findings**
   ```bash
   # Log daily check results
   echo "$(date): Daily check completed. Status: $STATUS" >> /var/log/conduit/daily-checks.log
   ```

### Cost Report Generation (2:00 PM)

**Objective**: Generate daily cost reports for stakeholders

**Procedure**:

1. **Generate Executive Summary**
   ```bash
   ./scripts/generate-cost-report.sh \
     --type daily \
     --format pdf \
     --recipients "executives@conduit.ai" \
     --include-forecast
   ```

2. **Create Department Breakdowns**
   ```sql
   SELECT 
     vk.department,
     COUNT(DISTINCT vk.id) as active_keys,
     SUM(ul.total_cost) as daily_cost,
     AVG(ul.total_cost) as avg_per_request
   FROM virtual_keys vk
   JOIN llm_usage_log ul ON vk.id = ul.virtual_key_id
   WHERE ul.created_at >= CURRENT_DATE
   GROUP BY vk.department
   ORDER BY daily_cost DESC;
   ```

3. **Identify Cost Optimization Opportunities**
   ```bash
   # Run cost optimization analyzer
   ./scripts/analyze-cost-optimization.sh --threshold 100 --output /tmp/optimization.json
   ```

### End of Day Reconciliation (6:00 PM)

**Objective**: Ensure all costs are accurately tracked

**Procedure**:

1. **Reconcile Provider Costs**
   ```bash
   # Compare internal tracking with provider APIs
   ./scripts/reconcile-provider-costs.sh --date today
   ```

2. **Verify Batch Processing**
   ```promql
   # Check batch processing lag
   conduit_batch_spend_update_lag_seconds
   
   # Ensure no stuck batches
   sum(rate(conduit_batch_spend_update_failures_total[1h]))
   ```

3. **Update Cost Forecasts**
   ```bash
   # Update ML model with today's data
   python3 /opt/conduit/ml/update_cost_forecast.py --date today
   ```

---

## Weekly Procedures

### Monday: Cost Review Meeting Preparation

**Objective**: Prepare comprehensive cost analysis for weekly review

**Procedure**:

1. **Generate Weekly Report**
   ```bash
   ./scripts/generate-weekly-report.sh \
     --start-date "$(date -d 'last monday' +%Y-%m-%d)" \
     --end-date "$(date -d 'sunday' +%Y-%m-%d)" \
     --output /reports/weekly_cost_$(date +%Y%W).pdf
   ```

2. **Analyze Trends**
   ```python
   import pandas as pd
   import matplotlib.pyplot as plt
   
   # Load cost data
   df = pd.read_sql("""
     SELECT DATE(created_at) as date, 
            SUM(total_cost) as daily_cost
     FROM llm_usage_log
     WHERE created_at >= CURRENT_DATE - INTERVAL '4 weeks'
     GROUP BY date
   """, connection)
   
   # Calculate week-over-week changes
   df['wow_change'] = df['daily_cost'].pct_change(7)
   
   # Generate trend chart
   df.plot(x='date', y=['daily_cost', 'wow_change'])
   plt.savefig('/reports/weekly_trend.png')
   ```

3. **Identify Anomalies**
   ```sql
   -- Find unusual spending patterns
   WITH weekly_avg AS (
     SELECT 
       virtual_key_id,
       AVG(daily_cost) as avg_cost,
       STDDEV(daily_cost) as stddev_cost
     FROM (
       SELECT 
         virtual_key_id,
         DATE(created_at) as date,
         SUM(total_cost) as daily_cost
       FROM llm_usage_log
       WHERE created_at >= CURRENT_DATE - INTERVAL '4 weeks'
       GROUP BY virtual_key_id, date
     ) daily
     GROUP BY virtual_key_id
   )
   SELECT 
     vk.name,
     wa.avg_cost,
     wa.stddev_cost,
     today.daily_cost as today_cost,
     (today.daily_cost - wa.avg_cost) / wa.stddev_cost as z_score
   FROM weekly_avg wa
   JOIN virtual_keys vk ON wa.virtual_key_id = vk.id
   JOIN (
     SELECT virtual_key_id, SUM(total_cost) as daily_cost
     FROM llm_usage_log
     WHERE DATE(created_at) = CURRENT_DATE
     GROUP BY virtual_key_id
   ) today ON wa.virtual_key_id = today.virtual_key_id
   WHERE ABS((today.daily_cost - wa.avg_cost) / wa.stddev_cost) > 3;
   ```

### Wednesday: Provider Cost Validation

**Objective**: Validate costs against provider invoices

**Procedure**:

1. **Download Provider Reports**
   ```bash
   # OpenAI
   ./scripts/download-provider-report.sh --provider openai --period "last-week"
   
   # Anthropic
   ./scripts/download-provider-report.sh --provider anthropic --period "last-week"
   
   # Others
   for provider in groq fireworks replicate; do
     ./scripts/download-provider-report.sh --provider $provider --period "last-week"
   done
   ```

2. **Compare with Internal Records**
   ```python
   # Load and compare costs
   internal = load_internal_costs(start_date, end_date)
   external = load_provider_reports(provider_reports_dir)
   
   discrepancies = []
   for provider in external.keys():
     internal_cost = internal[provider]['total']
     external_cost = external[provider]['total']
     diff = abs(internal_cost - external_cost)
     
     if diff > 0.01:  # $0.01 threshold
       discrepancies.append({
         'provider': provider,
         'internal': internal_cost,
         'external': external_cost,
         'difference': diff
       })
   
   if discrepancies:
     send_alert("Cost discrepancies detected", discrepancies)
   ```

3. **Update Reconciliation Records**
   ```sql
   INSERT INTO cost_reconciliation (
     provider,
     period_start,
     period_end,
     internal_cost,
     provider_reported_cost,
     discrepancy,
     reconciled_at,
     notes
   ) VALUES (
     $1, $2, $3, $4, $5, $6, NOW(), $7
   );
   ```

### Friday: Optimization Review

**Objective**: Implement cost optimization recommendations

**Procedure**:

1. **Review Model Usage Efficiency**
   ```sql
   -- Find inefficient model usage
   SELECT 
     model,
     COUNT(*) as request_count,
     AVG(prompt_tokens) as avg_prompt_tokens,
     AVG(completion_tokens) as avg_completion_tokens,
     AVG(total_cost) as avg_cost,
     SUM(total_cost) as total_cost
   FROM llm_usage_log
   WHERE created_at >= CURRENT_DATE - INTERVAL '7 days'
   GROUP BY model
   HAVING AVG(prompt_tokens) < 100  -- Short prompts
      AND AVG(completion_tokens) < 50  -- Short responses
   ORDER BY total_cost DESC;
   ```

2. **Implement Routing Optimizations**
   ```bash
   # Update routing rules for cost optimization
   kubectl apply -f - <<EOF
   apiVersion: v1
   kind: ConfigMap
   metadata:
     name: cost-optimization-routing
   data:
     rules: |
       - condition: "prompt_length < 100 AND max_tokens < 100"
         preferred_model: "gpt-3.5-turbo"
       - condition: "requires_vision == false"
         exclude_models: ["gpt-4-vision"]
       - condition: "request_type == 'classification'"
         preferred_model: "claude-instant"
   EOF
   ```

3. **Cache Optimization**
   ```bash
   # Analyze cache effectiveness
   ./scripts/analyze-cache-patterns.sh --period 7d
   
   # Update cache policies
   redis-cli CONFIG SET maxmemory-policy allkeys-lru
   redis-cli CONFIG SET maxmemory 20gb
   ```

---

## Monthly Procedures

### Month-End Cost Closing

**Objective**: Finalize monthly costs for billing and reporting

**Procedure**:

1. **Freeze Cost Records**
   ```sql
   -- Create monthly snapshot
   INSERT INTO monthly_cost_snapshot
   SELECT 
     DATE_TRUNC('month', CURRENT_DATE) as month,
     virtual_key_id,
     SUM(total_cost) as total_cost,
     COUNT(*) as request_count,
     SUM(prompt_tokens) as total_prompt_tokens,
     SUM(completion_tokens) as total_completion_tokens,
     NOW() as snapshot_time
   FROM llm_usage_log
   WHERE created_at >= DATE_TRUNC('month', CURRENT_DATE)
     AND created_at < DATE_TRUNC('month', CURRENT_DATE) + INTERVAL '1 month'
   GROUP BY virtual_key_id;
   ```

2. **Generate Invoices**
   ```bash
   # Generate customer invoices
   ./scripts/generate-invoices.sh \
     --month "$(date +%Y-%m)" \
     --template /templates/invoice.html \
     --output-dir /invoices/$(date +%Y-%m)/
   ```

3. **Budget Reset**
   ```sql
   -- Reset monthly budgets
   UPDATE virtual_keys
   SET 
     current_period_spend = 0,
     period_start_date = DATE_TRUNC('month', CURRENT_DATE),
     last_reset_at = NOW()
   WHERE budget_period = 'monthly'
     AND period_start_date < DATE_TRUNC('month', CURRENT_DATE);
   ```

### Quarterly Provider Review

**Objective**: Evaluate provider performance and costs

**Procedure**:

1. **Generate Provider Scorecards**
   ```python
   # Calculate provider metrics
   metrics = {}
   for provider in providers:
     metrics[provider] = {
       'total_cost': calculate_total_cost(provider, quarter),
       'avg_latency': calculate_avg_latency(provider, quarter),
       'error_rate': calculate_error_rate(provider, quarter),
       'availability': calculate_availability(provider, quarter),
       'cost_per_token': calculate_cost_per_token(provider, quarter)
     }
   
   # Generate scorecard
   generate_scorecard(metrics, output_file=f'provider_scorecard_Q{quarter}.pdf')
   ```

2. **Negotiate Rates**
   ```bash
   # Prepare negotiation data
   ./scripts/prepare-negotiation-data.sh \
     --providers "openai,anthropic" \
     --period "last-quarter" \
     --include-projections
   ```

---

## Cost Configuration Management

### Adding New Model Costs

**Objective**: Configure pricing for new models

**Procedure**:

1. **Create Cost Configuration**
   ```sql
   -- Add new model cost
   INSERT INTO model_cost (
     cost_name,
     input_token_cost,
     output_token_cost,
     pricing_model,
     effective_date,
     is_active
   ) VALUES (
     'GPT-4 Turbo 2024',
     0.01,    -- $0.01 per 1K input tokens
     0.03,    -- $0.03 per 1K output tokens
     'Standard',
     CURRENT_DATE,
     true
   );
   ```

2. **Map to Provider Models**
   ```sql
   -- Create model mapping
   INSERT INTO model_cost_mapping (
     model_cost_id,
     model_provider_mapping_id,
     is_active
   ) VALUES (
     (SELECT id FROM model_cost WHERE cost_name = 'GPT-4 Turbo 2024'),
     (SELECT id FROM model_provider_mapping WHERE model_alias = 'gpt-4-turbo'),
     true
   );
   ```

3. **Validate Configuration**
   ```bash
   # Test cost calculation
   curl -X POST http://admin-api:5002/api/costs/validate \
     -H "Content-Type: application/json" \
     -d '{
       "model": "gpt-4-turbo",
       "prompt_tokens": 1000,
       "completion_tokens": 500
     }'
   ```

### Updating Pricing

**Objective**: Update model pricing when providers change rates

**Procedure**:

1. **Archive Current Pricing**
   ```sql
   -- Deactivate old pricing
   UPDATE model_cost
   SET 
     is_active = false,
     expiry_date = CURRENT_DATE
   WHERE cost_name = 'Old Model Pricing';
   ```

2. **Apply New Pricing**
   ```sql
   -- Add new pricing with higher priority
   INSERT INTO model_cost (
     cost_name,
     input_token_cost,
     output_token_cost,
     pricing_model,
     effective_date,
     priority,
     is_active
   ) VALUES (
     'Updated Model Pricing',
     0.012,   -- New rates
     0.035,
     'Standard',
     CURRENT_DATE,
     100,     -- Higher priority
     true
   );
   ```

3. **Clear Cache**
   ```bash
   # Invalidate cost cache
   redis-cli --scan --pattern "model:cost:*" | xargs redis-cli DEL
   ```

---

## Budget Management

### Setting Virtual Key Budgets

**Objective**: Configure and manage virtual key spending limits

**Procedure**:

1. **Initial Budget Setup**
   ```sql
   -- Set budget for new key
   UPDATE virtual_keys
   SET 
     budget_amount = 1000.00,
     budget_period = 'monthly',
     budget_alert_threshold = 0.8,
     hard_limit_enabled = true
   WHERE id = $virtual_key_id;
   ```

2. **Configure Alerts**
   ```bash
   # Set up budget alerts
   curl -X POST http://admin-api:5002/api/budgets/alerts \
     -H "Content-Type: application/json" \
     -d '{
       "virtual_key_id": "'$KEY_ID'",
       "thresholds": [50, 80, 90, 100],
       "notification_channels": ["email", "slack", "webhook"]
     }'
   ```

3. **Implement Progressive Limits**
   ```yaml
   # Progressive rate limiting based on budget
   budget_rules:
     - threshold: 0.5
       rate_limit: 1000  # requests per minute
     - threshold: 0.8
       rate_limit: 100
     - threshold: 0.9
       rate_limit: 10
     - threshold: 1.0
       rate_limit: 0    # Complete block
   ```

### Budget Monitoring

**Objective**: Track budget utilization and prevent overages

**Procedure**:

1. **Real-time Monitoring**
   ```bash
   # Monitor budget utilization
   watch -n 10 'curl -s http://prometheus:9090/api/v1/query?query=conduit_virtualkey_budget_utilization_percent | jq ".data.result[] | select(.value[1] | tonumber > 70)"'
   ```

2. **Automated Interventions**
   ```python
   # Budget enforcement automation
   def check_budgets():
     keys_near_limit = query_prometheus(
       'conduit_virtualkey_budget_utilization_percent > 95'
     )
     
     for key in keys_near_limit:
       # Apply emergency rate limit
       redis.set(f"ratelimit:{key['virtual_key_id']}", "1")
       
       # Notify customer
       send_notification(
         key['virtual_key_id'],
         "Budget limit approaching - service may be restricted"
       )
   ```

---

## Provider Management

### Onboarding New Provider

**Objective**: Add new LLM provider to the system

**Procedure**:

1. **Create Provider Record**
   ```sql
   INSERT INTO provider (
     name,
     provider_type,
     base_url,
     is_active,
     routing_weight,
     max_retries,
     timeout_seconds
   ) VALUES (
     'New Provider',
     'OpenAICompatible',
     'https://api.newprovider.com',
     false,  -- Start inactive
     1.0,
     3,
     30
   );
   ```

2. **Configure Authentication**
   ```sql
   INSERT INTO provider_key_credential (
     provider_id,
     key_name,
     encrypted_key,
     is_active,
     rate_limit,
     provider_account_group
   ) VALUES (
     (SELECT id FROM provider WHERE name = 'New Provider'),
     'Primary Key',
     encrypt($api_key),
     true,
     1000,
     'default'
   );
   ```

3. **Test Provider**
   ```bash
   # Run provider health check
   ./scripts/test-provider.sh \
     --provider "New Provider" \
     --model "test-model" \
     --dry-run
   ```

4. **Configure Costs**
   ```sql
   -- Add provider-specific costs
   INSERT INTO model_cost (
     cost_name,
     input_token_cost,
     output_token_cost,
     pricing_model,
     effective_date,
     is_active
   ) VALUES (
     'New Provider Standard Pricing',
     0.002,
     0.006,
     'Standard',
     CURRENT_DATE,
     true
   );
   ```

### Provider Failover

**Objective**: Handle provider outages gracefully

**Procedure**:

1. **Detect Provider Failure**
   ```promql
   # Alert on provider health
   conduit_provider_health{provider="$PROVIDER"} == 0
   ```

2. **Initiate Failover**
   ```bash
   # Disable failed provider
   kubectl patch configmap provider-config \
     --patch '{"data":{"'$PROVIDER'_enabled":"false"}}'
   
   # Increase weight for backup providers
   ./scripts/adjust-routing-weights.sh \
     --decrease "$PROVIDER:0" \
     --increase "$BACKUP_PROVIDER:2.0"
   ```

3. **Monitor Failover**
   ```bash
   # Check traffic distribution
   watch -n 5 'curl -s http://prometheus:9090/api/v1/query?query=sum+by+\(provider\)+\(rate\(conduit_model_requests_total[1m]\)\) | jq .data.result'
   ```

---

## Maintenance Procedures

### Prometheus Maintenance

**Objective**: Maintain Prometheus performance and storage

**Procedure**:

1. **Compact TSDB**
   ```bash
   # Trigger manual compaction
   curl -X POST http://prometheus:9090/api/v1/admin/tsdb/compact
   ```

2. **Clean Tombstones**
   ```bash
   # Clean deleted series
   curl -X POST http://prometheus:9090/api/v1/admin/tsdb/clean_tombstones
   ```

3. **Manage Retention**
   ```yaml
   # Update retention policy
   prometheus:
     retention_time: 30d
     retention_size: 100GB
   ```

### Grafana Dashboard Updates

**Objective**: Keep dashboards current and performant

**Procedure**:

1. **Export Current Dashboards**
   ```bash
   # Backup all dashboards
   for uid in $(curl -s http://grafana:3000/api/search | jq -r '.[].uid'); do
     curl -s http://grafana:3000/api/dashboards/uid/$uid \
       -H "Authorization: Bearer $GRAFANA_API_KEY" \
       > backups/dashboard_$uid_$(date +%Y%m%d).json
   done
   ```

2. **Optimize Queries**
   ```promql
   # Replace heavy queries with recording rules
   - record: conduit:daily_cost
     expr: sum(increase(conduit_cost_total_dollars[1d]))
   ```

3. **Update Variables**
   ```json
   {
     "templating": {
       "list": [{
         "name": "provider",
         "query": "label_values(conduit_cost_total_dollars, provider)",
         "refresh": 2,
         "sort": 1
       }]
     }
   }
   ```

---

## Backup and Recovery

### Daily Backups

**Objective**: Ensure cost data is protected

**Procedure**:

1. **Database Backup**
   ```bash
   # Backup cost-related tables
   pg_dump -h $DB_HOST -U $DB_USER -d conduit \
     -t model_cost \
     -t model_cost_mapping \
     -t virtual_keys \
     -t llm_usage_log \
     -t cost_reconciliation \
     > /backups/cost_data_$(date +%Y%m%d).sql
   ```

2. **Prometheus Snapshot**
   ```bash
   # Create Prometheus snapshot
   curl -X POST http://prometheus:9090/api/v1/admin/tsdb/snapshot
   
   # Copy to backup location
   rsync -avz /prometheus/snapshots/ /backups/prometheus/
   ```

3. **Configuration Backup**
   ```bash
   # Backup all configurations
   tar -czf /backups/config_$(date +%Y%m%d).tar.gz \
     /etc/conduit/ \
     /etc/prometheus/ \
     /etc/grafana/
   ```

### Recovery Procedures

**Objective**: Restore cost tracking after failure

**Procedure**:

1. **Restore Database**
   ```bash
   # Restore from backup
   psql -h $DB_HOST -U $DB_USER -d conduit < /backups/cost_data_YYYYMMDD.sql
   ```

2. **Restore Prometheus Data**
   ```bash
   # Stop Prometheus
   systemctl stop prometheus
   
   # Restore snapshot
   rm -rf /prometheus/data/*
   cp -r /backups/prometheus/snapshot_YYYYMMDD/* /prometheus/data/
   
   # Start Prometheus
   systemctl start prometheus
   ```

3. **Verify Recovery**
   ```bash
   # Check data integrity
   ./scripts/verify-cost-data.sh --date "$(date +%Y-%m-%d)"
   ```

---

## Capacity Planning

### Monthly Capacity Review

**Objective**: Ensure systems can handle growth

**Procedure**:

1. **Analyze Growth Trends**
   ```python
   # Project future load
   import numpy as np
   from sklearn.linear_model import LinearRegression
   
   # Load historical data
   data = load_metrics_history()
   
   # Fit growth model
   X = np.array(range(len(data))).reshape(-1, 1)
   y = data['daily_requests'].values
   
   model = LinearRegression()
   model.fit(X, y)
   
   # Project 3 months ahead
   future_points = np.array(range(len(data), len(data) + 90)).reshape(-1, 1)
   projections = model.predict(future_points)
   
   print(f"Projected requests in 3 months: {projections[-1]:,.0f}/day")
   ```

2. **Resource Requirements**
   ```bash
   # Calculate required resources
   ./scripts/capacity-calculator.sh \
     --current-load "$(get_current_metrics)" \
     --growth-rate 0.15 \
     --period 90d
   ```

3. **Scale Infrastructure**
   ```yaml
   # Update resource allocations
   prometheus:
     resources:
       requests:
         memory: "16Gi"
         cpu: "4"
       limits:
         memory: "32Gi"
         cpu: "8"
   ```

---

## Compliance and Auditing

### Monthly Audit

**Objective**: Ensure compliance with financial controls

**Procedure**:

1. **Generate Audit Trail**
   ```sql
   -- Extract audit records
   SELECT 
     al.timestamp,
     al.user_id,
     al.action,
     al.resource_type,
     al.resource_id,
     al.changes
   FROM audit_log al
   WHERE al.resource_type IN ('model_cost', 'virtual_key', 'provider')
     AND al.timestamp >= DATE_TRUNC('month', CURRENT_DATE)
   ORDER BY al.timestamp;
   ```

2. **Verify Cost Accuracy**
   ```python
   # Cross-check calculations
   def audit_cost_calculations(sample_size=1000):
     samples = get_random_usage_samples(sample_size)
     discrepancies = []
     
     for sample in samples:
       recorded_cost = sample['total_cost']
       calculated_cost = calculate_cost(
         sample['model'],
         sample['prompt_tokens'],
         sample['completion_tokens']
       )
       
       if abs(recorded_cost - calculated_cost) > 0.001:
         discrepancies.append({
           'id': sample['id'],
           'recorded': recorded_cost,
           'calculated': calculated_cost,
           'difference': recorded_cost - calculated_cost
         })
     
     return discrepancies
   ```

3. **Compliance Report**
   ```bash
   # Generate compliance report
   ./scripts/generate-compliance-report.sh \
     --period "$(date +%Y-%m)" \
     --include-audit-trail \
     --include-reconciliation \
     --output "/reports/compliance_$(date +%Y%m).pdf"
   ```

### Access Control Review

**Objective**: Ensure proper access to cost data

**Procedure**:

1. **Review Permissions**
   ```sql
   -- Check user permissions
   SELECT 
     u.username,
     r.role_name,
     p.permission,
     p.resource
   FROM users u
   JOIN user_roles ur ON u.id = ur.user_id
   JOIN roles r ON ur.role_id = r.id
   JOIN role_permissions rp ON r.id = rp.role_id
   JOIN permissions p ON rp.permission_id = p.id
   WHERE p.resource LIKE '%cost%'
   ORDER BY u.username, p.resource;
   ```

2. **Rotate API Keys**
   ```bash
   # Rotate monitoring API keys
   ./scripts/rotate-api-keys.sh \
     --service prometheus \
     --service grafana \
     --service alertmanager
   ```

3. **Update Access Logs**
   ```sql
   -- Log access review
   INSERT INTO access_review_log (
     reviewed_by,
     review_date,
     changes_made,
     next_review_date
   ) VALUES (
     CURRENT_USER,
     CURRENT_DATE,
     'Rotated API keys, removed 2 inactive users',
     CURRENT_DATE + INTERVAL '90 days'
   );
   ```

---

## Appendix

### Standard Operating Metrics

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| Cache Hit Ratio | > 95% | < 80% |
| Batch Processing Lag | < 10s | > 30s |
| Cost Calculation Latency | < 10ms | > 50ms |
| Provider Availability | > 99.9% | < 99% |
| Dashboard Load Time | < 2s | > 5s |
| Alert Response Time | < 5min | > 15min |

### Automation Scripts Repository

All operational scripts are located in:
- `/opt/conduit/scripts/operations/`
- Git repository: `https://github.com/conduit/ops-scripts`

### Contact Information

- **Operations Team**: ops@conduit.ai
- **On-Call**: PagerDuty rotation "conduit-platform"
- **Escalation**: platform-leads@conduit.ai

### Change Log

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2024-01-15 | 1.0 | Initial procedures | Platform Team |
| 2024-02-01 | 1.1 | Added budget management | Finance Team |
| 2024-03-01 | 1.2 | Updated provider procedures | Operations |