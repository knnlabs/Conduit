#!/bin/bash

echo "Stopping ConduitLLM processes..."

# --- Get ports from environment variables or use defaults ---
WebUIHttpPort="${WebUIHttpPort:-5001}"
WebUIHttpsPort="${WebUIHttpsPort:-5002}"
HttpApiHttpPort="${HttpApiHttpPort:-5000}"
HttpApiHttpsPort="${HttpApiHttpsPort:-5003}"
CONDUIT_PORTS=($WebUIHttpPort $WebUIHttpsPort $HttpApiHttpPort $HttpApiHttpsPort)

echo "Identified ports to target: ${CONDUIT_PORTS[*]}"

# --- Method 1: Kill by specific process command line (More Precise) ---
echo "Attempting to stop processes by specific command line..."
PIDS_KILLED_BY_CMD=0

pkill -f "dotnet run --project \\./ConduitLLM\\.WebUI" && echo "Sent TERM signal to WebUI process(es)." && PIDS_KILLED_BY_CMD=1
pkill -f "dotnet run --project \\./ConduitLLM\\.Http" && echo "Sent TERM signal to Http API process(es)." && PIDS_KILLED_BY_CMD=1

if [ "$PIDS_KILLED_BY_CMD" -eq 1 ]; then
  echo "Waiting up to 3 seconds for graceful shutdown..."
  sleep 3
fi

# --- Method 2: Kill processes listening on specific ports (Targeted Fallback) ---
echo "Checking for any remaining ConduitLLM processes listening on ports: ${CONDUIT_PORTS[*]}..."

for PORT in "${CONDUIT_PORTS[@]}"; do
  PORT_PID=$(timeout 1s ss -lntp "sport = :$PORT" | grep -oP 'pid=\\K[0-9]+' | head -1)

  if [ -n "$PORT_PID" ]; then
    echo "Found process PID $PORT_PID listening on port $PORT."
    CMDLINE=$(ps -p "$PORT_PID" -o cmd=)
    if echo "$CMDLINE" | grep -Eq 'dotnet.*(ConduitLLM\\.Http|ConduitLLM\\.WebUI)'; then
      echo "Process $PORT_PID command line matches ConduitLLM. Attempting termination..."
      if kill "$PORT_PID" 2>/dev/null; then
        echo "Sent TERM signal to PID $PORT_PID."
        sleep 0.5
        if kill -0 "$PORT_PID" 2>/dev/null; then
          echo "Process $PORT_PID did not terminate gracefully. Sending KILL signal (SIGKILL)..."
          kill -9 "$PORT_PID" 2>/dev/null || echo "Failed to send KILL signal to PID $PORT_PID on port $PORT."
        else
          echo "Process $PORT_PID terminated gracefully after TERM signal."
        fi
      else
         echo "Process $PORT_PID might have already terminated or cannot be signaled."
         if kill -0 "$PORT_PID" 2>/dev/null; then
            echo "Attempting force kill (SIGKILL) on PID $PORT_PID..."
            kill -9 "$PORT_PID" 2>/dev/null
         fi
      fi
    else
      echo "Process $PORT_PID on port $PORT does not appear to be a ConduitLLM service (Command: '$CMDLINE'). Skipping."
    fi
  else
    echo "No process found listening on port $PORT." 
  fi
done

# --- Final Verification ---
echo "Verifying that ConduitLLM ports [${CONDUIT_PORTS[*]}] are free..."
ALL_PORTS_FREE=true
for PORT in "${CONDUIT_PORTS[@]}"; do
  if timeout 1s ss -lnt "sport = :$PORT" | grep -q "LISTEN"; then
    echo "WARN: Port $PORT is still in use."
    timeout 1s ss -lntp "sport = :$PORT"
    ALL_PORTS_FREE=false
  else
    echo "Port $PORT is free."
  fi
done

if [ "$ALL_PORTS_FREE" = true ]; then
  echo "All specified ConduitLLM ports appear to be free."
else
  echo "Warning: Some ConduitLLM ports may still be in use by other processes or termination is slow."
  echo "Manual intervention might be required."
fi

echo "Stop operation completed."
