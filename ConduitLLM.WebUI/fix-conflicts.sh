#!/bin/bash

# Remove all conflict markers from the specified files
files=(
  "src/components/providers/CreateProviderModal.tsx"
  "src/components/providers/ProvidersTable.tsx"
  "src/components/virtualkeys/VirtualKeysTable.tsx"
  "src/app/provider-health/page.tsx"
  "src/app/request-logs/page.tsx"
  "src/app/usage-analytics/page.tsx"
)

for file in "${files[@]}"; do
  if [ -f "$file" ]; then
    echo "Cleaning conflict markers from $file"
    # Remove conflict markers
    sed -i '/^<<<<<<< HEAD$/d' "$file"
    sed -i '/^=======$/d' "$file"
    sed -i '/^>>>>>>> 8c6e680a0779d662d0317f0cdb2a8f3f34cd47a6$/d' "$file"
  fi
done

echo "Conflict markers removed from all files"