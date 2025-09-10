# Cost Observability Migration Guide

## Overview

This guide provides step-by-step instructions for migrating to or upgrading the Conduit cost observability system. It covers migrations from legacy systems, version upgrades, and data migration procedures.

## Migration Scenarios

1. [Fresh Installation](#fresh-installation)
2. [Migrating from Legacy Cost Tracking](#migrating-from-legacy-cost-tracking)
3. [Version Upgrades](#version-upgrades)
4. [Provider Migration](#provider-migration)
5. [Database Migration](#database-migration)
6. [Monitoring Stack Migration](#monitoring-stack-migration)

---

## Fresh Installation

### Prerequisites

- Kubernetes cluster 1.24+
- PostgreSQL 14+
- Redis 6.2+
- Prometheus 2.40+
- Grafana 9.0+
- 50GB available storage for metrics

### Installation Steps

#### Step 1: Database Setup

```sql
-- Create database and schema
CREATE DATABASE conduit_cost;
\c conduit_cost;

-- Create cost tracking tables
CREATE TABLE model_cost (
    id SERIAL PRIMARY KEY,
    cost_name VARCHAR(255) NOT NULL,
    input_token_cost DECIMAL(10, 8) NOT NULL,
    output_token_cost DECIMAL(10, 8) NOT NULL,
    embedding_token_cost DECIMAL(10, 8),
    image_cost_per_image DECIMAL(10, 8),
    model_type VARCHAR(50) NOT NULL,
    pricing_model VARCHAR(50) DEFAULT 'Standard',
    is_active BOOLEAN DEFAULT true,
    effective_date TIMESTAMP NOT NULL,
    expiry_date TIMESTAMP,
    priority INTEGER DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE model_cost_mapping (
    id SERIAL PRIMARY KEY,
    model_cost_id INTEGER REFERENCES model_cost(id),
    model_provider_mapping_id INTEGER NOT NULL,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE virtual_keys (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    budget_amount DECIMAL(10, 2),
    budget_period VARCHAR(50),
    current_period_spend DECIMAL(10, 2) DEFAULT 0,
    total_spend DECIMAL(10, 2) DEFAULT 0,
    period_start_date TIMESTAMP,
    hard_limit_enabled BOOLEAN DEFAULT false,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE llm_usage_log (
    id BIGSERIAL PRIMARY KEY,
    virtual_key_id UUID REFERENCES virtual_keys(id),
    model VARCHAR(255) NOT NULL,
    provider VARCHAR(255) NOT NULL,
    prompt_tokens INTEGER,
    completion_tokens INTEGER,
    total_tokens INTEGER,
    total_cost DECIMAL(10, 6),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes for performance
CREATE INDEX idx_usage_log_created_key ON llm_usage_log(created_at, virtual_key_id);
CREATE INDEX idx_usage_log_model_cost ON llm_usage_log(model, total_cost);
CREATE INDEX idx_virtual_keys_budget ON virtual_keys(budget_amount, current_period_spend);

-- Create partitioning for large tables
CREATE TABLE llm_usage_log_2024_01 PARTITION OF llm_usage_log
FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');
```

#### Step 2: Deploy Core Services

```bash
# Deploy namespace
kubectl create namespace conduit-cost

# Deploy Redis for caching
kubectl apply -f - <<EOF
apiVersion: apps/v1
kind: Deployment
metadata:
  name: redis-cache
  namespace: conduit-cost
spec:
  replicas: 1
  selector:
    matchLabels:
      app: redis-cache
  template:
    metadata:
      labels:
        app: redis-cache
    spec:
      containers:
      - name: redis
        image: redis:7-alpine
        ports:
        - containerPort: 6379
        resources:
          requests:
            memory: "2Gi"
            cpu: "500m"
          limits:
            memory: "4Gi"
            cpu: "1"
---
apiVersion: v1
kind: Service
metadata:
  name: redis-cache
  namespace: conduit-cost
spec:
  selector:
    app: redis-cache
  ports:
  - port: 6379
    targetPort: 6379
EOF

# Deploy batch processor
kubectl apply -f - <<EOF
apiVersion: apps/v1
kind: Deployment
metadata:
  name: batch-processor
  namespace: conduit-cost
spec:
  replicas: 1
  selector:
    matchLabels:
      app: batch-processor
  template:
    metadata:
      labels:
        app: batch-processor
    spec:
      containers:
      - name: processor
        image: conduit/batch-processor:latest
        env:
        - name: DATABASE_URL
          valueFrom:
            secretKeyRef:
              name: database-credentials
              key: url
        - name: REDIS_URL
          value: "redis://redis-cache:6379"
        - name: BATCH_SIZE
          value: "100"
        - name: FLUSH_INTERVAL
          value: "10s"
EOF
```

#### Step 3: Configure Prometheus

```yaml
# prometheus-config.yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'conduit-cost'
    kubernetes_sd_configs:
    - role: pod
      namespaces:
        names:
        - conduit-cost
    relabel_configs:
    - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
      action: keep
      regex: true
    - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_path]
      action: replace
      target_label: __metrics_path__
      regex: (.+)
    - source_labels: [__address__, __meta_kubernetes_pod_annotation_prometheus_io_port]
      action: replace
      regex: ([^:]+)(?::\d+)?;(\d+)
      replacement: $1:$2
      target_label: __address__

rule_files:
  - '/etc/prometheus/rules/*.yml'

alerting:
  alertmanagers:
  - static_configs:
    - targets:
      - alertmanager:9093
```

#### Step 4: Deploy Prometheus and Grafana

```bash
# Deploy Prometheus
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm install prometheus prometheus-community/prometheus \
  --namespace conduit-cost \
  --values prometheus-values.yaml

# Deploy Grafana
helm repo add grafana https://grafana.github.io/helm-charts
helm install grafana grafana/grafana \
  --namespace conduit-cost \
  --set persistence.enabled=true \
  --set persistence.size=10Gi
```

#### Step 5: Import Dashboards and Alerts

```bash
# Import Grafana dashboard
GRAFANA_POD=$(kubectl get pods -n conduit-cost -l app.kubernetes.io/name=grafana -o jsonpath='{.items[0].metadata.name}')
kubectl cp cost-observability-dashboard.json conduit-cost/$GRAFANA_POD:/tmp/dashboard.json
kubectl exec -n conduit-cost $GRAFANA_POD -- \
  curl -X POST http://localhost:3000/api/dashboards/db \
  -H "Content-Type: application/json" \
  -d @/tmp/dashboard.json

# Apply Prometheus alert rules
kubectl create configmap prometheus-alerts \
  --from-file=alerts.yml=cost-alerts.yml \
  -n conduit-cost
```

#### Step 6: Verify Installation

```bash
# Check all pods are running
kubectl get pods -n conduit-cost

# Test metrics endpoint
kubectl port-forward -n conduit-cost svc/conduit-api 8080:8080
curl http://localhost:8080/metrics | grep conduit_cost

# Check Prometheus targets
kubectl port-forward -n conduit-cost svc/prometheus-server 9090:9090
curl http://localhost:9090/api/v1/targets | jq '.data.activeTargets'

# Access Grafana
kubectl port-forward -n conduit-cost svc/grafana 3000:3000
# Open http://localhost:3000 in browser
```

---

## Migrating from Legacy Cost Tracking

### Pre-Migration Assessment

```bash
#!/bin/bash
# assess-legacy-system.sh

echo "=== Legacy System Assessment ==="

# Check data volume
LEGACY_RECORDS=$(psql -h $LEGACY_DB -c "SELECT COUNT(*) FROM cost_records" -t)
echo "Legacy records to migrate: $LEGACY_RECORDS"

# Check date range
DATE_RANGE=$(psql -h $LEGACY_DB -c "SELECT MIN(created_at), MAX(created_at) FROM cost_records" -t)
echo "Date range: $DATE_RANGE"

# Estimate migration time
ESTIMATED_TIME=$((LEGACY_RECORDS / 10000))  # ~10k records per minute
echo "Estimated migration time: ${ESTIMATED_TIME} minutes"

# Check for data quality issues
echo "Checking data quality..."
psql -h $LEGACY_DB -c "
  SELECT 
    COUNT(*) FILTER (WHERE cost IS NULL) as null_costs,
    COUNT(*) FILTER (WHERE cost < 0) as negative_costs,
    COUNT(*) FILTER (WHERE model IS NULL) as null_models
  FROM cost_records
"
```

### Data Mapping

```python
# legacy_mapping.py
LEGACY_TO_NEW_MAPPING = {
    'models': {
        'gpt-4-32k': 'gpt-4',
        'text-davinci-003': 'gpt-3.5-turbo',
        'claude-v1': 'claude-3-sonnet',
    },
    'providers': {
        'openai_legacy': 'openai',
        'anthropic_old': 'anthropic',
    },
    'fields': {
        'user_id': 'virtual_key_id',
        'tokens_used': 'total_tokens',
        'cost_usd': 'total_cost',
        'timestamp': 'created_at',
    }
}

def map_legacy_record(legacy_record):
    """Map legacy record to new schema"""
    return {
        'virtual_key_id': legacy_record.get('user_id'),
        'model': LEGACY_TO_NEW_MAPPING['models'].get(
            legacy_record['model'], 
            legacy_record['model']
        ),
        'provider': LEGACY_TO_NEW_MAPPING['providers'].get(
            legacy_record['provider'],
            legacy_record['provider']
        ),
        'prompt_tokens': legacy_record.get('prompt_tokens', 0),
        'completion_tokens': legacy_record.get('completion_tokens', 0),
        'total_tokens': legacy_record.get('tokens_used', 0),
        'total_cost': float(legacy_record.get('cost_usd', 0)),
        'created_at': legacy_record['timestamp']
    }
```

### Migration Execution

```python
#!/usr/bin/env python3
# migrate_legacy_data.py

import psycopg2
from psycopg2.extras import RealDictCursor
import logging
from datetime import datetime
import sys

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

def migrate_batch(legacy_conn, new_conn, offset, batch_size=1000):
    """Migrate a batch of records"""
    with legacy_conn.cursor(cursor_factory=RealDictCursor) as legacy_cur:
        legacy_cur.execute("""
            SELECT * FROM cost_records 
            ORDER BY timestamp 
            LIMIT %s OFFSET %s
        """, (batch_size, offset))
        
        records = legacy_cur.fetchall()
        
        if not records:
            return 0
        
        with new_conn.cursor() as new_cur:
            for record in records:
                mapped = map_legacy_record(record)
                
                # Insert into new schema
                new_cur.execute("""
                    INSERT INTO llm_usage_log (
                        virtual_key_id, model, provider,
                        prompt_tokens, completion_tokens,
                        total_tokens, total_cost, created_at
                    ) VALUES (
                        %(virtual_key_id)s, %(model)s, %(provider)s,
                        %(prompt_tokens)s, %(completion_tokens)s,
                        %(total_tokens)s, %(total_cost)s, %(created_at)s
                    )
                """, mapped)
            
            new_conn.commit()
            logger.info(f"Migrated batch at offset {offset}")
            
        return len(records)

def main():
    legacy_conn = psycopg2.connect(os.environ['LEGACY_DATABASE_URL'])
    new_conn = psycopg2.connect(os.environ['NEW_DATABASE_URL'])
    
    try:
        offset = 0
        total_migrated = 0
        
        while True:
            batch_count = migrate_batch(legacy_conn, new_conn, offset)
            if batch_count == 0:
                break
            
            total_migrated += batch_count
            offset += batch_count
            
            # Progress update
            if total_migrated % 10000 == 0:
                logger.info(f"Total migrated: {total_migrated}")
        
        logger.info(f"Migration complete. Total records: {total_migrated}")
        
    finally:
        legacy_conn.close()
        new_conn.close()

if __name__ == "__main__":
    main()
```

### Post-Migration Validation

```sql
-- Validate migration completeness
WITH legacy_stats AS (
  SELECT 
    COUNT(*) as record_count,
    SUM(cost_usd) as total_cost,
    MIN(timestamp) as min_date,
    MAX(timestamp) as max_date
  FROM legacy_db.cost_records
),
new_stats AS (
  SELECT 
    COUNT(*) as record_count,
    SUM(total_cost) as total_cost,
    MIN(created_at) as min_date,
    MAX(created_at) as max_date
  FROM llm_usage_log
  WHERE created_at <= (SELECT MAX(timestamp) FROM legacy_db.cost_records)
)
SELECT 
  'Records' as metric,
  legacy_stats.record_count as legacy_value,
  new_stats.record_count as new_value,
  CASE 
    WHEN legacy_stats.record_count = new_stats.record_count THEN 'PASS'
    ELSE 'FAIL'
  END as status
FROM legacy_stats, new_stats
UNION ALL
SELECT 
  'Total Cost',
  legacy_stats.total_cost,
  new_stats.total_cost,
  CASE 
    WHEN ABS(legacy_stats.total_cost - new_stats.total_cost) < 0.01 THEN 'PASS'
    ELSE 'FAIL'
  END
FROM legacy_stats, new_stats;
```

---

## Version Upgrades

### Upgrading from v1.x to v2.x

#### Breaking Changes

1. **Metric Name Changes**
   - `conduit_cost_dollars` → `conduit_cost_total_dollars`
   - `conduit_key_spend` → `conduit_virtualkey_spend_total`

2. **Database Schema Changes**
   ```sql
   -- Add new columns for v2.x
   ALTER TABLE model_cost 
   ADD COLUMN pricing_model VARCHAR(50) DEFAULT 'Standard',
   ADD COLUMN batch_processing_multiplier DECIMAL(3, 2),
   ADD COLUMN cached_input_token_cost DECIMAL(10, 8);
   
   -- Add new tables
   CREATE TABLE cost_reconciliation (
     id SERIAL PRIMARY KEY,
     provider VARCHAR(255),
     period_start TIMESTAMP,
     period_end TIMESTAMP,
     internal_cost DECIMAL(10, 2),
     provider_reported_cost DECIMAL(10, 2),
     discrepancy DECIMAL(10, 2),
     reconciled_at TIMESTAMP
   );
   ```

3. **Configuration Changes**
   ```yaml
   # Old configuration (v1.x)
   cost_tracking:
     enabled: true
     cache_ttl: 300
   
   # New configuration (v2.x)
   cost_observability:
     enabled: true
     cache:
       provider: redis
       ttl: 300
     batch_processing:
       enabled: true
       size: 100
       interval: 10s
   ```

#### Upgrade Procedure

```bash
#!/bin/bash
# upgrade_v1_to_v2.sh

set -e

echo "Starting upgrade from v1.x to v2.x..."

# Step 1: Backup current data
echo "Creating backup..."
pg_dump -h $DB_HOST -d conduit > backup_v1_$(date +%Y%m%d).sql

# Step 2: Apply database migrations
echo "Applying database migrations..."
psql -h $DB_HOST -d conduit < migrations/v1_to_v2.sql

# Step 3: Update Prometheus rules
echo "Updating Prometheus rules..."
kubectl delete configmap prometheus-alerts -n conduit-cost
kubectl create configmap prometheus-alerts \
  --from-file=alerts.yml=alerts_v2.yml \
  -n conduit-cost

# Step 4: Update Grafana dashboards
echo "Updating Grafana dashboards..."
./scripts/update-dashboards.sh --version v2

# Step 5: Deploy new services
echo "Deploying new services..."
kubectl apply -f deployments/v2/

# Step 6: Migrate metrics
echo "Migrating metrics..."
./scripts/migrate-metrics.sh --from v1 --to v2

# Step 7: Verify upgrade
echo "Verifying upgrade..."
./scripts/verify-upgrade.sh --version v2

echo "Upgrade complete!"
```

### Rollback Procedure

```bash
#!/bin/bash
# rollback_v2_to_v1.sh

set -e

echo "Starting rollback from v2.x to v1.x..."

# Step 1: Stop v2 services
kubectl scale deployment --all --replicas=0 -n conduit-cost

# Step 2: Restore database
psql -h $DB_HOST -d conduit < backup_v1_$(date +%Y%m%d).sql

# Step 3: Deploy v1 services
kubectl apply -f deployments/v1/

# Step 4: Restore v1 configurations
kubectl apply -f configs/v1/

# Step 5: Scale up services
kubectl scale deployment --all --replicas=1 -n conduit-cost

echo "Rollback complete!"
```

---

## Provider Migration

### Migrating to New Provider

```python
#!/usr/bin/env python3
# migrate_provider.py

import argparse
import psycopg2
from datetime import datetime

def migrate_provider(old_provider, new_provider, cutover_date):
    """Migrate from one provider to another"""
    
    conn = psycopg2.connect(os.environ['DATABASE_URL'])
    
    try:
        with conn.cursor() as cur:
            # Step 1: Add new provider
            cur.execute("""
                INSERT INTO provider (name, provider_type, is_active)
                VALUES (%s, %s, true)
                ON CONFLICT (name) DO NOTHING
            """, (new_provider['name'], new_provider['type']))
            
            # Step 2: Copy model mappings
            cur.execute("""
                INSERT INTO model_provider_mapping (
                    model_alias, provider_model_id, provider_id
                )
                SELECT 
                    model_alias,
                    provider_model_id,
                    (SELECT id FROM provider WHERE name = %s)
                FROM model_provider_mapping mpm
                JOIN provider p ON mpm.provider_id = p.id
                WHERE p.name = %s
            """, (new_provider['name'], old_provider))
            
            # Step 3: Copy cost configurations
            cur.execute("""
                INSERT INTO model_cost_mapping (
                    model_cost_id, model_provider_mapping_id, is_active
                )
                SELECT 
                    mcm.model_cost_id,
                    new_mpm.id,
                    true
                FROM model_cost_mapping mcm
                JOIN model_provider_mapping old_mpm ON mcm.model_provider_mapping_id = old_mpm.id
                JOIN model_provider_mapping new_mpm ON old_mpm.model_alias = new_mpm.model_alias
                WHERE old_mpm.provider_id = (SELECT id FROM provider WHERE name = %s)
                  AND new_mpm.provider_id = (SELECT id FROM provider WHERE name = %s)
            """, (old_provider, new_provider['name']))
            
            # Step 4: Schedule cutover
            cur.execute("""
                UPDATE provider
                SET 
                    is_active = false,
                    deactivated_at = %s
                WHERE name = %s
            """, (cutover_date, old_provider))
            
            conn.commit()
            print(f"Provider migration scheduled for {cutover_date}")
            
    finally:
        conn.close()

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('--old-provider', required=True)
    parser.add_argument('--new-provider', required=True)
    parser.add_argument('--cutover-date', required=True)
    args = parser.parse_args()
    
    migrate_provider(
        args.old_provider,
        {'name': args.new_provider, 'type': 'openai'},
        datetime.fromisoformat(args.cutover_date)
    )
```

---

## Database Migration

### PostgreSQL Version Upgrade

```bash
#!/bin/bash
# upgrade_postgres.sh

# Step 1: Backup current database
pg_dumpall -h old_host -p 5432 -U postgres > full_backup.sql

# Step 2: Create new cluster
initdb -D /var/lib/postgresql/14/data

# Step 3: Start new PostgreSQL
pg_ctl -D /var/lib/postgresql/14/data start

# Step 4: Restore backup
psql -h localhost -p 5433 -U postgres < full_backup.sql

# Step 5: Update connection strings
kubectl set env deployment/conduit-api \
  DATABASE_URL="postgresql://user:pass@new_host:5433/conduit"

# Step 6: Verify migration
psql -h new_host -p 5433 -c "SELECT version();"
```

### Partitioning Large Tables

```sql
-- Convert existing table to partitioned table
BEGIN;

-- Rename old table
ALTER TABLE llm_usage_log RENAME TO llm_usage_log_old;

-- Create partitioned table
CREATE TABLE llm_usage_log (
    id BIGSERIAL,
    virtual_key_id UUID,
    model VARCHAR(255),
    provider VARCHAR(255),
    prompt_tokens INTEGER,
    completion_tokens INTEGER,
    total_tokens INTEGER,
    total_cost DECIMAL(10, 6),
    created_at TIMESTAMP
) PARTITION BY RANGE (created_at);

-- Create partitions
CREATE TABLE llm_usage_log_2024_01 PARTITION OF llm_usage_log
    FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');
    
CREATE TABLE llm_usage_log_2024_02 PARTITION OF llm_usage_log
    FOR VALUES FROM ('2024-02-01') TO ('2024-03-01');
    
-- Continue for all months...

-- Copy data
INSERT INTO llm_usage_log 
SELECT * FROM llm_usage_log_old;

-- Recreate constraints and indexes
ALTER TABLE llm_usage_log ADD PRIMARY KEY (id, created_at);
CREATE INDEX ON llm_usage_log (virtual_key_id, created_at);

-- Drop old table
DROP TABLE llm_usage_log_old;

COMMIT;
```

---

## Monitoring Stack Migration

### Migrating from CloudWatch to Prometheus

```python
#!/usr/bin/env python3
# migrate_cloudwatch_to_prometheus.py

import boto3
import requests
from datetime import datetime, timedelta

def export_cloudwatch_metrics(namespace, start_time, end_time):
    """Export metrics from CloudWatch"""
    
    cloudwatch = boto3.client('cloudwatch')
    
    metrics = []
    
    # Get metric list
    paginator = cloudwatch.get_paginator('list_metrics')
    for page in paginator.paginate(Namespace=namespace):
        for metric in page['Metrics']:
            # Get metric data
            response = cloudwatch.get_metric_statistics(
                Namespace=namespace,
                MetricName=metric['MetricName'],
                Dimensions=metric['Dimensions'],
                StartTime=start_time,
                EndTime=end_time,
                Period=300,  # 5 minutes
                Statistics=['Average', 'Sum', 'Maximum', 'Minimum']
            )
            
            metrics.append({
                'name': metric['MetricName'],
                'dimensions': metric['Dimensions'],
                'datapoints': response['Datapoints']
            })
    
    return metrics

def import_to_prometheus(metrics):
    """Import metrics to Prometheus"""
    
    for metric in metrics:
        # Convert to Prometheus format
        prometheus_metric = convert_to_prometheus_format(metric)
        
        # Push to Prometheus Pushgateway
        response = requests.post(
            'http://pushgateway:9091/metrics/job/migration',
            data=prometheus_metric
        )
        
        if response.status_code != 200:
            print(f"Failed to import {metric['name']}: {response.text}")

def convert_to_prometheus_format(metric):
    """Convert CloudWatch metric to Prometheus format"""
    
    lines = []
    metric_name = f"migrated_{metric['name'].lower()}"
    
    for datapoint in metric['datapoints']:
        timestamp = int(datapoint['Timestamp'].timestamp() * 1000)
        value = datapoint.get('Average') or datapoint.get('Sum')
        
        labels = ','.join([
            f'{d["Name"]}="{d["Value"]}"' 
            for d in metric['dimensions']
        ])
        
        lines.append(f"{metric_name}{{{labels}}} {value} {timestamp}")
    
    return '\n'.join(lines)

if __name__ == "__main__":
    # Export last 30 days of metrics
    end_time = datetime.now()
    start_time = end_time - timedelta(days=30)
    
    metrics = export_cloudwatch_metrics(
        'Conduit/Cost',
        start_time,
        end_time
    )
    
    import_to_prometheus(metrics)
    print(f"Migrated {len(metrics)} metrics")
```

---

## Migration Validation

### Comprehensive Validation Script

```bash
#!/bin/bash
# validate_migration.sh

set -e

ERRORS=0

echo "=== Migration Validation ==="
echo

# Check database
echo "Checking database..."
if psql -h $DB_HOST -c "SELECT 1" > /dev/null 2>&1; then
    echo "✓ Database connection successful"
else
    echo "✗ Database connection failed"
    ((ERRORS++))
fi

# Check table structure
for table in model_cost model_cost_mapping virtual_keys llm_usage_log; do
    if psql -h $DB_HOST -c "\d $table" > /dev/null 2>&1; then
        echo "✓ Table $table exists"
    else
        echo "✗ Table $table missing"
        ((ERRORS++))
    fi
done

# Check Redis
echo -e "\nChecking Redis..."
if redis-cli ping > /dev/null 2>&1; then
    echo "✓ Redis connection successful"
else
    echo "✗ Redis connection failed"
    ((ERRORS++))
fi

# Check Prometheus
echo -e "\nChecking Prometheus..."
if curl -s http://prometheus:9090/-/ready | grep -q "Prometheus is Ready"; then
    echo "✓ Prometheus is ready"
else
    echo "✗ Prometheus not ready"
    ((ERRORS++))
fi

# Check metrics
METRIC_COUNT=$(curl -s http://localhost:8080/metrics | grep -c "conduit_cost" || true)
if [ $METRIC_COUNT -gt 0 ]; then
    echo "✓ Cost metrics exposed: $METRIC_COUNT"
else
    echo "✗ No cost metrics found"
    ((ERRORS++))
fi

# Check Grafana
echo -e "\nChecking Grafana..."
if curl -s http://grafana:3000/api/health | grep -q "ok"; then
    echo "✓ Grafana is healthy"
else
    echo "✗ Grafana unhealthy"
    ((ERRORS++))
fi

# Check dashboards
DASHBOARD_COUNT=$(curl -s http://grafana:3000/api/search \
    -H "Authorization: Bearer $GRAFANA_API_KEY" | jq length)
if [ $DASHBOARD_COUNT -gt 0 ]; then
    echo "✓ Dashboards loaded: $DASHBOARD_COUNT"
else
    echo "✗ No dashboards found"
    ((ERRORS++))
fi

# Data integrity checks
echo -e "\nChecking data integrity..."

# Check for orphaned records
ORPHANED=$(psql -h $DB_HOST -t -c "
    SELECT COUNT(*) FROM llm_usage_log l
    LEFT JOIN virtual_keys v ON l.virtual_key_id = v.id
    WHERE v.id IS NULL
")
if [ $ORPHANED -eq 0 ]; then
    echo "✓ No orphaned usage records"
else
    echo "✗ Found $ORPHANED orphaned records"
    ((ERRORS++))
fi

# Check cost calculations
COST_CHECK=$(psql -h $DB_HOST -t -c "
    SELECT COUNT(*) FROM llm_usage_log
    WHERE total_cost IS NULL OR total_cost < 0
")
if [ $COST_CHECK -eq 0 ]; then
    echo "✓ All costs valid"
else
    echo "✗ Found $COST_CHECK invalid cost records"
    ((ERRORS++))
fi

# Summary
echo
echo "=== Validation Summary ==="
if [ $ERRORS -eq 0 ]; then
    echo "✓ All checks passed!"
    exit 0
else
    echo "✗ Found $ERRORS errors"
    exit 1
fi
```

---

## Migration Checklist

### Pre-Migration

- [ ] Full backup of existing system
- [ ] Document current configuration
- [ ] Test migration in staging environment
- [ ] Notify stakeholders of maintenance window
- [ ] Prepare rollback plan
- [ ] Verify backup restoration process

### During Migration

- [ ] Enable maintenance mode
- [ ] Stop incoming traffic
- [ ] Execute migration scripts
- [ ] Validate data integrity
- [ ] Update configuration files
- [ ] Test critical paths

### Post-Migration

- [ ] Run validation scripts
- [ ] Monitor error rates
- [ ] Check performance metrics
- [ ] Verify alerting works
- [ ] Document any issues
- [ ] Remove maintenance mode
- [ ] Monitor for 24 hours

---

## Troubleshooting Migration Issues

### Common Issues

#### Data Loss During Migration

```sql
-- Check for missing records
WITH source_count AS (
    SELECT COUNT(*) as count FROM old_database.cost_records
),
target_count AS (
    SELECT COUNT(*) as count FROM llm_usage_log
)
SELECT 
    source_count.count as source_records,
    target_count.count as target_records,
    source_count.count - target_count.count as missing_records
FROM source_count, target_count;

-- Find missing date ranges
SELECT 
    DATE(created_at) as date,
    COUNT(*) as records
FROM llm_usage_log
GROUP BY DATE(created_at)
HAVING COUNT(*) < (
    SELECT AVG(daily_count) * 0.5
    FROM (
        SELECT DATE(created_at) as date, COUNT(*) as daily_count
        FROM llm_usage_log
        GROUP BY DATE(created_at)
    ) daily_counts
)
ORDER BY date;
```

#### Performance Degradation After Migration

```bash
# Check index usage
psql -c "
SELECT 
    schemaname,
    tablename,
    indexname,
    idx_scan,
    idx_tup_read,
    idx_tup_fetch
FROM pg_stat_user_indexes
WHERE schemaname = 'public'
ORDER BY idx_scan DESC;
"

# Rebuild indexes if needed
psql -c "REINDEX TABLE llm_usage_log;"
psql -c "ANALYZE llm_usage_log;"
```

---

## Support Resources

### Documentation

- Migration Guide: This document
- Architecture: [/docs/operations/cost-observability-architecture.md]
- Troubleshooting: [/docs/troubleshooting/cost-observability-troubleshooting.md]

### Contact

- Slack: #cost-migration-support
- Email: platform-migration@conduit.ai
- Emergency: PagerDuty "migration-support"

### Tools and Scripts

All migration scripts are available at:
- Repository: https://github.com/conduit/migration-tools
- Path: `/opt/conduit/migration/`