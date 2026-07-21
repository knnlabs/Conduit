#!/usr/bin/env node
// Compatibility alias retained for existing developer and CI commands.
import { generate } from './generate-openapi-offline.mjs';

generate('admin');
