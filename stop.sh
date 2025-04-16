#!/bin/bash

echo "Stopping ConduitLLM processes..."

# --- Dynamically read ports from start.sh ---
START_SCRIPT="./start.sh" # Path to your start script
CONDUIT_PORTS=()

# Check if start.sh exists
if [ ! -f "$START_SCRIPT" ]; then
  echo "Error: $START_SCRIPT not found. Cannot determine ports to stop."
  exit 1
fi

echo "Reading port configuration from $START_SCRIPT..."

# Use grep to find the port export lines and sed to extract the numbers
# This looks for lines like 'export VarName=NUMBER'
while IFS='=' read -r key value; do
  # Trim potential whitespace/comments after the number if needed
  value=$(echo "$value" | sed 's/[^0-9]*$//')
  # Basic validation: check if it looks like a port number
  if [[ "$value" =~ ^[0-9]+$ ]] && [ "$value" -gt 0 ] && [ "$value" -lt 65536 ]; then
    CONDUIT_PORTS+=("$value")
    echo "Found port: $value"
  else
    echo "Warning: Could not parse port value from line '$key=$value' in $START_SCRIPT"
  fi
# Grep for the specific variable names being exported
done < <(grep -E '^export (WebUIHttpPort|WebUIHttpsPort|HttpApiHttpPort|HttpApiHttpsPort)=' "$START_SCRIPT")

# Check if any ports were found
if [ ${#CONDUIT_PORTS[@]} -eq 0 ]; then
  echo "Error: Could not find any valid port definitions in $START_SCRIPT."
  echo "Expected lines like 'export WebUIHttpPort=5001'"
  exit 1
fi

echo "Identified ports to target: ${CONDUIT_PORTS[*]}"

# --- Method 1: Kill by specific process command line (More Precise) ---
# Target the specific dotnet run commands as launched by start.sh
echo "Attempting to stop processes by specific command line..."
PIDS_KILLED_BY_CMD=0

# Use pkill with the full command pattern if possible for precision
# Note: Adjust the path './' if start.sh is run from a different directory relative to the projects
pkill -f "dotnet run --project \./ConduitLLM\.WebUI" && echo "Sent TERM signal to WebUI process(es)." && PIDS_KILLED_BY_CMD=1
pkill -f "dotnet run --project \./ConduitLLM\.Http" && echo "Sent TERM signal to Http API process(es)." && PIDS_KILLED_BY_CMD=1

# Give processes a moment to shut down gracefully if signals were sent
if [ "$PIDS_KILLED_BY_CMD" -eq 1 ]; then
  echo "Waiting up to 3 seconds for graceful shutdown..."
  sleep 3
fi

# --- Method 2: Kill processes listening on specific ports (Targeted Fallback) ---
echo "Checking for any remaining ConduitLLM processes listening on ports: ${CONDUIT_PORTS[*]}..."

for PORT in "${CONDUIT_PORTS[@]}"; do
  # Find PID listening on the specific TCP port
  # ss options: -l listen, -n numeric, -t tcp, -p process
  # Use timeout to prevent ss from hanging indefinitely in rare cases
  PORT_PID=$(timeout 1s ss -lntp "sport = :$PORT" | grep -oP 'pid=\K[0-9]+' | head -1)

  if [ -n "$PORT_PID" ]; then
    echo "Found process PID $PORT_PID listening on port $PORT."
    # --- Verification Step: Check if the PID belongs to a relevant dotnet process ---
    # This prevents killing unrelated processes that might be using the same port
    CMDLINE=$(ps -p "$PORT_PID" -o cmd=)
    # Updated grep pattern to be slightly more robust against path variations
    if echo "$CMDLINE" | grep -Eq 'dotnet.*(ConduitLLM\.Http|ConduitLLM\.WebUI)'; then
      echo "Process $PORT_PID command line matches ConduitLLM. Attempting termination..."
      # Try graceful termination first (SIGTERM)
      if kill "$PORT_PID" 2>/dev/null; then
        echo "Sent TERM signal to PID $PORT_PID."
        sleep 0.5 # Brief wait
        # Check if it's still alive
        if kill -0 "$PORT_PID" 2>/dev/null; then
          echo "Process $PORT_PID did not terminate gracefully. Sending KILL signal (SIGKILL)..."
          kill -9 "$PORT_PID" 2>/dev/null || echo "Failed to send KILL signal to PID $PORT_PID on port $PORT."
        else
          echo "Process $PORT_PID terminated gracefully after TERM signal."
        fi
      else
         # If initial kill fails, it might already be gone or we lack permissions
         echo "Process $PORT_PID might have already terminated or cannot be signaled."
         # Force kill if it somehow still exists
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
  # Check if any process is listening on the port
  # Use timeout here as well
  if timeout 1s ss -lnt "sport = :$PORT" | grep -q "LISTEN"; then
    echo "WARN: Port $PORT is still in use."
    timeout 1s ss -lntp "sport = :$PORT" # Show details of what's using it
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

