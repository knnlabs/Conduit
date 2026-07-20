#!/bin/sh
set -eu

# Grafana requires a non-empty URL for provisioned webhook contact points.
# Build a writable provisioning tree so alert rules remain available when
# outbound notifications are intentionally not configured.
provisioning_dir=/tmp/conduit-grafana-provisioning
rm -rf "$provisioning_dir"
cp -R /etc/grafana/provisioning "$provisioning_dir"

if [ -z "${CONDUIT_ALERT_WEBHOOK_URL:-}" ]; then
    rm -f "$provisioning_dir/alerting/contact-points.yml"
    echo "CONDUIT_ALERT_WEBHOOK_URL is not set; Grafana webhook notifications are disabled."
fi

export GF_PATHS_PROVISIONING="$provisioning_dir"
exec /run.sh
