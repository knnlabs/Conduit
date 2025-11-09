# Bulk Mapping Feature

## Overview
The Bulk Mapping feature allows administrators to quickly add multiple model mappings from a single provider, automatically discovering model capabilities and checking for conflicts.

## How to Use

1. **Navigate to Model Mappings**
   - Go to the Model Mappings page in the WebUI
   - Click the "Bulk Import" button in the top right

2. **Select a Provider**
   - Choose a provider from the dropdown
   - The system will automatically discover all available models from that provider

3. **Review Discovered Models**
   - Models are displayed in a table with:
     - Model ID and Display Name
     - Capabilities (icons show supported features)
     - Context length
     - Status (Available or Exists if already mapped)
   - Models with existing mappings are highlighted and cannot be selected

4. **Configure Settings**
   - **Default Priority**: Set the priority for all new mappings (0-100)
   - **Enable by default**: Toggle whether mappings should be active immediately

5. **Select Models**
   - Use checkboxes to select individual models
   - Use "Select All Available" to select all non-conflicting models
   - The counter shows how many models are selected

6. **Create Mappings**
   - Click "Create X Mappings" to create all selected mappings
   - The system will show success/failure counts
   - Any failures will be detailed in the response

## Technical Details

### API Endpoints

- `POST /api/model-mappings/bulk-discover`
  - Discovers models from a specific provider
  - Checks for existing mappings to identify conflicts
  - Returns enhanced model data with capabilities

- `POST /api/model-mappings/bulk-create`
  - Creates multiple mappings in a single request
  - Uses the Admin SDK's bulk create functionality
  - Returns detailed success/failure information

### Capabilities Detected

The feature automatically detects and displays:
- Vision support
- Image generation
- Audio transcription
- Text-to-speech
- Realtime audio
- Function calling
- Streaming support
- Video generation
- Embeddings
- Context length limits

### Conflict Detection

The system prevents duplicate mappings by:
1. Fetching all existing mappings
2. Comparing model IDs
3. Disabling selection for models that already have mappings
4. Showing conflict counts in notifications

### Error Handling

- Network errors are caught and displayed as notifications
- Partial failures are supported - successful mappings are created even if some fail
- Detailed error messages are provided for each failed mapping