#!/bin/bash

# This script is a wrapper for generate-openapi-from-build.sh
# It is intended to be run from the root of the repository
# It will run generate-openapi-from-build.sh from SDKs/Node/scripts
# and generate the API clients for both the Core and Admin APIs

./SDKs/Node/scripts/generate-openapi-from-build.sh

